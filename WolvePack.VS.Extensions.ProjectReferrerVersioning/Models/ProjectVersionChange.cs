namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

public enum VersionChangeType
{
    Added,
    Removed
}

public class ProjectVersionChange
{
    public string FileName { get; set; }
    public string OldVersion { get; set; }
    public string NewVersion { get; set; }
    public string Source { get; set; } // "csproj" or "AssemblyInfo"
    public VersionChangeType ChangeType { get; set; } // Indicates if detected as add or remove
    public string VersionProperty { get; set; } // Indicates which version property was detected (Version, AssemblyVersion, FileVersion)
}
