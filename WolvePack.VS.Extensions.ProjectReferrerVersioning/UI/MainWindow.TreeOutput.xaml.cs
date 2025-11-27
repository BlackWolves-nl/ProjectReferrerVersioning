using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI;

public partial class MainWindow
{
    private void LayoutComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(_drawingService == null)
            return; // Drawing service not ready yet

        if(LayoutComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            string layoutType = item.Content.ToString();
            switch(layoutType)
            {
                case "Standard (Tree)":
                    SetDrawingService(new StandardReferrerChainDrawingService(_currentTheme));
                    break;
                case "Compact Horizontal":
                    SetDrawingService(new CompactHorizontalOverlapReferrerChainDrawingService(_currentTheme));
                    break;
                case "Compact Vertical":
                    SetDrawingService(new CompactVerticalOverlapReferrerChainDrawingService(_currentTheme));
                    break;
                default:
                    SetDrawingService(new StandardReferrerChainDrawingService(_currentTheme));
                    break;
            }

            if(_lastGeneratedChains != null)
            {
                if (_drawingService is ReferrerChainDrawingServiceBase baseSvc)
                    baseSvc.HideSubsequentVisits = HideVisitedCheckBox?.IsChecked == true || _userSettings.HideSubsequentVisits;
                _drawingService.DrawChainsBase(ReferrerTreeCanvas, _lastGeneratedChains);
                UpdateTreeStats();
            }
        }
    }

    private void UpdateTreeOutputLegendColors(ReferrerChainTheme theme)
    {
        LegendNugetAndVersionNodeRect.Fill = theme.NugetAndVersionChangeBrush;
        LegendVersionOnlyNodeRect.Fill = theme.VersionOnlyBrush;
        LegendNugetNodeRect.Fill = theme.NugetOrProjectChangeBrush;
        LegendModifiedNodeRect.Fill = theme.ModifiedBrush;
        LegendCleanNodeRect.Fill = theme.CleanBrush;
        LegendVisitedNodeRect.Fill = theme.VisitedBrush;
    }

    /// <summary>
    /// Handles the Generate button click event. Generates the referrer tree for selected projects,
    /// logs the tree structure, bumps child revisions if all roots have a new version set, and draws the tree output.
    /// </summary>
    /// <remarks>
    /// - Tree generation and logging are performed on a background thread to keep the UI responsive.
    /// - BumpChildRevisionsIfAllRootsSet is called before drawing to ensure child revisions are updated if all roots have a new version set.
    /// - The tree is always redrawn after bumping revisions to reflect any changes in the UI.
    /// - UI controls are disabled during generation to prevent user interaction and re-enabled afterwards.
    /// </remarks>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        // Get selected projects for tree generation
        System.Collections.Generic.List<ProjectModel> selectedProjects = _allProjects?.Where(p => p.IsSelected).ToList();
        if (selectedProjects?.Count > 0)
        {
            try
            {
                // Indicate status and disable controls during generation
                StatusTextBlock.Text = "Generating referrer tree...";
                GenerateButton.IsEnabled = false;
                UpdateVersionsButton.IsEnabled = false;

                // Run tree generation and logging on a background thread for performance
                System.Collections.Generic.List<ReferrerChainNode> generatedChains = await System.Threading.Tasks.Task.Run(() =>
                {
                    // Build the referrer chains for selected projects with minimize chain drawing setting
                    System.Collections.Generic.List<ReferrerChainNode> chains = ReferrerChainService.BuildReferrerChains(selectedProjects, _userSettings.MinimizeChainDrawing);
                    // Log the tree structure for debugging and diagnostics
                    foreach (ReferrerChainNode chain in chains)
                    {
                        Helpers.DebugHelper.LogSeparator("Referrer Chain for " + chain.Project.Name);
                        LogReferrerChainNode(chain, 0);
                    }

                    return chains;
                });

                // Store the generated chains for later use
                _lastGeneratedChains = generatedChains;

                // Clear the canvas before drawing
                ReferrerTreeCanvas.Children.Clear();
                // Bump child revisions if all roots have a new version set (important for version propagation)
                _drawingService.BumpChildRevisionsIfAllRootsSet(ReferrerTreeCanvas, _lastGeneratedChains);
                // Draw the tree output (must be after bumping revisions to reflect changes)
                _drawingService.DrawChainsBase(ReferrerTreeCanvas, _lastGeneratedChains);
                UpdateTreeStats();

                // Switch to the tree output tab
                MainTabControl.SelectedItem = TabTreeOutput;
                StatusTextBlock.Text = "Tree generated (" + selectedProjects.Count + " projects)";
            }
            catch (Exception ex)
            {
                // Show error in status and log for diagnostics
                StatusTextBlock.Text = "Error generating tree: " + ex.Message;
                Helpers.DebugHelper.ShowError("Error generating referrer tree: " + ex.Message, "GenerateButton");
            }
            finally
            {
                // Re-enable controls after generation
                GenerateButton.IsEnabled = true;
            }
        }
    }

    private void LogReferrerChainNode(ReferrerChainNode node, int level)
    {
        DebugHelper.Log(new string(' ', level * 2) + node.Project.Name + (string.IsNullOrEmpty(node.Project.Version) ? "0.0.0.0" : $" [v{node.Project.Version}]"), "ReferrerChain");
        foreach (ReferrerChainNode child in node.Referrers)
        {
            LogReferrerChainNode(child, level + 1);
        }
    }

    private void ExportPngButton_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.SaveFileDialog dlg = new()
        {
            Filter = "PNG Image|*.png",
            FileName = "ProjectReferrerTree.png"
        };

        if(dlg.ShowDialog() == true)
        {
            ExportCanvasToPng(ReferrerTreeCanvas, dlg.FileName);
        }
    }

    private void ExportCanvasToPng(Canvas canvas, String fileName)
    {
        if (canvas == null) return;

        // Minimal quality fix strategy:
        // 1. Temporarily remove any LayoutTransform (zoom) so we capture the logical geometry 1:1.
        // 2. Enable layout rounding + pixel snapping to align glyph baselines.
        // 3. Render directly with RenderTargetBitmap.Render(canvas) (avoid VisualBrush indirection).
        // 4. Restore previous state.
        // (Further improvements planned separately – see plan in response.)

        // Store current transform / settings
        Transform originalTransform = canvas.LayoutTransform;
        bool originalSnaps = canvas.SnapsToDevicePixels;
        bool originalLayoutRounding = canvas.UseLayoutRounding;

        try
        {
            // Neutralize zoom transform for crisp baseline geometry
            canvas.LayoutTransform = Transform.Identity;
            canvas.UseLayoutRounding = true;
            canvas.SnapsToDevicePixels = true;

            // Force re-measure/layout
            canvas.UpdateLayout();

            Rect bounds = VisualTreeHelper.GetDescendantBounds(canvas);
            if (bounds.IsEmpty) return;

            // Calculate pixel size (1 DIP == 1 pixel at 96 DPI)
            double width = Math.Ceiling(bounds.Width + (bounds.X < 0 ? -bounds.X : 0));
            double height = Math.Ceiling(bounds.Height + (bounds.Y < 0 ? -bounds.Y : 0));
            if (width <= 0 || height <= 0) return;

            int pixelWidth = (int)width;
            int pixelHeight = (int)height;
            const double dpi = 96.0;

            // Prepare bitmap
            RenderTargetBitmap rtb = new(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);

            // Translate content if there are negative coordinates (rare but possible with some layouts)
            if (bounds.X != 0 || bounds.Y != 0)
            {
                // Use a DrawingVisual wrapper to shift content into positive space exactly once
                DrawingVisual dv = new();
                using (DrawingContext ctx = dv.RenderOpen())
                {
                    ctx.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
                    VisualBrush vb = new(canvas)
                    {
                        Stretch = Stretch.None,
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top,
                        TileMode = TileMode.None
                    };
                    ctx.DrawRectangle(vb, null, new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
                    ctx.Pop();
                }

                rtb.Render(dv);
            }
            else
            {
                // Direct render (fast path, avoids an extra brush pass)
                rtb.Render(canvas);
            }

            // Encode PNG
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = new(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(fs);
            }
        }
        finally
        {
            // Restore original visual state
            canvas.LayoutTransform = originalTransform;
            canvas.SnapsToDevicePixels = originalSnaps;
            canvas.UseLayoutRounding = originalLayoutRounding;
            canvas.UpdateLayout();
        }
    }

    private async void UpdateVersionsButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "Updating project versions...";
        UpdateVersionsButton.IsEnabled = false;
        VersionUpdateResult result = await ReferrerChainService.UpdateVersionsAsync(_lastGeneratedChains, progress => StatusTextBlock.Text = progress);
        StatusTextBlock.Text = "Update complete.";
        // Show custom dialog instead of MessageBox
        Dialogs.VersionUpdateResultWindow dlg = new(result)
        {
            Owner = this
        };
        dlg.ShowDialog();

        // Refresh Version property from .csproj files
        ProjectModel.RefreshVersionsFromCsproj(_allProjects);

        // Rebuild the referrer tree with updated versions
        System.Collections.Generic.List<ProjectModel> selectedProjects = _allProjects?.Where(p => p.IsSelected).ToList();
        if (selectedProjects != null && selectedProjects.Count > 0)
        {
            _lastGeneratedChains = ReferrerChainService.BuildReferrerChains(selectedProjects, _userSettings.MinimizeChainDrawing);
            ReferrerTreeCanvas.Children.Clear();
            _drawingService.DrawChainsBase(ReferrerTreeCanvas, _lastGeneratedChains);
            UpdateTreeStats();
        }
    }

    private void HideVisitedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_drawingService is ReferrerChainDrawingServiceBase baseSvc)
        {
            bool newVal = HideVisitedCheckBox.IsChecked == true;
            baseSvc.HideSubsequentVisits = newVal;
            // No persistence here (session override)
            if (_lastGeneratedChains != null)
            {
                ReferrerTreeCanvas.Children.Clear();
                _drawingService.DrawChainsBase(ReferrerTreeCanvas, _lastGeneratedChains);
                UpdateTreeStats();
            }
        }
    }

    private void ExportSvgButton_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.SaveFileDialog dlg = new()
        {
            Filter = "SVG Image|*.svg",
            FileName = "ProjectReferrerTree.svg"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                Services.SvgExportService.Export(ReferrerTreeCanvas, dlg.FileName);
                StatusTextBlock.Text = "SVG exported.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "SVG export failed.";
                Helpers.DebugHelper.ShowError("SVG export failed: " + ex.Message, "ExportSvgButton");
            }
        }
    }

    private void UpdateTreeStats()
    {
        if (TreeStatsText == null)
            return;
        if (_lastGeneratedChains == null || _lastGeneratedChains.Count == 0)
        {
            TreeStatsText.Text = "No tree generated";
            return;
        }
        // Count roots
        int rootCount = _lastGeneratedChains.Count;
        // Gather all nodes (instances) and unique projects from the current chains
        var visitedNodeInstances = new System.Collections.Generic.HashSet<ReferrerChainNode>();
        var uniqueProjects = new System.Collections.Generic.HashSet<ProjectModel>();
        void Traverse(ReferrerChainNode n)
        {
            if (n == null || !visitedNodeInstances.Add(n)) return;
            if (n.Project != null) uniqueProjects.Add(n.Project);
            foreach (var c in n.Referrers) Traverse(c);
        }

        foreach (var r in _lastGeneratedChains) Traverse(r);
        int totalGraphNodes = visitedNodeInstances.Count;
        int uniqueProjectCount = uniqueProjects.Count;
        // Count drawn rectangles (WPF nodes) & edges currently on canvas
        int drawnNodeRects = 0; int drawnEdges = 0;
        foreach (var child in ReferrerTreeCanvas.Children)
        {
            if (child is System.Windows.Shapes.Rectangle rect && rect.Tag is ReferrerChainDrawingServiceBase.ReferrerChainNodeTag)
                drawnNodeRects++;
            else if (child is System.Windows.Shapes.Line line && line.Tag is ReferrerChainDrawingServiceBase.ReferrerChainEdgeTag)
                drawnEdges++;
        }
        // If some nodes suppressed (Hide Subsequent Visits), drawnNodeRects < totalGraphNodes
        int suppressed = totalGraphNodes - drawnNodeRects;
        string suppressedPart = suppressed > 0 ? $"  Suppressed: {suppressed}" : string.Empty;
        TreeStatsText.Text = $"Roots: {rootCount}  Nodes Drawn: {drawnNodeRects}  Unique Projects: {uniqueProjectCount}  Total Graph Nodes: {totalGraphNodes}{suppressedPart}  Edges: {drawnEdges}";
    }
}
