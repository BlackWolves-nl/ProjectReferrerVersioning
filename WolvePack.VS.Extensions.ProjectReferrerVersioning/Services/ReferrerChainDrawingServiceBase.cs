using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    public abstract class ReferrerChainDrawingServiceBase : IReferrerChainDrawingService
    {
        public ReferrerChainTheme Theme { get; set; }
        public abstract ReferrerChainLayoutMode LayoutMode { get; }
        public ReferrerChainDrawingServiceBase(ReferrerChainTheme theme) { Theme = theme; }
        protected List<ReferrerChainNode> _lastRoots;
        protected Canvas _lastCanvas;
        public event Action AllRootNodesUpdated;

        protected class NodeLayout
        {
            public ReferrerChainNode Node;
            public int Depth;
            public int Row;
        }

        protected Dictionary<ReferrerChainNode, string> _nodePaths;

        public void DrawChainsBase(Canvas canvas, List<ReferrerChainNode> roots)
        {
            _lastCanvas = canvas;
            _lastRoots = roots;
            DrawChains(canvas, roots);
        }
        protected abstract void DrawChains(Canvas canvas, List<ReferrerChainNode> roots);

        // Remove expanded projects tracking (no sticky expand/collapse)

        // Add nodePath parameter for unique edge highlighting
        protected void DrawNode(Canvas canvas, ReferrerChainNode node, double x, double y, Brush fill, string nodePath = null)
        {
            // Subtle dark gradient for node background
            Color baseColor = ((SolidColorBrush)fill).Color;
            Color gradColor = Color.Multiply(baseColor, 1.15f); // Slightly lighter, not white
            LinearGradientBrush gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            gradient.GradientStops.Add(new GradientStop(baseColor, 0));
            gradient.GradientStops.Add(new GradientStop(gradColor, 1));

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
                    Color = Colors.Black,
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.18
                },
                IsHitTestVisible = true,
                Tag = new ReferrerChainNodeTag { NodePath = nodePath, ProjectName = node.Project.Name }
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);

            // Hover effect: border color for hovered node, and highlight all edges to root only for this node
            rect.MouseEnter += (s, e) =>
            {
                rect.Stroke = Theme.HoverBorderBrush;
                if (!string.IsNullOrEmpty(nodePath) && _nodePaths != null)
                {
                    // Highlight all nodes with the same ProjectName
                    foreach (object child in canvas.Children)
                    {
                        if (child is Rectangle r && r.Tag is ReferrerChainNodeTag nodeTag && nodeTag.ProjectName == node.Project.Name)
                        {
                            r.Stroke = Theme.HoverBorderBrush;
                            //Panel.SetZIndex(r, 1000);
                        }
                    }

                    string currentPath = nodePath;
                    ReferrerChainNode current = node;
                    while (true)
                    {
                        ReferrerChainNode parent = FindParentNode(current, _lastRoots);
                        if (parent == null || !_nodePaths.TryGetValue(parent, out string parentPath)) break;
                        foreach (object child in canvas.Children)
                        {
                            if (child is Line l && l.Tag is ReferrerChainEdgeTag edgeTag && edgeTag.ParentPath == parentPath && edgeTag.ChildPath == currentPath)
                            {
                                l.Stroke = Theme.HoverBorderBrush;
                                Panel.SetZIndex(l, 1000);
                            }

                            if (child is Polygon p && p.Tag is ReferrerChainEdgeTag edgeTag2 && edgeTag2.ParentPath == parentPath && edgeTag2.ChildPath == currentPath)
                            {
                                p.Fill = Theme.HoverBorderBrush;
                                Panel.SetZIndex(p, 1000);
                            }
                        }

                        current = parent;
                        currentPath = parentPath;
                    }
                }
            };
            rect.MouseLeave += (s, e) =>
            {
                rect.Stroke = node.IsRoot ? Theme.RootNodeBorderBrush : Theme.NodeBorderBrush;
                if (!string.IsNullOrEmpty(nodePath) && _nodePaths != null)
                {
                    // Reset all nodes with the same ProjectName
                    foreach (object child in canvas.Children)
                    {
                        if (child is Rectangle r && r.Tag is ReferrerChainNodeTag nodeTag && nodeTag.ProjectName == node.Project.Name)
                        {
                            r.Stroke = r.Tag is ReferrerChainNodeTag tag && node.IsRoot ? Theme.RootNodeBorderBrush : Theme.NodeBorderBrush;
                            //Panel.SetZIndex(r, 0);
                        }
                    }

                    string currentPath = nodePath;
                    ReferrerChainNode current = node;
                    while (true)
                    {
                        ReferrerChainNode parent = FindParentNode(current, _lastRoots);
                        if (parent == null || !_nodePaths.TryGetValue(parent, out string parentPath)) break;
                        foreach (object child in canvas.Children)
                        {
                            if (child is Line l && l.Tag is ReferrerChainEdgeTag edgeTag && edgeTag.ParentPath == parentPath && edgeTag.ChildPath == currentPath)
                            {
                                l.Stroke = Theme.ArrowBrush;
                                Panel.SetZIndex(l, 0);
                            }

                            if (child is Polygon p && p.Tag is ReferrerChainEdgeTag edgeTag2 && edgeTag2.ParentPath == parentPath && edgeTag2.ChildPath == currentPath)
                            {
                                p.Fill = Theme.ArrowBrush;
                                Panel.SetZIndex(p, 0);
                            }
                        }

                        current = parent;
                        currentPath = parentPath;
                    }
                }
            };

            // Attach context menu for originally selected projects (all root projects should have version context menu)
            if (node.WasOriginallySelected)
            {
                ContextMenu menu = new ContextMenu();
                menu.Items.Add(CreateVersionMenuItem("Major", node.Project.Version ?? "0.0.0.0", 0, node));
                menu.Items.Add(CreateVersionMenuItem("Minor", node.Project.Version ?? "0.0.0.0", 1, node));
                menu.Items.Add(CreateVersionMenuItem("Patch", node.Project.Version ?? "0.0.0.0", 2, node));
                menu.Items.Add(CreateVersionMenuItem("Revision", node.Project.Version ?? "0.0.0.0", 3, node));
                rect.ContextMenu = menu;
            }

            // Tooltip for changed/removed NuGet/project references
            string tooltip = BuildReferenceChangeTooltip(node.Project.ReferenceChanges);
            if (!string.IsNullOrWhiteSpace(tooltip))
            {
                ToolTipService.SetToolTip(rect, tooltip);
                ToolTipService.SetInitialShowDelay(rect, 0);
            }

            // Always show new version if set
            string versionText;
            if (!string.IsNullOrEmpty(node.NewVersion))
                versionText = $"V {node.Project.Version} -> V {node.NewVersion}";
            else
                versionText = $"V {node.Project.Version}";

            List<string> nameLines = SplitProjectName(node.Project.Name, 3, Theme.NodeWidth, Theme.FontFamily, Theme.FontSize);
            List<(string text, double fontSize, FontWeight fontWeight)> allLines = new List<(string text, double fontSize, FontWeight fontWeight)>();
            // Bold project name for contrast
            foreach (string line in nameLines)
                allLines.Add((line, Theme.FontSize, Theme.ProjectNameFontWeight));
            allLines.Add((versionText, Theme.FontSize - 2, Theme.VersionFontWeight));

            double totalTextHeight = 0;
            for (int i = 0; i < allLines.Count; i++)
                totalTextHeight += allLines[i].fontSize + 2;
            totalTextHeight -= 2;
            double textY = y + (Theme.NodeHeight - totalTextHeight) / 2;

            for (int i = 0; i < allLines.Count; i++)
            {
                (string text, double fontSize, FontWeight fontWeight) = allLines[i];
                TextBlock tb = new TextBlock
                {
                    Text = text,
                    Foreground = Theme.TextBrush,
                    FontFamily = new FontFamily(Theme.FontFamily),
                    FontSize = fontSize,
                    FontWeight = fontWeight,
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
                textY += fontSize + 2;
            }

            // Draw badge for number of changes (top right corner)
            //int changeCount = node.Project.ReferenceChanges != null ? node.Project.ReferenceChanges.Count : 0;
            //if (changeCount > 0)
            //{
            //    string badgeText = changeCount > 99 ? "99+" : changeCount.ToString();
            //    double badgeDiameter = 26;
            //    double badgeX = x + Theme.NodeWidth - badgeDiameter * 0.7;
            //    double badgeY = y - badgeDiameter * 0.3;

            //    Ellipse badge = new Ellipse
            //    {
            //        Width = badgeDiameter,
            //        Height = badgeDiameter,
            //        Fill = Theme.BadgeBackgroundBrush,
            //        Stroke = Brushes.White,
            //        StrokeThickness = 2,
            //        Effect = new DropShadowEffect
            //        {
            //            Color = Colors.Black,
            //            BlurRadius = 6,
            //            ShadowDepth = 1,
            //            Opacity = 0.18
            //        },
            //        IsHitTestVisible = false
            //    };
            //    Canvas.SetLeft(badge, badgeX);
            //    Canvas.SetTop(badge, badgeY);
            //    Panel.SetZIndex(badge, 2);
            //    canvas.Children.Add(badge);

            //    TextBlock badgeLabel = new TextBlock
            //    {
            //        Text = badgeText,
            //        Foreground = Theme.BadgeForegroundBrush,
            //        FontFamily = new FontFamily(Theme.FontFamily),
            //        FontSize = 13,
            //        FontWeight = FontWeights.Bold,
            //        TextAlignment = TextAlignment.Center,
            //        HorizontalAlignment = HorizontalAlignment.Center,
            //        VerticalAlignment = VerticalAlignment.Center,
            //        Width = badgeDiameter,
            //        Height = badgeDiameter,
            //        IsHitTestVisible = false
            //    };
            //    Canvas.SetLeft(badgeLabel, badgeX);
            //    Canvas.SetTop(badgeLabel, badgeY + 2);
            //    Panel.SetZIndex(badgeLabel, 3);
            //    canvas.Children.Add(badgeLabel);
            //}

            // Draw badge for number of git changes (top right corner)
            int fileCount = node.Project.GitChangedFileCount;
            int lineCount = node.Project.GitChangedLineCount;
            int totalCount = fileCount + lineCount;
            if (fileCount > 0 || lineCount > 0)
            {
                string totalText = totalCount > 99 ? "99+" : totalCount.ToString();
                string fileText = fileCount > 99 ? "99+" : fileCount.ToString();
                string lineText = lineCount > 99 ? "99+" : lineCount.ToString();
                double badgeDiameter = 32;
                double badgeX = x + Theme.NodeWidth - badgeDiameter * 0.7;
                double badgeY = y - badgeDiameter * 0.3;

                // Draw the circle badge (default)
                Ellipse badge = new Ellipse
                {
                    Width = badgeDiameter,
                    Height = badgeDiameter,
                    Fill = Theme.BadgeBackgroundBrush,
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 6,
                        ShadowDepth = 1,
                        Opacity = 0.18
                    },
                    IsHitTestVisible = true // for hover
                };
                Canvas.SetLeft(badge, badgeX);
                Canvas.SetTop(badge, badgeY);
                Panel.SetZIndex(badge, 2);
                canvas.Children.Add(badge);

                // Use a Grid to center the text vertically and horizontally
                Grid badgeGrid = new Grid
                {
                    Width = badgeDiameter,
                    Height = badgeDiameter,
                    IsHitTestVisible = false
                };
                TextBlock badgeLabel = new TextBlock
                {
                    Text = totalText,
                    Foreground = Theme.BadgeForegroundBrush,
                    FontFamily = new FontFamily(Theme.FontFamily),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Padding = new Thickness(0),
                    IsHitTestVisible = false
                };
                badgeGrid.Children.Add(badgeLabel);
                Canvas.SetLeft(badgeGrid, badgeX);
                Canvas.SetTop(badgeGrid, badgeY);
                Panel.SetZIndex(badgeGrid, 3);
                canvas.Children.Add(badgeGrid);

                // Expanded pill (hidden by default), expands to the right
                double pillWidth = badgeDiameter * 2.1;
                double pillHeight = badgeDiameter;
                double pillX = badgeX; // left edge matches badge
                double pillY = badgeY;

                Border pill = new Border
                {
                    Width = pillWidth,
                    Height = pillHeight,
                    CornerRadius = new CornerRadius(pillHeight / 2),
                    Background = Theme.BadgeBackgroundBrush,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2),
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
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

                // Pill content: two numbers separated by a vertical bar, centered in their halves
                Grid pillGrid = new Grid
                {
                    Width = pillWidth,
                    Height = pillHeight,
                    Visibility = Visibility.Hidden,
                    IsHitTestVisible = false
                };
                pillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                pillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); // for separator
                pillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                TextBlock fileLabel = new TextBlock
                {
                    Text = fileText,
                    Foreground = Theme.BadgeForegroundBrush,
                    FontFamily = new FontFamily(Theme.FontFamily),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Padding = new Thickness(0),
                    IsHitTestVisible = false
                };
                Grid.SetColumn(fileLabel, 0);
                pillGrid.Children.Add(fileLabel);

                Border separator = new Border
                {
                    Width = 2,
                    Height = pillHeight * 0.6,
                    Background = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CornerRadius = new CornerRadius(1),
                    IsHitTestVisible = false
                };
                Grid.SetColumn(separator, 1);
                pillGrid.Children.Add(separator);

                TextBlock lineLabel = new TextBlock
                {
                    Text = lineText,
                    Foreground = Theme.BadgeForegroundBrush,
                    FontFamily = new FontFamily(Theme.FontFamily),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Padding = new Thickness(0),
                    IsHitTestVisible = false
                };
                Grid.SetColumn(lineLabel, 2);
                pillGrid.Children.Add(lineLabel);

                Canvas.SetLeft(pillGrid, pillX);
                Canvas.SetTop(pillGrid, pillY);
                Panel.SetZIndex(pillGrid, 5);
                canvas.Children.Add(pillGrid);

                // Helper to trigger node hover
                void triggerNodeHover(bool isEnter)
                {
                    // Find the Rectangle for this node and raise MouseEnter/Leave
                    foreach(object child in canvas.Children)
                    {
                        if(child is Rectangle r && r.Tag is ReferrerChainNodeTag tag && tag.ProjectName == node.Project.Name)
                        {
                            if(isEnter)
                                r.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
                            else
                                r.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
                        }
                    }
                }

                // Hover logic
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
                pill.MouseEnter += (s, e) => { triggerNodeHover(true); };
            }

            // Draw root badge for originally selected projects (top left corner)
            if (node.WasOriginallySelected) // Use new property instead of node.Project.IsSelected
            {
                double rootBadgeDiameter = 24;
                double rootBadgeX = x - rootBadgeDiameter * 0.3;
                double rootBadgeY = y - rootBadgeDiameter * 0.3;

                // Change border color based on whether a version has been selected
                Brush rootBorderBrush = !string.IsNullOrEmpty(node.NewVersion) 
                    ? Brushes.LimeGreen  // Green border when version is selected
                    : Brushes.White;     // White border when no version selected

                Ellipse rootBadge = new Ellipse
                {
                    Width = rootBadgeDiameter,
                    Height = rootBadgeDiameter,
                    Fill = Theme.RootNodeBorderBrush, // Use the root border color for consistency
                    Stroke = rootBorderBrush,
                    StrokeThickness = 2,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 6,
                        ShadowDepth = 1,
                        Opacity = 0.18
                    },
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(rootBadge, rootBadgeX);
                Canvas.SetTop(rootBadge, rootBadgeY);
                Panel.SetZIndex(rootBadge, 2);
                canvas.Children.Add(rootBadge);

                // Use a Grid to center the text like other badges
                Grid rootBadgeGrid = new Grid
                {
                    Width = rootBadgeDiameter,
                    Height = rootBadgeDiameter,
                    IsHitTestVisible = false
                };
                TextBlock rootBadgeLabel = new TextBlock
                {
                    Text = "R",
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily(Theme.FontFamily),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Padding = new Thickness(0),
                    IsHitTestVisible = false
                };
                rootBadgeGrid.Children.Add(rootBadgeLabel);
                Canvas.SetLeft(rootBadgeGrid, rootBadgeX);
                Canvas.SetTop(rootBadgeGrid, rootBadgeY);
                Panel.SetZIndex(rootBadgeGrid, 3);
                canvas.Children.Add(rootBadgeGrid);
            }
        }

        private MenuItem CreateVersionMenuItem(String type, String version, int part, ReferrerChainNode node)
        {
            string newVersion = IncrementVersion(version, part);
            MenuItem item = new MenuItem { Header = $"{type} {newVersion}" };
            item.Click += (s, e) =>
            {
                // Update all nodes in all chains with the same Project reference
                if (_lastRoots != null)
                {
                    foreach (ReferrerChainNode n in GetAllNodes(_lastRoots))
                    {
                        if (n.Project == node.Project)
                        {
                            n.NewVersion = newVersion;
                            n.Project.ProjectVersionChange = null; // Clear existing version change
                        }
                    }
                }

                // Check version bump logic with improved originally selected project tracking
                BumpChildRevisionsIfAllRootsSet();
                if(_lastCanvas != null && _lastRoots != null)
                    DrawChains(_lastCanvas, _lastRoots);
            };
            return item;
        }

        // Helper to enumerate all nodes in all chains (avoiding duplicates)
        private IEnumerable<ReferrerChainNode> GetAllNodes(IEnumerable<ReferrerChainNode> roots)
        {
            HashSet<ReferrerChainNode> visited = new HashSet<ReferrerChainNode>();
            Stack<ReferrerChainNode> stack = new Stack<ReferrerChainNode>(roots);
            while (stack.Count > 0)
            {
                ReferrerChainNode node = stack.Pop();
                if (node == null || !visited.Add(node))
                    continue;
                yield return node;
                if (node.Referrers != null)
                {
                    foreach (ReferrerChainNode child in node.Referrers)
                        stack.Push(child);
                }
            }
        }

        public void BumpChildRevisionsIfAllRootsSet(Canvas canvas, List<ReferrerChainNode> roots)
        {
            _lastCanvas = canvas;
            _lastRoots = roots;
            BumpChildRevisionsIfAllRootsSet();
        }

        public void BumpChildRevisionsIfAllRootsSet()
        {
            if(_lastRoots == null)
                return;

            // Collect all originally selected projects from the current chains
            HashSet<ProjectModel> originallySelectedProjects = new HashSet<ProjectModel>();
            foreach(ReferrerChainNode root in _lastRoots)
                CollectOriginallySelectedProjects(root, originallySelectedProjects);

            // Check if ALL originally selected projects have versions set (not just drawn root nodes)
            bool allOriginallySelectedHaveVersions = true;
            foreach(ReferrerChainNode root in _lastRoots)
            {
                if(!CheckAllOriginallySelectedHaveVersionsRecursive(root, originallySelectedProjects))
                {
                    allOriginallySelectedHaveVersions = false;
                    break;
                }
            }

            if(allOriginallySelectedHaveVersions && originallySelectedProjects.Count > 0)
            {
                HashSet<ProjectModel> rootProjects = new HashSet<ProjectModel>(_lastRoots.Select(r => r.Project));
                HashSet<ReferrerChainNode> visited = new HashSet<ReferrerChainNode>(_lastRoots);
                
                foreach(ReferrerChainNode root in _lastRoots)
                    BumpChildrenRecursive(root, visited, rootProjects, originallySelectedProjects);
                AllRootNodesUpdated?.Invoke();
            }
        }

        private bool CheckAllOriginallySelectedHaveVersionsRecursive(ReferrerChainNode node, HashSet<ProjectModel> originallySelectedProjects)
        {
            // If this is an originally selected project, check if it has a version
            if (originallySelectedProjects.Contains(node.Project))
            {
                if (string.IsNullOrEmpty(node.NewVersion))
                    return false; // This originally selected project doesn't have a version yet
            }

            // Check all children recursively
            foreach (ReferrerChainNode child in node.Referrers)
            {
                if (!CheckAllOriginallySelectedHaveVersionsRecursive(child, originallySelectedProjects))
                    return false;
            }

            return true;
        }

        private void CollectOriginallySelectedProjects(ReferrerChainNode node, HashSet<ProjectModel> originallySelectedProjects)
        {
            if (node.WasOriginallySelected) // Use new property instead of node.Project.IsSelected
            {
                originallySelectedProjects.Add(node.Project);
            }

            foreach (ReferrerChainNode child in node.Referrers)
            {
                CollectOriginallySelectedProjects(child, originallySelectedProjects);
            }
        }

        private void BumpChildrenRecursive(ReferrerChainNode node, HashSet<ReferrerChainNode> visited, HashSet<ProjectModel> rootProjects, HashSet<ProjectModel> originallySelectedProjects)
        {
            foreach(ReferrerChainNode child in node.Referrers)
            {
                // Skip if this child is a root in any chain, unless it's an originally selected project
                // that should be able to get version updates even if it's now a child
                bool shouldSkip = rootProjects.Contains(child.Project) && !originallySelectedProjects.Contains(child.Project);
                
                if (!shouldSkip && visited.Add(child))
                {
                    // Originally selected projects get version updates even if they're now children
                    // Other projects only get revision bumps if they're not roots
                    if (!child.Project.IsExcludedFromVersionUpdates && string.IsNullOrEmpty(child.NewVersion))
                    {
                        if (originallySelectedProjects.Contains(child.Project))
                        {
                            // Originally selected projects can get any version update, not just revision
                            // For now, we'll give them revision updates like other children
                            child.NewVersion = IncrementVersion(child.Project.Version ?? "0.0.0.0", 3); // Revision bump
                        }
                        else if (!rootProjects.Contains(child.Project))
                        {
                            // Regular child nodes get revision bumps
                            child.NewVersion = IncrementVersion(child.Project.Version ?? "0.0.0.0", 3); // Revision bump
                        }
                    }

                    BumpChildrenRecursive(child, visited, rootProjects, originallySelectedProjects);
                }
            }
        }

        private string IncrementVersion(string version, int part)
        {
            string[] parts = version.Split('.');
            int[] nums = new int[4];
            for (int i = 0; i < 4; i++)
            {
                if (i < parts.Length && int.TryParse(parts[i], out int n))
                    nums[i] = n;
                else
                    nums[i] = 0;
            }

            nums[part]++;
            for(int i = part + 1; i < 4; i++)
            {
                nums[i] = 0;
            }

            return string.Join(".", nums);
        }

        protected void DrawLine(Canvas canvas, double x1, double y1, double x2, double y2, string parentName = null, string childName = null, string parentPath = null, string childPath = null)
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

            // Only draw arrowhead for horizontal lines
            if (Math.Abs(y2 - y1) < 1.0)
            {
                double arrowLength = 12, arrowWidth = 7;
                double dx = x2 - x1, dy = y2 - y1;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.1)
                {
                    double ux = dx / len, uy = dy / len;
                    double ax = x2 - arrowLength * ux, ay = y2 - arrowLength * uy;
                    double perpX = -uy, perpY = ux;
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

        // Helper to find parent node in the current layout
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
                    foreach (ReferrerChainNode child in current.Referrers)
                        stack.Push(child);
                }
            }

            return null;
        }

        protected Brush GetNodeBrush(ReferrerChainNode node, bool isRoot, HashSet<ProjectModel> visited)
        {
            if (!isRoot && visited.Contains(node.Project))
                return Theme.VisitedBrush;

            // Status-based coloring only
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

        protected List<string> SplitProjectName(string name, int maxRows, double maxWidth, string fontFamily, double fontSize)
        {
            List<string> lines = new List<string>();
            string remaining = name;
            for (int i = 0; i < maxRows - 1 && remaining.Length > 0; i++)
            {
                int split = FindSplitIndex(remaining, maxWidth, fontFamily, fontSize);
                if (split <= 0 || split >= remaining.Length) break;
                lines.Add(remaining.Substring(0, split));
                remaining = remaining.Substring(split);
            }

            if (remaining.Length > 0) lines.Add(remaining);
            return lines;
        }

        protected int FindSplitIndex(string text, double maxWidth, string fontFamily, double fontSize)
        {
            FormattedText formatted = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily),
                fontSize,
                Brushes.Black,
                new NumberSubstitution(),
                1.0);
            if (formatted.Width <= maxWidth - 20) return text.Length;
            int lastDot = text.LastIndexOf('.', Math.Min(text.Length - 1, 30));
            if (lastDot > 0) return lastDot + 1;
            for (int i = 1; i < text.Length; i++)
            {
                string sub = text.Substring(0, i);
                FormattedText subFormatted = new FormattedText(
                    sub,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily),
                    fontSize,
                    Brushes.Black,
                    new NumberSubstitution(),
                    1.0);
                if (subFormatted.Width > maxWidth - 20)
                    return i - 1;
            }

            return text.Length;
        }

        protected NodeLayout FindParent(ReferrerChainNode node, List<NodeLayout> layouts)
        {
            foreach (NodeLayout layout in layouts)
            {
                if (layout.Node.Referrers != null && layout.Node.Referrers.Contains(node))
                    return layout;
            }

            return null;
        }

        private string BuildReferenceChangeTooltip(List<ReferenceChange> changes)
        {
            if(changes == null || changes.Count == 0)
                return null;
            List<string> lines = new List<string>();
            List<ReferenceChange> nuget = changes.Where(c => c.IsNuGet).ToList();
            List<ReferenceChange> proj = changes.Where(c => c.IsProjectReference).ToList();
            if(nuget.Count > 0)
            {
                lines.Add("NuGet Packages:");
                foreach(ReferenceChange c in nuget)
                {
                    string action = c.ChangeType == ReferenceChangeType.Added ? "Added" :
                                    c.ChangeType == ReferenceChangeType.Removed ? "Removed" : "Changed";
                    string version = c.ChangeType == ReferenceChangeType.Edited ? $"{c.OldVersion} → {c.NewVersion}" : c.NewVersion ?? c.OldVersion;
                    lines.Add($"  {action}: {c.Name}{(string.IsNullOrEmpty(version) ? "" : " (" + version + ")")}");
                }
            }

            if(proj.Count > 0)
            {
                if(nuget.Count > 0) lines.Add(""); // Separator
                lines.Add("Project References:");
                foreach(ReferenceChange c in proj)
                {
                    string action = c.ChangeType == ReferenceChangeType.Added ? "Added" :
                                    c.ChangeType == ReferenceChangeType.Removed ? "Removed" : "Changed";
                    lines.Add($"  {action}: {c.Name}");
                }
            }

            return string.Join("\n", lines);
        }

        // Assigns a unique path string to each node (e.g., 1.2.1.3)
        protected void AssignPaths(ReferrerChainNode node, string path, Dictionary<ReferrerChainNode, string> nodePaths)
        {
            nodePaths[node] = path;
            int childIndex = 1;
            if(node.Referrers != null)
            {
                foreach(ReferrerChainNode child in node.Referrers)
                {
                    AssignPaths(child, path + "." + childIndex, nodePaths);
                    childIndex++;
                }
            }
        }

        protected void LayoutTree(ReferrerChainNode node, int depth, ref int currentRow, List<NodeLayout> layouts, HashSet<ProjectModel> pathVisited)
        {
            if(pathVisited.Contains(node.Project)) return;
            HashSet<ProjectModel> newPathVisited = new HashSet<ProjectModel>(pathVisited) { node.Project };

            int myRow = currentRow++;
            layouts.Add(new NodeLayout { Node = node, Depth = depth, Row = myRow });

            foreach(ReferrerChainNode child in node.Referrers)
            {
                LayoutTree(child, depth + 1, ref currentRow, layouts, newPathVisited);
            }
        }

        // Add a public property to expose last roots for redraw
        public List<ReferrerChainNode> LastRoots => _lastRoots;

        // Tag base and derived types for node/edge tagging
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
