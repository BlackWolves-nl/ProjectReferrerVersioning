using System.Collections.Generic;
using System.Windows.Controls;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    public class CompactHorizontalOverlapReferrerChainDrawingService : ReferrerChainDrawingServiceBase
    {
        public override ReferrerChainLayoutMode LayoutMode => ReferrerChainLayoutMode.CompactVertical;
        public CompactHorizontalOverlapReferrerChainDrawingService(ReferrerChainTheme theme) : base(theme) { }

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

            HashSet<ProjectModel> visitedProjects = new HashSet<ProjectModel>();
            foreach (NodeLayout layout in layouts)
            {
                double x = hSpace + layout.Depth * (nodeW + hSpace - overlapX);
                double y = vSpace + layout.Row * (nodeH + vSpace - overlapY);
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
                    double childX = hSpace + childLayout.Depth * (nodeW + hSpace - overlapX);
                    double childY = vSpace + childLayout.Row * (nodeH + vSpace - overlapY);

                    double startX = parentX + nodeW / 2;
                    double startY = parentY + nodeH;
                    double midX = startX;
                    double midY = childY + nodeH / 2;
                    double endX = childX;
                    double endY = midY;

                    string parentPath = nodePaths[layout.Node];
                    string childPath = nodePaths[child];
                    DrawLine(canvas, startX, startY, midX, midY, layout.Node.Project.Name, child.Project.Name, parentPath, childPath); // vertical, tag for hover
                    DrawLine(canvas, midX, midY, endX, endY, layout.Node.Project.Name, child.Project.Name, parentPath, childPath); // horizontal, tag + arrowhead
                }
            }
        }
    }
}
