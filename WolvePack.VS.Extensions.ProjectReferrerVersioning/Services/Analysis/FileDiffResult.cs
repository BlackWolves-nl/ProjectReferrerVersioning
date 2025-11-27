using System.Collections.Generic;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

/// <summary>
/// Results extracted from a single file diff.
/// </summary>
internal class FileDiffResult
{
    public List<ReferenceChange> NugetChanges { get; } = new List<ReferenceChange>();
    public List<ReferenceChange> RefChanges { get; } = new List<ReferenceChange>();
    public List<ProjectVersionChange> RawVersionChanges { get; } = new List<ProjectVersionChange>();
    public List<ProjectVersionChange> VersionChanges { get; set; } = new List<ProjectVersionChange>();
    public bool HasOtherChanges { get; set; }
}
