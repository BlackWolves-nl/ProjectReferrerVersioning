using System.Text.RegularExpressions;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    /// <summary>
    /// Processes individual diff lines using regex patterns to populate a FileDiffResult.
    /// </summary>
    internal class DiffLineProcessor
    {
        private readonly string _fileName;
        
        // Verbatim regex strings: use doubled quotes for literal quotes inside pattern
        private static readonly Regex _nugetRegex = new Regex(
            @"<PackageReference[^>]*Include=""([^""]+)""[^>]*Version=""([^""]*)""[^>]*>",
            RegexOptions.Compiled);
            
        private static readonly Regex _projectRefRegex = new Regex(
            @"<ProjectReference[^>]*Include=""([^""]+)""[^>]*>",
            RegexOptions.Compiled);
            
        private static readonly Regex _versionRegex = new Regex(
            @"<(Version|AssemblyVersion|FileVersion)[^>]*>([^<]+)</\1>",
            RegexOptions.Compiled);
            
        private static readonly Regex _assemblyInfoRegex = new Regex(
            @"\[assembly:\s*(AssemblyVersion|AssemblyFileVersion)\(""([0-9.]+)""\)\]",
            RegexOptions.Compiled);

        public DiffLineProcessor(string fileName)
        {
            _fileName = fileName;
        }

        public bool ProcessLine(string line, FileDiffResult result)
        {
            string trimmed = line.TrimStart('+', '-');
            bool isAdd = line.StartsWith("+");

            return ProcessNugetChange(trimmed, isAdd, result) ||
                   ProcessProjectReference(trimmed, isAdd, result) ||
                   ProcessVersionChange(trimmed, isAdd, result) ||
                   ProcessAssemblyInfoChange(trimmed, isAdd, result);
        }

        private bool ProcessNugetChange(string trimmed, bool isAdd, FileDiffResult result)
        {
            Match match = _nugetRegex.Match(trimmed);
            if (!match.Success) return false;

            ReferenceChange change = new ReferenceChange
            {
                Name = match.Groups[1].Value,
                OldVersion = isAdd ? null : match.Groups[2].Value,
                NewVersion = isAdd ? match.Groups[2].Value : null,
                IsNuGet = true,
                IsProjectReference = false,
                ChangeType = isAdd ? ReferenceChangeType.Added : ReferenceChangeType.Removed
            };
            
            result.NugetChanges.Add(change);
            return true;
        }

        private bool ProcessProjectReference(string trimmed, bool isAdd, FileDiffResult result)
        {
            Match match = _projectRefRegex.Match(trimmed);
            if (!match.Success) return false;

            ReferenceChange change = new ReferenceChange
            {
                Name = match.Groups[1].Value,
                IsNuGet = false,
                IsProjectReference = true,
                ChangeType = isAdd ? ReferenceChangeType.Added : ReferenceChangeType.Removed
            };
            
            result.RefChanges.Add(change);
            return true;
        }

        private bool ProcessVersionChange(string trimmed, bool isAdd, FileDiffResult result)
        {
            Match match = _versionRegex.Match(trimmed);
            if (!match.Success) return false;

            ProjectVersionChange change = new ProjectVersionChange
            {
                FileName = _fileName,
                OldVersion = isAdd ? null : match.Groups[2].Value,
                NewVersion = isAdd ? match.Groups[2].Value : null,
                Source = "csproj",
                ChangeType = isAdd ? VersionChangeType.Added : VersionChangeType.Removed,
                VersionProperty = match.Groups[1].Value
            };
            
            result.RawVersionChanges.Add(change);
            return true;
        }

        private bool ProcessAssemblyInfoChange(string trimmed, bool isAdd, FileDiffResult result)
        {
            Match match = _assemblyInfoRegex.Match(trimmed);
            if (!match.Success) return false;

            ProjectVersionChange change = new ProjectVersionChange
            {
                FileName = _fileName,
                OldVersion = isAdd ? null : match.Groups[2].Value,
                NewVersion = isAdd ? match.Groups[2].Value : null,
                Source = "AssemblyInfo",
                ChangeType = isAdd ? VersionChangeType.Added : VersionChangeType.Removed,
                VersionProperty = match.Groups[1].Value
            };
            
            result.RawVersionChanges.Add(change);
            return true;
        }
    }
}
