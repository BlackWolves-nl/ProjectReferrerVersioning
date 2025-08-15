using System.Collections.Generic;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    /// <summary>
    /// Aggregated results from analyzing all changed files for a project.
    /// </summary>
    internal class FileAnalysisResults
    {
        public List<ReferenceChange> ReferenceChanges { get; } = new List<ReferenceChange>();
        public List<ProjectVersionChange> VersionChanges { get; } = new List<ProjectVersionChange>();
        public bool HasOtherChanges { get; set; }
    }
}
