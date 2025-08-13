using System;
using System.Collections.Generic;
using System.Windows.Controls;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    /// <summary>
    /// Provides methods for drawing project referrer chains on a WPF Canvas and handling version bump logic.
    /// </summary>
    public interface IReferrerChainDrawingService
    {
        /// <summary>
        /// Gets or sets the theme used for drawing nodes and lines.
        /// </summary>
        ReferrerChainTheme Theme { get; set; }

        /// <summary>
        /// Gets the layout mode used for arranging the referrer chain (e.g., Standard, CompactHorizontal, CompactVertical).
        /// </summary>
        ReferrerChainLayoutMode LayoutMode { get; }

        /// <summary>
        /// Draws the referrer chains on the specified canvas using the provided root nodes.
        /// </summary>
        /// <param name="canvas">The WPF Canvas to draw on.</param>
        /// <param name="roots">The list of root nodes for the referrer chains.</param>
        void DrawChainsBase(Canvas canvas, List<ReferrerChainNode> roots);

        /// <summary>
        /// Bumps the revision (fourth segment) of all child nodes if all root nodes have a new version set.
        /// Updates the internal state and returns true if any revisions were bumped.
        /// </summary>
        /// <param name="canvas">The WPF Canvas associated with the drawing.</param>
        /// <param name="roots">The list of root nodes for the referrer chains.</param>
        void BumpChildRevisionsIfAllRootsSet(Canvas canvas, List<ReferrerChainNode> roots);

        /// <summary>
        /// Raised when all root nodes have been updated and child revisions have been bumped.
        /// </summary>
        event Action AllRootNodesUpdated;
    }
}
