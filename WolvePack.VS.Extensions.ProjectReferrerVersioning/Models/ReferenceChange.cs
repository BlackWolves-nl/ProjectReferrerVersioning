using System.Collections.Generic;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

public enum ReferenceChangeType
{
    Added,
    Removed,
    Edited
}

public class ReferenceChange
{
    public string Name { get; set; }
    public string OldVersion { get; set; } // null for add/remove or project reference
    public string NewVersion { get; set; } // null for remove or project reference
    public bool IsNuGet { get; set; }
    public bool IsProjectReference { get; set; }
    public ReferenceChangeType ChangeType { get; set; }
}
