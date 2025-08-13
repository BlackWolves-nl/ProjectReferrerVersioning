using System.Collections.Generic;
using System.Windows.Controls;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    public class CompactVerticalOverlapReferrerChainDrawingService : ReferrerChainDrawingServiceBase
    {
        public override ReferrerChainLayoutMode LayoutMode => ReferrerChainLayoutMode.CompactHorizontal;
        public CompactVerticalOverlapReferrerChainDrawingService(ReferrerChainTheme theme) : base(theme) { }

        protected override void DrawChains(Canvas canvas, List<ReferrerChainNode> roots)
        {
            canvas.Children.Clear();
            canvas.Background = Theme.BackgroundBrush;

            List<NodeLayout> layouts = new List<NodeLayout>();
            Dictionary<ReferrerChainNode, string> nodePaths = new Dictionary<ReferrerChainNode, string>();
            int currentRow = 0;
            int rootIndex = 1;
            foreach (ReferrerChainNode root in roots)
            {
                AssignPaths(root, rootIndex.ToString(), nodePaths);
                LayoutTree(root, 0, ref currentRow, layouts, new HashSet<ProjectModel>());
                currentRow++;
                rootIndex++;
            }

            _nodePaths = nodePaths;

            Dictionary<ReferrerChainNode, List<NodeLayout>> parentToChildren = new Dictionary<ReferrerChainNode, List<NodeLayout>>();
            foreach (NodeLayout layout in layouts)
            {
                NodeLayout parentLayout = FindParent(layout.Node, layouts);
                if (parentLayout != null)
                {
                    ReferrerChainNode parentNode = parentLayout.Node;
                    if (!parentToChildren.ContainsKey(parentNode))
                        parentToChildren[parentNode] = new List<NodeLayout>();
                    parentToChildren[parentNode].Add(layout);
                }
            }

            Dictionary<NodeLayout, double> rowExtraOffset = new Dictionary<NodeLayout, double>();
            foreach (List<NodeLayout> siblings in parentToChildren.Values)
            {
                siblings.Sort((a, b) => a.Row.CompareTo(b.Row));
                double offset = 0;
                for (int i = 0; i < siblings.Count; i++)
                {
                    if (i > 0)
                    {
                        offset += 5;
                    }

                    rowExtraOffset[siblings[i]] = offset;
                }
            }

            int maxDepth = 0, maxRow = 0;
            foreach (NodeLayout layout in layouts)
            {
                if (layout.Depth > maxDepth) maxDepth = layout.Depth;
                if (layout.Row > maxRow) maxRow = layout.Row;
            }

            double nodeW = Theme.NodeWidth, nodeH = Theme.NodeHeight;
            double hSpace = Theme.HorizontalSpacing, vSpace = Theme.VerticalSpacing;
            double overlapX = 0, overlapY = nodeH * 0.33;

            double totalWidth = hSpace + (maxDepth + 1) * (nodeW + hSpace - overlapX);
            double maxExtraY = 0;
            foreach (double offset in rowExtraOffset.Values)
                if (offset > maxExtraY) maxExtraY = offset;
            double totalHeight = vSpace + (maxRow + 1) * (nodeH + vSpace - overlapY) + maxExtraY;
            canvas.Width = totalWidth;
            canvas.Height = totalHeight;

            HashSet<ProjectModel> visitedProjects = new HashSet<ProjectModel>();
            foreach (NodeLayout layout in layouts)
            {
                double x = hSpace + layout.Depth * (nodeW + hSpace - overlapX);
                double y = vSpace + layout.Row * (nodeH + vSpace - overlapY);
                if (rowExtraOffset.TryGetValue(layout, out double extraY))
                {
                    y += extraY;
                }

                System.Windows.Media.Brush brush = GetNodeBrush(layout.Node, layout.Depth == 0, visitedProjects);
                DrawNode(canvas, layout.Node, x, y, brush, nodePaths[layout.Node]);
                visitedProjects.Add(layout.Node.Project);
            }

            foreach (NodeLayout layout in layouts)
            {
                foreach (ReferrerChainNode child in layout.Node.Referrers)
                {
                    NodeLayout childLayout = layouts.Find(l => l.Node == child);
                    if (childLayout == null) continue;

                    double parentX = hSpace + layout.Depth * (nodeW + hSpace - overlapX);
                    double parentY = vSpace + layout.Row * (nodeH + vSpace - overlapY);
                    if (rowExtraOffset.TryGetValue(layout, out double parentExtraY))
                        parentY += parentExtraY;
                    double childX = hSpace + childLayout.Depth * (nodeW + hSpace - overlapX);
                    double childY = vSpace + childLayout.Row * (nodeH + vSpace - overlapY);
                    if (rowExtraOffset.TryGetValue(childLayout, out double childExtraY))
                        childY += childExtraY;

                    string parentPath = nodePaths[layout.Node];
                    string childPath = nodePaths[child];
                    DrawLine(canvas, parentX + nodeW / 2, parentY + nodeH, parentX + nodeW / 2, childY + nodeH / 2, layout.Node.Project.Name, child.Project.Name, parentPath, childPath); // vertical, tag for hover
                    DrawLine(canvas, parentX + nodeW / 2, childY + nodeH / 2, childX, childY + nodeH / 2, layout.Node.Project.Name, child.Project.Name, parentPath, childPath); // horizontal, tag + arrowhead
                }
            }
        }
    }
}
