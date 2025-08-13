namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Models
{
    /// <summary>
    /// Represents the Git status of a project
    /// </summary>
    public enum ProjectStatus
    {
        /// <summary>
        /// Still initializing, no status set yet
        /// </summary>
        Initial,
        /// <summary>
        /// No uncommitted changes
        /// </summary>
        Clean,
        /// <summary>
        /// Has uncommitted changes (general)
        /// </summary>
        Modified,
        /// <summary>
        /// Has only NuGet package or project reference changes
        /// </summary>
        NuGetOrProjectReferenceChanges,
        /// <summary>
        /// Has only version changes (no other modifications)
        /// </summary>
        IsVersionChangeOnly,
        /// <summary>
        /// Has both NuGet/project reference changes and version changes (no other modifications)
        /// </summary>
        NuGetOrProjectReferenceAndVersionChanges
    }
}
