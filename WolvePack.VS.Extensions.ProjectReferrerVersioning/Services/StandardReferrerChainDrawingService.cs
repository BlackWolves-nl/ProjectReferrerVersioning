using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Media;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    public class StandardReferrerChainDrawingService : ReferrerChainDrawingServiceBase
    {

        public override ReferrerChainLayoutMode LayoutMode => ReferrerChainLayoutMode.Standard;
        public StandardReferrerChainDrawingService(ReferrerChainTheme theme) : base(theme) { }

        protected override void DrawChains(Canvas canvas, List<ReferrerChainNode> roots)
        {
            canvas.Children.Clear();
            canvas.Background = Theme.BackgroundBrush;

            List<NodeLayout> layouts = new List<NodeLayout>();
            Dictionary<ReferrerChainNode, string> nodePaths = new Dictionary<ReferrerChainNode, string>();

            if (!HideSubsequentVisits)
            {
                int currentRow = 0;
                int rootIndex = 1;
                foreach (ReferrerChainNode root in roots)
                {
                    AssignPaths(root, rootIndex.ToString(), nodePaths);
                    LayoutTree(root, 0, ref currentRow, layouts, new HashSet<ProjectModel>());
                    currentRow++;
                    rootIndex++;
                }
            }
            else
            {
                // Compact rebuild: prune duplicate project occurrences and reclaim vertical space
                HashSet<ProjectModel> seen = new HashSet<ProjectModel>();
                int currentRow = 0;
                int rootIndex = 1;
                foreach (ReferrerChainNode root in roots)
                {
                    BuildCompact(root, depth: 0, ref currentRow, layouts, seen, path: rootIndex.ToString(), nodePaths);
                    currentRow++; // separation between root groups
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
            double overlapX = 0, overlapY = 0;

            double totalWidth = hSpace + (maxDepth + 1) * (nodeW + hSpace - overlapX);
            double totalHeight = vSpace + (maxRow + 1) * (nodeH + vSpace - overlapY);
            canvas.Width = totalWidth;
            canvas.Height = totalHeight;

            // Draw edges first (only between visible layouts)
            foreach (NodeLayout layout in layouts)
            {
                foreach (ReferrerChainNode child in layout.Node.Referrers)
                {
                    NodeLayout childLayout = layouts.Find(l => l.Node == child);
                    if (childLayout == null) continue; // child pruned in compact mode

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

            HashSet<ProjectModel> visitedProjects = new HashSet<ProjectModel>();
            foreach (NodeLayout layout in layouts)
            {
                double x = hSpace + layout.Depth * (nodeW + hSpace - overlapX);
                double y = vSpace + layout.Row * (nodeH + vSpace - overlapY);
                System.Windows.Media.Brush brush = GetNodeBrush(layout.Node, layout.Depth == 0, visitedProjects);
                DrawNode(canvas, layout.Node, x, y, brush, nodePaths.ContainsKey(layout.Node) ? nodePaths[layout.Node] : null);
                visitedProjects.Add(layout.Node.Project);
            }
        }

        private void BuildCompact(ReferrerChainNode node, int depth, ref int currentRow, List<NodeLayout> layouts, HashSet<ProjectModel> seen, string path, Dictionary<ReferrerChainNode, string> nodePaths)
        {
            if (node == null) return;
            if (seen.Contains(node.Project)) return; // prune duplicate subtree
            seen.Add(node.Project);
            nodePaths[node] = path;
            int rowForThis = currentRow;
            layouts.Add(new NodeLayout { Node = node, Depth = depth, Row = rowForThis });
            currentRow++; // move to next available row for children
            int childIndex = 1;
            foreach (var child in node.Referrers)
            {
                BuildCompact(child, depth + 1, ref currentRow, layouts, seen, path + "." + childIndex, nodePaths);
                childIndex++;
            }
        }
    }
}
