using System.Collections.Generic;
using System.Linq;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

/// <summary>
/// Result container for version update operations.
/// </summary>
public class VersionUpdateResult
{
    public List<string> Successes { get; } = new List<string>();
    public List<string> Errors   { get; } = new List<string>();
    public string Message => string.Join("\n", Successes.Concat(Errors));
    public bool HasErrors => Errors.Count > 0;
}
