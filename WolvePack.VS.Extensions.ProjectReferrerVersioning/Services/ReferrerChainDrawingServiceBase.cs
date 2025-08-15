using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    /// <summary>
    /// Base class for drawing referrer (reverse dependency) chains onto a WPF <see cref="Canvas"/>
    /// Handles: node rendering, hover highlighting (node + path to root),
    /// context menu driven version bumping, automatic child revision / patch propagation
    /// once all root nodes have selected versions, and tooltip / badge adornments.
    /// Concrete subclasses supply only layout (node positioning + edge routing) via <see cref="DrawChains"/>.
    /// </summary>
    public abstract class ReferrerChainDrawingServiceBase : IReferrerChainDrawingService
    {
        /// <inheritdoc />
        public ReferrerChainTheme Theme { get; set; }

        /// <inheritdoc />
        public abstract ReferrerChainLayoutMode LayoutMode { get; }

        protected ReferrerChainDrawingServiceBase(ReferrerChainTheme theme)
        {
            Theme = theme;
        }

        /// <summary>Last roots drawn (used to re-render after version changes).</summary>
        protected List<ReferrerChainNode> _lastRoots;
        /// <summary>Last canvas target (for redraw after interactive changes).</summary>
        protected Canvas _lastCanvas;
        /// <summary>Raised after automatic child version propagation completes.</summary>
        public event Action AllRootNodesUpdated;

        /// <summary>
        /// Internal layout record used by layout strategies to cache coordinate related dimensions (Depth/Row mapping).
        /// </summary>
        protected class NodeLayout
        {
            public ReferrerChainNode Node;    // Graph node
            public int Depth;                 // Column (or vertical rank) depending on layout
            public int Row;                   // Row (or horizontal rank)
        }

        /// <summary>Map node instance to a unique path string (rootIndex.childIndex...) used for edge highlighting.</summary>
        protected Dictionary<ReferrerChainNode, string> _nodePaths;

        /// <inheritdoc />
        public void DrawChainsBase(Canvas canvas, List<ReferrerChainNode> roots)
        {
            _lastCanvas = canvas;
            _lastRoots = roots;
            DrawChains(canvas, roots);
        }

        /// <summary>
        /// Implemented by derived classes to compute layout and invoke <see cref="DrawNode"/> / <see cref="DrawLine"/>.
        /// Must set <see cref="_nodePaths"/> to enable hover path highlighting.
        /// </summary>
        protected abstract void DrawChains(Canvas canvas, List<ReferrerChainNode> roots);

        /// <summary>
        /// Renders a single project node including gradient background, labels, context menu, tooltips,
        /// git change badge, root marker, and attaches mouse hover highlighting behavior.
        /// </summary>
        protected void DrawNode(Canvas canvas, ReferrerChainNode node, double x, double y, Brush fill, string nodePath = null)
        {
            // Background gradient for subtle depth (avoid flat color blocks)
            Color baseColor = ((SolidColorBrush)fill).Color;
            Color gradColor = Color.Multiply(baseColor, 1.15f);
            LinearGradientBrush gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(baseColor, 0),
                    new GradientStop(gradColor, 1)
                }
            };

            Brush borderBrush = node.IsRoot ? Theme.RootNodeBorderBrush : Theme.NodeBorderBrush;
            double borderThickness = node.IsRoot ? 4 : 2;

            Rectangle rect = new Rectangle
            {
                Width = Theme.NodeWidth,
                Height = Theme.NodeHeight,
                Fill = gradient,
                Stroke = borderBrush,
                StrokeThickness = borderThickness,
                RadiusX = 10,
                RadiusY = 10,
                Effect = new DropShadowEffect
                {
                    Color = Theme.ShadowColor,
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.18
                },
                IsHitTestVisible = true,
                Tag = new ReferrerChainNodeTag
                {
                    NodePath = nodePath,
                    ProjectName = node.Project.Name
                }
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);

            // Hover effects cascade through path to root and duplicate project nodes
            rect.MouseEnter += (s, e) => HighlightNodeAndPath(canvas, node, nodePath, true, rect);
            rect.MouseLeave += (s, e) => HighlightNodeAndPath(canvas, node, nodePath, false, rect);

            // Context menu for version bump only on originally selected roots (business rule)
            if (node.WasOriginallySelected)
            {
                ContextMenu menu = new ContextMenu();
                string version = node.Project.Version ?? "0.0.0.0";

                if (node.Project.IsExcludedFromVersionUpdates)
                {
                    menu.Items.Add(new MenuItem
                    {
                        Header = "This project is excluded from the update path (version selection optional)",
                        IsEnabled = false,
                        FontStyle = FontStyles.Italic
                    });
                    menu.Items.Add(new Separator());
                }

                // Order: Major, Minor, Patch, (Revision if four-part)
                menu.Items.Add(CreateVersionMenuItem("Major", version, 0, node));
                menu.Items.Add(CreateVersionMenuItem("Minor", version, 1, node));
                menu.Items.Add(CreateVersionMenuItem("Patch", version, 2, node));
                if (UserSettings.ActiveVersioningMode == VersioningMode.FourPart)
                    menu.Items.Add(CreateVersionMenuItem("Revision", version, 3, node));

                rect.ContextMenu = menu;
            }

            // Tooltip summarizing reference changes (NuGet / project references)
            string tooltip = BuildReferenceChangeTooltip(node.Project.ReferenceChanges);
            if (!string.IsNullOrWhiteSpace(tooltip))
            {
                ToolTipService.SetToolTip(rect, tooltip);
                ToolTipService.SetInitialShowDelay(rect, 0);
            }

            // Local function to trim trailing .0 in 3-part mode (display only)
            string formatVersion(string v)
            {
                if (string.IsNullOrWhiteSpace(v)) return v;
                if (UserSettings.ActiveVersioningMode == VersioningMode.ThreePart)
                {
                    string[] segs = v.Split('.');
                    if (segs.Length == 4 && segs[3] == "0")
                        return string.Join(".", segs[0], segs[1], segs[2]);
                }

                return v;
            }

            string displayCurrent = formatVersion(node.Project.Version);
            string versionText = !string.IsNullOrEmpty(node.NewVersion)
                ? $"V {displayCurrent} -> V {formatVersion(node.NewVersion)}"
                : $"V {displayCurrent}";

            // Compose text lines: project name (wrapped) + version info
            List<string> nameLines = SplitProjectName(node.Project.Name, 3, Theme.NodeWidth, Theme.FontFamily, Theme.FontSize);
            List<(string Text, double Size, FontWeight Weight)> lines = new List<(string, double, FontWeight)>();
            foreach (string l in nameLines)
                lines.Add((l, Theme.FontSize, Theme.ProjectNameFontWeight));
            lines.Add((versionText, Theme.FontSize - 2, Theme.VersionFontWeight));

            double totalHeight = lines.Sum(l => l.Size + 2) - 2;
            double textY = y + (Theme.NodeHeight - totalHeight) / 2;

            foreach ((string Text, double Size, FontWeight Weight) in lines)
            {
                TextBlock tb = new TextBlock
                {
                    Text = Text,
                    Foreground = Theme.TextBrush,
                    FontFamily = new FontFamily(Theme.FontFamily),
                    FontSize = Size,
                    FontWeight = Weight,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Width = Theme.NodeWidth - 20,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(tb, x + 10);
                Canvas.SetTop(tb, textY);
                Panel.SetZIndex(tb, 1);
                canvas.Children.Add(tb);
                textY += Size + 2;
            }

            // Supplemental adornments
            DrawGitBadge(canvas, node, x, y);
            DrawRootBadge(canvas, node, x, y);
        }

        /// <summary>
        /// Renders the Git badge (or expandable pill) that shows changed file & line counts.
        /// Hovering over the badge expands it; we also artificially trigger node hover to keep highlighting consistent.
        /// </summary>
        private void DrawGitBadge(Canvas canvas, ReferrerChainNode node, double x, double y)
        {
            int fileCount = node.Project.GitChangedFileCount;
            int lineCount = node.Project.GitChangedLineCount;
            int total = fileCount + lineCount;
            if (fileCount == 0 && lineCount == 0) return; // Nothing to display

            string totalText = total > 99 ? "99+" : total.ToString();
            string fileText = fileCount > 99 ? "99+" : fileCount.ToString();
            string lineText = lineCount > 99 ? "99+" : lineCount.ToString();

            double badgeDiam = 32;
            double badgeX = x + Theme.NodeWidth - badgeDiam * 0.7;
            double badgeY = y - badgeDiam * 0.3;

            Ellipse badge = new Ellipse
            {
                Width = badgeDiam,
                Height = badgeDiam,
                Fill = Theme.BadgeBackgroundBrush,
                Stroke = Theme.BadgeBorderBrush,
                StrokeThickness = 2,
                Effect = new DropShadowEffect
                {
                    Color = Theme.ShadowColor,
                    BlurRadius = 6,
                    ShadowDepth = 1,
                    Opacity = 0.18
                },
                IsHitTestVisible = true
            };
            Canvas.SetLeft(badge, badgeX);
            Canvas.SetTop(badge, badgeY);
            Panel.SetZIndex(badge, 2);
            canvas.Children.Add(badge);

            // Compact circle when not hovered
            Grid badgeGrid = new Grid
            {
                Width = badgeDiam,
                Height = badgeDiam,
                IsHitTestVisible = false
            };
            badgeGrid.Children.Add(new TextBlock
            {
                Text = totalText,
                Foreground = Theme.BadgeForegroundBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            Canvas.SetLeft(badgeGrid, badgeX);
            Canvas.SetTop(badgeGrid, badgeY);
            Panel.SetZIndex(badgeGrid, 3);
            canvas.Children.Add(badgeGrid);

            // Expanded pill (files | lines)
            double pillWidth = badgeDiam * 2.1;
            double pillHeight = badgeDiam;
            double pillX = badgeX;
            double pillY = badgeY;

            Border pill = new Border
            {
                Width = pillWidth,
                Height = pillHeight,
                CornerRadius = new CornerRadius(pillHeight / 2),
                Background = Theme.BadgeBackgroundBrush,
                BorderBrush = Theme.BadgeBorderBrush,
                BorderThickness = new Thickness(2),
                Effect = new DropShadowEffect
                {
                    Color = Theme.ShadowColor,
                    BlurRadius = 6,
                    ShadowDepth = 1,
                    Opacity = 0.18
                },
                Visibility = Visibility.Hidden,
                IsHitTestVisible = true
            };
            Canvas.SetLeft(pill, pillX);
            Canvas.SetTop(pill, pillY);
            Panel.SetZIndex(pill, 4);
            canvas.Children.Add(pill);

            Grid pillGrid = new Grid
            {
                Width = pillWidth,
                Height = pillHeight,
                Visibility = Visibility.Hidden,
                IsHitTestVisible = false
            };
            pillGrid.ColumnDefinitions.Add(new ColumnDefinition());
            pillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            pillGrid.ColumnDefinitions.Add(new ColumnDefinition());

            pillGrid.Children.Add(new TextBlock
            {
                Text = fileText,
                Foreground = Theme.BadgeForegroundBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            Border sep = new Border
            {
                Width = 2,
                Height = pillHeight * 0.6,
                Background = Theme.SeparatorBrush,
                CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            Grid.SetColumn(sep, 1);
            pillGrid.Children.Add(sep);

            TextBlock lineLbl = new TextBlock
            {
                Text = lineText,
                Foreground = Theme.BadgeForegroundBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(lineLbl, 2);
            pillGrid.Children.Add(lineLbl);

            Canvas.SetLeft(pillGrid, pillX);
            Canvas.SetTop(pillGrid, pillY);
            Panel.SetZIndex(pillGrid, 5);
            canvas.Children.Add(pillGrid);

            // Mirror node hover highlight state when badge expands / collapses
            void triggerNodeHover(bool enter)
            {
                foreach (object child in canvas.Children)
                {
                    if (child is Rectangle r && r.Tag is ReferrerChainNodeTag tag && tag.ProjectName == node.Project.Name)
                    {
                        r.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0)
                        {
                            RoutedEvent = enter ? UIElement.MouseEnterEvent : UIElement.MouseLeaveEvent
                        });
                    }
                }
            }

            badge.MouseEnter += (s, e) =>
            {
                badge.Visibility = Visibility.Hidden;
                badgeGrid.Visibility = Visibility.Hidden;
                pill.Visibility = Visibility.Visible;
                pillGrid.Visibility = Visibility.Visible;
                triggerNodeHover(true);
            };

            pill.MouseLeave += (s, e) =>
            {
                badge.Visibility = Visibility.Visible;
                badgeGrid.Visibility = Visibility.Visible;
                pill.Visibility = Visibility.Hidden;
                pillGrid.Visibility = Visibility.Hidden;
                triggerNodeHover(false);
            };

            pill.MouseEnter += (s, e) => triggerNodeHover(true);
        }

        /// <summary>
        /// Draws the root badge ("R") in the top-left of nodes that were originally selected for the update path.
        /// Border color conveys version selection state or exclusion.
        /// </summary>
        private void DrawRootBadge(Canvas canvas, ReferrerChainNode node, double x, double y)
        {
            if (!node.WasOriginallySelected) return;

            double diameter = 24;
            double badgeX = x - diameter * 0.3;
            double badgeY = y - diameter * 0.3;

            Brush rootBorderBrush = node.Project.IsExcludedFromVersionUpdates
                ? Theme.RootBadgeExcludedBorderBrush
                : !string.IsNullOrEmpty(node.NewVersion)
                    ? Theme.RootBadgeVersionSelectedBorderBrush
                    : Theme.RootBadgeRequiresVersionBorderBrush;

            Ellipse badge = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = Theme.RootNodeBorderBrush,
                Stroke = rootBorderBrush,
                StrokeThickness = 2,
                Effect = new DropShadowEffect
                {
                    Color = Theme.ShadowColor,
                    BlurRadius = 6,
                    ShadowDepth = 1,
                    Opacity = 0.18
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(badge, badgeX);
            Canvas.SetTop(badge, badgeY);
            Panel.SetZIndex(badge, 2);
            canvas.Children.Add(badge);

            Grid grid = new Grid { Width = diameter, Height = diameter, IsHitTestVisible = false };
            grid.Children.Add(new TextBlock
            {
                Text = "R",
                Foreground = Theme.RootBadgeTextBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            Canvas.SetLeft(grid, badgeX);
            Canvas.SetTop(grid, badgeY);
            Panel.SetZIndex(grid, 3);
            canvas.Children.Add(grid);
        }

        /// <summary>
        /// Central hover/highlight logic: emphasizes hovered node, every duplicate instance (by project name),
        /// and the chain of edges + arrowheads from node up to root via stored path mappings.
        /// </summary>
        private void HighlightNodeAndPath(Canvas canvas, ReferrerChainNode node, string nodePath, bool enter, Rectangle rect)
        {
            rect.Stroke = enter ? Theme.HoverBorderBrush : (node.IsRoot ? Theme.RootNodeBorderBrush : Theme.NodeBorderBrush);
            if (string.IsNullOrEmpty(nodePath) || _nodePaths == null) return;

            // Highlight all node rectangles referencing same project
            foreach (object child in canvas.Children)
            {
                if (child is Rectangle r && r.Tag is ReferrerChainNodeTag tag && tag.ProjectName == node.Project.Name)
                {
                    r.Stroke = enter ? Theme.HoverBorderBrush : (node.IsRoot ? Theme.RootNodeBorderBrush : Theme.NodeBorderBrush);
                }
            }

            // Walk ancestry path (node -> root) using unique path mapping
            ReferrerChainNode current = node;
            string currentPath = nodePath;
            while (true)
            {
                ReferrerChainNode parent = FindParentNode(current, _lastRoots);
                if (parent == null || !_nodePaths.TryGetValue(parent, out string parentPath))
                    break;

                foreach (object child in canvas.Children)
                {
                    if (child is Line line && line.Tag is ReferrerChainEdgeTag edge && edge.ParentPath == parentPath && edge.ChildPath == currentPath)
                    {
                        line.Stroke = enter ? Theme.HoverBorderBrush : Theme.ArrowBrush;
                        Panel.SetZIndex(line, enter ? 1000 : 0);
                    }

                    if (child is Polygon polygon && polygon.Tag is ReferrerChainEdgeTag edge2 && edge2.ParentPath == parentPath && edge2.ChildPath == currentPath)
                    {
                        polygon.Fill = enter ? Theme.HoverBorderBrush : Theme.ArrowBrush;
                        Panel.SetZIndex(polygon, enter ? 1000 : 0);
                    }
                }

                current = parent;
                currentPath = parentPath;
            }
        }

        /// <summary>
        /// Creates a context menu item for a specific version segment increment (0..3).
        /// Skips creation for revision (3) when in three-part mode.
        /// </summary>
        private MenuItem CreateVersionMenuItem(string type, string version, int part, ReferrerChainNode node)
        {
            if (UserSettings.ActiveVersioningMode == VersioningMode.ThreePart && part == 3)
                return null;

            string newVersion = IncrementVersion(version, part);
            MenuItem item = new MenuItem { Header = $"{type} {newVersion}" };
            item.Click += (s, e) =>
            {
                if (_lastRoots != null)
                {
                    foreach (ReferrerChainNode n in GetAllNodes(_lastRoots))
                    {
                        if (n.Project == node.Project)
                        {
                            n.NewVersion = newVersion;
                            n.Project.ProjectVersionChange = null; // Clear diff indicator since user explicitly chose new version
                        }
                    }
                }

                // Potentially propagate bumps when all roots decided
                BumpChildRevisionsIfAllRootsSet();
                if (_lastCanvas != null && _lastRoots != null)
                    DrawChains(_lastCanvas, _lastRoots);
            };
            return item;
        }

        /// <summary>
        /// Pure version increment logic supporting 3-part or 4-part configuration.
        /// Resets lower-order segments after increment, consistent with semantic version rules.
        /// </summary>
        private string IncrementVersion(string version, int part)
        {
            bool threePart = UserSettings.ActiveVersioningMode == VersioningMode.ThreePart;
            string[] parts = version.Split('.');
            int[] nums = new int[4];
            for (int i = 0; i < 4; i++)
                nums[i] = (i < parts.Length && int.TryParse(parts[i], out int n)) ? n : 0;

            if (threePart && part == 3)
                part = 2; // Revision maps to Patch in three-part mode

            nums[part]++;
            for (int i = part + 1; i < 4; i++)
                nums[i] = 0; // Reset trailing segments

            return threePart
                ? string.Join(".", nums[0], nums[1], nums[2])
                : string.Join(".", nums);
        }

        /// <summary>
        /// Enumerates entire graph of nodes reachable from root collection (DFS, no duplicates).
        /// </summary>
        private IEnumerable<ReferrerChainNode> GetAllNodes(IEnumerable<ReferrerChainNode> roots)
        {
            HashSet<ReferrerChainNode> visited = new HashSet<ReferrerChainNode>();
            Stack<ReferrerChainNode> stack = new Stack<ReferrerChainNode>(roots);
            while (stack.Count > 0)
            {
                ReferrerChainNode n = stack.Pop();
                if (n == null || !visited.Add(n))
                    continue;
                yield return n;
                if (n.Referrers != null)
                {
                    foreach (ReferrerChainNode c in n.Referrers)
                        stack.Push(c);
                }
            }
        }

        /// <inheritdoc />
        public void BumpChildRevisionsIfAllRootsSet(Canvas canvas, List<ReferrerChainNode> roots)
        {
            _lastCanvas = canvas;
            _lastRoots = roots;
            BumpChildRevisionsIfAllRootsSet();
        }

        /// <summary>
        /// If all root nodes (originally selected) have explicit new versions (or are excluded), cascade a bump (Patch/Revision)
        /// to transitive children that have not been explicitly set or excluded yet. Ensures deterministic propagation order.
        /// </summary>
        public void BumpChildRevisionsIfAllRootsSet()
        {
            if (_lastRoots == null) return;

            // Gather originally selected projects (roots only, not just IsRoot flag)
            HashSet<ProjectModel> originally = new HashSet<ProjectModel>();
            foreach (ReferrerChainNode r in _lastRoots)
                CollectOriginallySelectedProjects(r, originally);

            // Verify all originally selected projects have either chosen version or exclusion
            bool allSet = true;
            foreach (ReferrerChainNode r in _lastRoots)
            {
                if (!CheckAllOriginallySelectedHaveVersionsRecursive(r, originally))
                {
                    allSet = false;
                    break;
                }
            }

            if (allSet && originally.Count > 0)
            {
                HashSet<ProjectModel> rootProjects = new HashSet<ProjectModel>(_lastRoots.Select(r => r.Project));
                HashSet<ReferrerChainNode> visited = new HashSet<ReferrerChainNode>(_lastRoots);
                foreach (ReferrerChainNode root in _lastRoots)
                    BumpChildrenRecursive(root, visited, rootProjects, originally);
                AllRootNodesUpdated?.Invoke();
            }
        }

        private bool CheckAllOriginallySelectedHaveVersionsRecursive(ReferrerChainNode node, HashSet<ProjectModel> originally)
        {
            if (originally.Contains(node.Project))
            {
                if (!node.Project.IsExcludedFromVersionUpdates && string.IsNullOrEmpty(node.NewVersion))
                    return false; // Missing explicit selection
            }

            foreach (ReferrerChainNode child in node.Referrers)
            {
                if (!CheckAllOriginallySelectedHaveVersionsRecursive(child, originally))
                    return false;
            }

            return true;
        }

        private void CollectOriginallySelectedProjects(ReferrerChainNode node, HashSet<ProjectModel> set)
        {
            if (node.WasOriginallySelected)
                set.Add(node.Project);
            foreach (ReferrerChainNode c in node.Referrers)
                CollectOriginallySelectedProjects(c, set);
        }

        private void BumpChildrenRecursive(ReferrerChainNode node, HashSet<ReferrerChainNode> visited, HashSet<ProjectModel> rootProjects, HashSet<ProjectModel> originally)
        {
            foreach (ReferrerChainNode child in node.Referrers)
            {
                bool skip = rootProjects.Contains(child.Project) && !originally.Contains(child.Project); // do not overwrite secondary root duplicates
                if (!skip && visited.Add(child))
                {
                    if (!child.Project.IsExcludedFromVersionUpdates && string.IsNullOrEmpty(child.NewVersion))
                    {
                        int targetPart = UserSettings.ActiveVersioningMode == VersioningMode.ThreePart ? 2 : 3; // Patch or Revision depending on mode
                        child.NewVersion = IncrementVersion(child.Project.Version ?? "0.0.0.0", targetPart);
                    }

                    BumpChildrenRecursive(child, visited, rootProjects, originally);
                }
            }
        }

        /// <summary>
        /// Chooses a brush for the node background based on project status & whether we already visited this project in layout.
        /// </summary>
        protected Brush GetNodeBrush(ReferrerChainNode node, bool isRoot, HashSet<ProjectModel> visited)
        {
            if (!isRoot && visited.Contains(node.Project))
                return Theme.VisitedBrush; // Visual de-dupe indicator

            switch (node.Project.Status)
            {
                case ProjectStatus.NuGetOrProjectReferenceAndVersionChanges:
                    return Theme.NugetAndVersionChangeBrush;
                case ProjectStatus.IsVersionChangeOnly:
                    return Theme.VersionOnlyBrush;
                case ProjectStatus.NuGetOrProjectReferenceChanges:
                    return Theme.NugetOrProjectChangeBrush;
                case ProjectStatus.Modified:
                    return Theme.ModifiedBrush;
                case ProjectStatus.Clean:
                    return Theme.CleanBrush;
                default:
                    return Theme.CleanBrush;
            }
        }

        /// <summary>
        /// Splits a long project name into multiple lines (maxRows) by heuristic measuring,
        /// attempting to break early at '.' where possible; avoids expensive full text measurement loops.
        /// </summary>
        protected List<string> SplitProjectName(string name, int maxRows, double maxWidth, string fontFamily, double fontSize)
        {
            List<string> lines = new List<string>();
            string remaining = name;
            for (int i = 0; i < maxRows - 1 && remaining.Length > 0; i++)
            {
                int split = FindSplitIndex(remaining, maxWidth, fontFamily, fontSize);
                if (split <= 0 || split >= remaining.Length)
                    break;
                lines.Add(remaining.Substring(0, split));
                remaining = remaining.Substring(split);
            }

            if (remaining.Length > 0)
                lines.Add(remaining);
            return lines;
        }

        /// <summary>
        /// Finds a reasonable wrap index for the provided text within width constraint.
        /// Prioritizes '.' early; falls back to measuring incremental substrings.
        /// </summary>
        protected int FindSplitIndex(string text, double maxWidth, string fontFamily, double fontSize)
        {
            FormattedText formatted = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily),
                fontSize,
                Theme.FormattedTextBrush,
                new NumberSubstitution(),
                1.0);

            if (formatted.Width <= maxWidth - 20)
                return text.Length;

            int lastDot = text.LastIndexOf('.', Math.Min(text.Length - 1, 30));
            if (lastDot > 0)
                return lastDot + 1; // include the dot so next line starts cleanly

            for (int i = 1; i < text.Length; i++)
            {
                string sub = text.Substring(0, i);
                FormattedText subFormatted = new FormattedText(
                    sub,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily),
                    fontSize,
                    Theme.FormattedTextBrush,
                    new NumberSubstitution(),
                    1.0);
                if (subFormatted.Width > maxWidth - 20)
                    return i - 1;
            }

            return text.Length;
        }

        /// <summary>
        /// Builds a multiline tooltip summarizing added / removed / changed NuGet and project references.
        /// Returns null if there are no changes.
        /// </summary>
        private string BuildReferenceChangeTooltip(List<ReferenceChange> changes)
        {
            if (changes == null || changes.Count == 0)
                return null;

            List<string> lines = new List<string>();
            List<ReferenceChange> nuget = changes.Where(c => c.IsNuGet).ToList();
            List<ReferenceChange> proj = changes.Where(c => c.IsProjectReference).ToList();

            if (nuget.Count > 0)
            {
                lines.Add("NuGet Packages:");
                foreach (ReferenceChange c in nuget)
                {
                    string action = c.ChangeType == ReferenceChangeType.Added
                        ? "Added" : c.ChangeType == ReferenceChangeType.Removed
                        ? "Removed" : "Changed";
                    string version = c.ChangeType == ReferenceChangeType.Edited
                        ? $"{c.OldVersion} → {c.NewVersion}"
                        : c.NewVersion ?? c.OldVersion;
                    lines.Add($"  {action}: {c.Name}{(string.IsNullOrEmpty(version) ? string.Empty : " (" + version + ")")}");
                }
            }

            if (proj.Count > 0)
            {
                if (nuget.Count > 0)
                    lines.Add(string.Empty);
                lines.Add("Project References:");
                foreach (ReferenceChange c in proj)
                {
                    string action = c.ChangeType == ReferenceChangeType.Added
                        ? "Added" : c.ChangeType == ReferenceChangeType.Removed
                        ? "Removed" : "Changed";
                    lines.Add($"  {action}: {c.Name}");
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Assigns unique hierarchical path identifiers (e.g. 1.2.1) to each node for reliable edge target lookup.
        /// </summary>
        protected void AssignPaths(ReferrerChainNode node, string path, Dictionary<ReferrerChainNode, string> nodePaths)
        {
            nodePaths[node] = path;
            int childIndex = 1;
            if (node.Referrers != null)
            {
                foreach (ReferrerChainNode child in node.Referrers)
                {
                    AssignPaths(child, path + "." + childIndex, nodePaths);
                    childIndex++;
                }
            }
        }

        /// <summary>
        /// Depth-first layout enumerator generating depth/row assignments while preventing infinite recursion on cycles.
        /// </summary>
        protected void LayoutTree(ReferrerChainNode node, int depth, ref int currentRow, List<NodeLayout> layouts, HashSet<ProjectModel> pathVisited)
        {
            if (pathVisited.Contains(node.Project))
                return; // Avoid infinite loops caused by circular references (should not normally happen)

            HashSet<ProjectModel> newVisited = new HashSet<ProjectModel>(pathVisited) { node.Project };
            int myRow = currentRow++;
            layouts.Add(new NodeLayout { Node = node, Depth = depth, Row = myRow });
            foreach (ReferrerChainNode child in node.Referrers)
                LayoutTree(child, depth + 1, ref currentRow, layouts, newVisited);
        }

        /// <summary>
        /// Draws an edge (and optional arrowhead if mostly horizontal) between nodes, tagging for hover relation highlighting.
        /// </summary>
        protected void DrawLine(Canvas canvas, double x1, double y1, double x2, double y2,
            string parentName = null, string childName = null, string parentPath = null, string childPath = null)
        {
            ReferrerChainEdgeTag tagObj = null;
            if (parentPath != null && childPath != null && parentName != null && childName != null)
            {
                tagObj = new ReferrerChainEdgeTag
                {
                    ParentPath = parentPath,
                    ChildPath = childPath,
                    ParentProjectName = parentName,
                    ChildProjectName = childName
                };
            }

            Line line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = Theme.ArrowBrush,
                StrokeThickness = 2.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.8,
                IsHitTestVisible = false,
                Tag = tagObj
            };
            canvas.Children.Add(line);

            // Add arrowhead only for near-horizontal segments (aesthetics & duplication avoidance)
            if (Math.Abs(y2 - y1) < 1.0)
            {
                double arrowLength = 12;
                double arrowWidth = 7;
                double dx = x2 - x1;
                double dy = y2 - y1;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.1)
                {
                    double ux = dx / len;
                    double uy = dy / len;
                    double ax = x2 - arrowLength * ux;
                    double ay = y2 - arrowLength * uy;
                    double perpX = -uy;
                    double perpY = ux;
                    Point p1 = new Point(x2, y2);
                    Point p2 = new Point(ax + arrowWidth * perpX, ay + arrowWidth * perpY);
                    Point p3 = new Point(ax - arrowWidth * perpX, ay - arrowWidth * perpY);
                    Polygon arrow = new Polygon
                    {
                        Points = new PointCollection { p1, p2, p3 },
                        Fill = Theme.ArrowBrush,
                        Opacity = 0.8,
                        IsHitTestVisible = false,
                        Tag = tagObj
                    };
                    canvas.Children.Add(arrow);
                }
            }
        }

        /// <summary>
        /// Locates parent node (the referencing project) by scanning roots and their descendants.
        /// </summary>
        private ReferrerChainNode FindParentNode(ReferrerChainNode node, List<ReferrerChainNode> roots)
        {
            if (roots == null) return null;
            Stack<ReferrerChainNode> stack = new Stack<ReferrerChainNode>(roots);
            while (stack.Count > 0)
            {
                ReferrerChainNode current = stack.Pop();
                if (current.Referrers != null && current.Referrers.Contains(node))
                    return current;
                if (current.Referrers != null)
                {
                    foreach (ReferrerChainNode c in current.Referrers)
                        stack.Push(c);
                }
            }

            return null;
        }

        /// <summary>Last drawn root collection (used for redraw / hover path lookups).</summary>
        public List<ReferrerChainNode> LastRoots => _lastRoots;

        // ---------------------------- Tag helper classes (attached to WPF shapes) ----------------------------
        public abstract class ReferrerChainTagBase { }
        public class ReferrerChainNodeTag : ReferrerChainTagBase
        {
            public string NodePath { get; set; }
            public string ProjectName { get; set; }
        }
        public class ReferrerChainEdgeTag : ReferrerChainTagBase
        {
            public string ParentPath { get; set; }
            public string ChildPath { get; set; }
            public string ParentProjectName { get; set; }
            public string ChildProjectName { get; set; }
        }
    }
}
