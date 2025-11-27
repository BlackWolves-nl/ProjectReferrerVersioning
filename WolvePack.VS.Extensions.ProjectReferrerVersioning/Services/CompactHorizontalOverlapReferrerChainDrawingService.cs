using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

public class CompactHorizontalOverlapReferrerChainDrawingService(ReferrerChainTheme theme) : ReferrerChainDrawingServiceBase(theme)
{
    public override ReferrerChainLayoutMode LayoutMode => ReferrerChainLayoutMode.CompactVertical;

    protected override void DrawChains(Canvas canvas, List<ReferrerChainNode> roots)
    {
        canvas.Children.Clear();
        canvas.Background = Theme.BackgroundBrush;

        List<NodeLayout> layouts = new();
        Dictionary<ReferrerChainNode, string> nodePaths = new();

        if (!HideSubsequentVisits)
        {
            int currentRowStd = 0;
            int rootIndexStd = 1;
            foreach (ReferrerChainNode root in roots)
            {
                AssignPaths(root, rootIndexStd.ToString(), nodePaths);
                LayoutTree(root, 0, ref currentRowStd, layouts, new HashSet<ProjectModel>());
                currentRowStd++;
                rootIndexStd++;
            }
        }
        else
        {
            // Build a compact layout: skip duplicate project occurrences and their descendants entirely
            HashSet<ProjectModel> seen = new();
            int currentRow = 0;
            int rootIndex = 1;
            foreach (ReferrerChainNode root in roots)
            {
                AssignPaths(root, rootIndex.ToString(), nodePaths); // still assign full paths (hidden nodes just unused)
                BuildCompact(root, 0, ref currentRow, layouts, seen);
                currentRow++; // gap between root groups (optional, keeps visual separation)
                rootIndex++;
            }
        }

        _nodePaths = nodePaths;

        int maxDepth = 0, maxRow = 0;
        foreach (NodeLayout layout in layouts)
        {
            if (layout.Depth > maxDepth) maxDepth = layout.Depth;
            if (layout.Row > maxRow) maxRow = layout.Row;
        }

        double nodeW = Theme.NodeWidth, nodeH = Theme.NodeHeight;
        double hSpace = Theme.HorizontalSpacing, vSpace = Theme.VerticalSpacing;
        double overlapX = nodeW * 0.33, overlapY = 0;

        double totalWidth = hSpace + (maxDepth + 1) * (nodeW + hSpace - overlapX);
        double totalHeight = vSpace + (maxRow + 1) * (nodeH + vSpace - overlapY);
        canvas.Width = totalWidth;
        canvas.Height = totalHeight;

        HashSet<ProjectModel> visitedProjects = new();
        foreach (NodeLayout layout in layouts)
        {
            double x = hSpace + layout.Depth * (nodeW + hSpace - overlapX);
            double y = vSpace + layout.Row * (nodeH + vSpace - overlapY);
            Brush brush = GetNodeBrush(layout.Node, layout.Depth == 0, visitedProjects);
            DrawNode(canvas, layout.Node, x, y, brush, nodePaths.ContainsKey(layout.Node) ? nodePaths[layout.Node] : null);
            visitedProjects.Add(layout.Node.Project);
        }

        // Edges only between nodes that survived compaction (both parent and child in layouts list)
        foreach (NodeLayout layout in layouts)
        {
            foreach (ReferrerChainNode child in layout.Node.Referrers)
            {
                NodeLayout childLayout = layouts.Find(l => l.Node == child);
                if (childLayout == null) continue; // child suppressed
                double parentX = hSpace + layout.Depth * (nodeW + hSpace - overlapX);
                double parentY = vSpace + layout.Row * (nodeH + vSpace - overlapY);
                double childX = hSpace + childLayout.Depth * (nodeW + hSpace - overlapX);
                double childY = vSpace + childLayout.Row * (nodeH + vSpace - overlapY);
                double startX = parentX + nodeW / 2;
                double startY = parentY + nodeH;
                double midX = startX;
                double midY = childY + nodeH / 2;
                double endX = childX;
                double endY = midY;
                string parentPath = nodePaths.ContainsKey(layout.Node) ? nodePaths[layout.Node] : null;
                string childPath = nodePaths.ContainsKey(child) ? nodePaths[child] : null;
                DrawLine(canvas, startX, startY, midX, midY, layout.Node.Project.Name, child.Project.Name, parentPath, childPath);
                DrawLine(canvas, midX, midY, endX, endY, layout.Node.Project.Name, child.Project.Name, parentPath, childPath);
            }
        }
    }

    private void BuildCompact(ReferrerChainNode node, int depth, ref int currentRow, List<NodeLayout> layouts, HashSet<ProjectModel> seen)
    {
        if (node == null) return;
        if (seen.Contains(node.Project)) return; // duplicate, prune entire branch
        seen.Add(node.Project);
        int rowForThis = currentRow;
        layouts.Add(new NodeLayout { Node = node, Depth = depth, Row = rowForThis });
        currentRow++; // advance row so children are placed below parent
        foreach (ReferrerChainNode child in node.Referrers)
            BuildCompact(child, depth + 1, ref currentRow, layouts, seen);
        // no extra currentRow++ here (already advanced before children)
    }
}
