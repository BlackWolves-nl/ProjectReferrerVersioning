using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows; // For MessageBox

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    /// <summary>
    /// Service for Git operations and project analysis.
    /// Consolidates all git queries so the rest of the code does not have to spawn processes or parse output.
    /// </summary>
    public static class GitService
    {
        private const int _max_DIRECTORY_LEVELS = 20;

        // =================================================================================================
        // Public API Methods
        // =================================================================================================
        /// <summary>
        /// Analyze a single project by first collecting the changed files for the entire repository.
        /// </summary>
        public static async Task<(ProjectStatus status, List<ReferenceChange> referenceChanges)> AnalyzeProjectAsync(ProjectModel projectModel, string repoRoot)
        {
            List<string> allChangedFiles = await GetAllChangedFilesInRepoAsync(repoRoot);
            return await AnalyzeProjectWithChangedFilesAsync(projectModel, repoRoot, allChangedFiles);
        }

        /// <summary>
        /// Analyze a project using a pre-fetched list of all changed files in the repository (perf optimization).
        /// </summary>
        public static async Task<(ProjectStatus status, List<ReferenceChange> referenceChanges)> AnalyzeProjectWithChangedFilesAsync(
            ProjectModel projectModel,
            string repoRoot,
            List<string> allChangedFiles)
        {
            (bool IsValid, string ProjectDirectory) = InitializeProjectAnalysis(projectModel);
            if (!IsValid)
            {
                return (ProjectStatus.Clean, new List<ReferenceChange>());
            }

            List<string> changedFiles = FilterChangedFilesForProject(allChangedFiles, repoRoot, ProjectDirectory);
            await UpdateProjectChangeCountsAsync(projectModel, repoRoot, changedFiles);
            if (!changedFiles.Any()) return (ProjectStatus.Clean, new List<ReferenceChange>());

            FileAnalysisResults analysisResults = await AnalyzeChangedFilesAsync(repoRoot, changedFiles);
            ProjectVersionChange versionChange = ProcessVersionChanges(analysisResults.VersionChanges, projectModel.Name);
            projectModel.ProjectVersionChange = versionChange;

            ProjectStatus status = DetermineProjectStatus(analysisResults.HasOtherChanges, analysisResults.ReferenceChanges.Count, versionChange);
            DebugHelper.Log($"AnalyzeProject: Final status for '{projectModel.Name}': {status}", nameof(GitService));
            return (status, analysisResults.ReferenceChanges);
        }

        /// <summary>
        /// Returns all changed (added/modified/deleted) files reported by git status --porcelain.
        /// </summary>
        public static async Task<List<string>> GetAllChangedFilesInRepoAsync(string repoRoot)
        {
            DebugHelper.Log($"GetChangedFiles: Starting Git status check in '{repoRoot}'", nameof(GitService));
            try
            {
                string output = await RunGitCommandAsync(repoRoot, "status --porcelain");
                return ParseGitStatusOutput(output);
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"GetChangedFiles: Error getting changed files: {ex.Message}", nameof(GitService));
                return new List<string>();
            }
        }

        /// <summary>
        /// Attempts to resolve the git repository root for a given solution or project file.
        /// </summary>
        public static string FindGitRootForSolutionOrProjectFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                DebugHelper.Log("FindGitRoot: filePath is null or empty", nameof(GitService));
                return null;
            }

            return FindGitRootFromDirectory(Path.GetDirectoryName(filePath));
        }

        // =================================================================================================
        // Project Analysis Methods
        // =================================================================================================
        /// <summary>
        /// Initial validation and setup. Returns false if the project directory is invalid.
        /// </summary>
        private static (bool IsValid, string ProjectDirectory) InitializeProjectAnalysis(ProjectModel projectModel)
        {
            string projectDir = Path.GetDirectoryName(projectModel.FileName);
            if (string.IsNullOrEmpty(projectDir))
            {
                projectModel.GitChangedFileCount = 0;
                projectModel.GitChangedLineCount = 0;
                return (false, null);
            }

            DebugHelper.Log($"AnalyzeProject: Starting analysis for '{projectModel.Name}', projectDir: '{projectDir}'", nameof(GitService));
            return (true, projectDir);
        }

        /// <summary>
        /// Narrows the full changed file list to only those under the project's directory.
        /// </summary>
        private static List<string> FilterChangedFilesForProject(List<string> allChangedFiles, string repoRoot, string projectDir)
        {
            DebugHelper.Log($"AnalyzeProject: Total changed files in repo: {allChangedFiles.Count}", nameof(GitService));
            List<string> changedFiles = new List<string>();
            string projectDirFull;
            try
            {
                projectDirFull = Path.GetFullPath(projectDir);
                DebugHelper.Log($"AnalyzeProject: Project full path: '{projectDirFull}'", nameof(GitService));
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"AnalyzeProject: Error getting full path for project directory '{projectDir}': {ex.Message}", nameof(GitService));
                return changedFiles;
            }

            foreach (string file in allChangedFiles)
            {
                if (IsFileUnderProjectDirectory(file, repoRoot, projectDirFull))
                {
                    changedFiles.Add(file);
                    DebugHelper.Log($"AnalyzeProject: Matched file: '{file}'", nameof(GitService));
                }
            }

            DebugHelper.Log($"AnalyzeProject: Found {changedFiles.Count} changed files for project", nameof(GitService));
            return changedFiles;
        }

        private static bool IsFileUnderProjectDirectory(string file, string repoRoot, string projectDirFull)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(file) || file.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    DebugHelper.Log($"AnalyzeProject: Skipping invalid file: '{file}'", nameof(GitService));
                    return false;
                }

                string combinedPath = Path.Combine(repoRoot, file);
                string fullPath = Path.GetFullPath(combinedPath);
                return fullPath.StartsWith(projectDirFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"AnalyzeProject: Error processing file '{file}': {ex.Message}", nameof(GitService));
                return false;
            }
        }

        private static async Task UpdateProjectChangeCountsAsync(ProjectModel projectModel, string repoRoot, List<string> changedFiles)
        {
            projectModel.GitChangedFileCount = changedFiles.Count;
            projectModel.GitChangedLineCount = await CalculateChangedLinesAsync(repoRoot, changedFiles);
        }

        /// <summary>
        /// Uses git diff --numstat to count added + deleted lines across all changed files belonging to a project.
        /// </summary>
        private static async Task<int> CalculateChangedLinesAsync(string repoRoot, List<string> changedFiles)
        {
            if (changedFiles.Count == 0) return 0;
            try
            {
                string diffNumstat = await RunGitCommandAsync(repoRoot,
                    "diff --numstat -- " + string.Join(" ", changedFiles.Select(f => '"' + f + '"')));
                return ParseDiffNumstat(diffNumstat);
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"AnalyzeProject: Error getting diff stats: {ex.Message}", nameof(GitService));
                return 0;
            }
        }

        private static int ParseDiffNumstat(string diffNumstat)
        {
            int changedLines = 0;
            string[] lines = diffNumstat.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 2)
                {
                    if (int.TryParse(parts[0], out int added)) changedLines += added;
                    if (int.TryParse(parts[1], out int deleted)) changedLines += deleted;
                }
            }

            return changedLines;
        }

        // =================================================================================================
        // File Analysis Methods
        // =================================================================================================
        /// <summary>
        /// Analyze the subset of changed files relevant to a project, building a roll-up of reference & version changes.
        /// </summary>
        private static async Task<FileAnalysisResults> AnalyzeChangedFilesAsync(string repoRoot, List<string> changedFiles)
        {
            FileAnalysisResults results = new FileAnalysisResults();
            foreach (string relFile in changedFiles)
            {
                try
                {
                    await AnalyzeSingleFileAsync(repoRoot, relFile, results);
                }
                catch (Exception ex)
                {
                    DebugHelper.Log($"AnalyzeProject: Error processing changed file '{relFile}': {ex.Message}", nameof(GitService));
                    results.HasOtherChanges = true;
                }
            }

            return results;
        }

        private static async Task AnalyzeSingleFileAsync(string repoRoot, string relFile, FileAnalysisResults results)
        {
            string absFile = Path.Combine(repoRoot, relFile);
            string ext = Path.GetExtension(absFile).ToLowerInvariant();
            string fileName = Path.GetFileName(absFile);
            if (IsAnalyzableFile(ext, fileName))
            {
                string diff = await GetGitDiffAsync(repoRoot, absFile);
                FileDiffResult diffResult = AnalyzeProjectFileDiff(absFile, diff);
                results.ReferenceChanges.AddRange(diffResult.NugetChanges);
                results.ReferenceChanges.AddRange(diffResult.RefChanges);
                results.VersionChanges.AddRange(diffResult.VersionChanges);
                if (diffResult.HasOtherChanges) results.HasOtherChanges = true;
            }
            else
            {
                results.HasOtherChanges = true;
            }
        }

        /// <summary>
        /// Restricts expensive diff parsing to project system / version impacting files.
        /// </summary>
        private static bool IsAnalyzableFile(string extension, string fileName)
        {
            return extension == ".csproj" ||
                   extension == ".props" ||
                   fileName.Equals("packages.config", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase);
        }

        // =================================================================================================
        // Diff Analysis Methods
        // =================================================================================================
        private static FileDiffResult AnalyzeProjectFileDiff(string file, string diff)
        {
            FileDiffResult result = new FileDiffResult();
            DiffLineProcessor diffProcessor = new DiffLineProcessor(file);
            string[] lines = diff.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (ShouldSkipDiffLine(line)) continue;
                if (diffProcessor.ProcessLine(line, result)) continue; // processed
                result.HasOtherChanges = true; // unrecognized change => mark other changes
                break;
            }

            result.VersionChanges = CombineVersionChanges(result.RawVersionChanges);
            return result;
        }

        private static bool ShouldSkipDiffLine(string line)
        {
            if (line.StartsWith("+++", StringComparison.Ordinal) ||
                line.StartsWith("---", StringComparison.Ordinal) ||
                line.StartsWith("@@", StringComparison.Ordinal)) return true;
            if (!(line.StartsWith("+") || line.StartsWith("-"))) return true;
            string trimmed = line.TrimStart('+', '-');
            return string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("<!--");
        }

        private static List<ProjectVersionChange> CombineVersionChanges(List<ProjectVersionChange> rawVersionChanges)
        {
            List<ProjectVersionChange> combinedVersionChanges = new List<ProjectVersionChange>();
            var grouped = rawVersionChanges.GroupBy(vc => new { vc.FileName, vc.Source, vc.VersionProperty });
            foreach (var group in grouped)
            {
                ProjectVersionChange combined = CombineVersionChangeGroup(group);
                if (combined != null) combinedVersionChanges.Add(combined);
            }

            return combinedVersionChanges;
        }

        private static ProjectVersionChange CombineVersionChangeGroup(IGrouping<dynamic, ProjectVersionChange> group)
        {
            ProjectVersionChange add = group.FirstOrDefault(vc => vc.ChangeType == VersionChangeType.Added);
            ProjectVersionChange remove = group.FirstOrDefault(vc => vc.ChangeType == VersionChangeType.Removed);
            if (add != null && remove != null)
            {
                return new ProjectVersionChange
                {
                    FileName = add.FileName,
                    OldVersion = remove.OldVersion,
                    NewVersion = add.NewVersion,
                    Source = add.Source,
                    ChangeType = VersionChangeType.Added,
                    VersionProperty = add.VersionProperty
                };
            }

            if (add != null)
            {
                return new ProjectVersionChange
                {
                    FileName = add.FileName,
                    OldVersion = null,
                    NewVersion = add.NewVersion,
                    Source = add.Source,
                    ChangeType = VersionChangeType.Added,
                    VersionProperty = add.VersionProperty
                };
            }

            if (remove != null)
            {
                return new ProjectVersionChange
                {
                    FileName = remove.FileName,
                    OldVersion = remove.OldVersion,
                    NewVersion = null,
                    Source = remove.Source,
                    ChangeType = VersionChangeType.Removed,
                    VersionProperty = remove.VersionProperty
                };
            }

            return null;
        }

        // =================================================================================================
        // Version Change Processing
        // =================================================================================================
        private static ProjectVersionChange ProcessVersionChanges(List<ProjectVersionChange> versionChanges, string projectName)
        {
            if (versionChanges.Count <= 1) return versionChanges.FirstOrDefault();
            return ValidateAndResolveVersionConflicts(versionChanges, projectName);
        }

        private static ProjectVersionChange ValidateAndResolveVersionConflicts(List<ProjectVersionChange> versionChanges, string projectName)
        {
            ProjectVersionChange first = versionChanges[0];
            bool allIdentical = versionChanges.All(vc =>
                vc.OldVersion == first.OldVersion && vc.NewVersion == first.NewVersion);
            if (allIdentical) return first;
            ShowVersionConflictDialog(versionChanges, projectName);
            throw new InvalidOperationException($"Conflicting version changes detected for project '{projectName}'. Generation stopped.");
        }

        private static void ShowVersionConflictDialog(List<ProjectVersionChange> versionChanges, string projectName)
        {
            string msg = $"Multiple conflicting version changes detected for project '{projectName}':\n\n";
            foreach (ProjectVersionChange vc in versionChanges)
            {
                msg += $"File: {vc.FileName}\nSource: {vc.Source}\nOld Version: {vc.OldVersion ?? "<none>"}\nNew Version: {vc.NewVersion ?? "<none>"}\n\n";
            }

            MessageBox.Show(msg, "Version Change Conflict", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // =================================================================================================
        // Status Determination
        // =================================================================================================
        private static ProjectStatus DetermineProjectStatus(bool hasOtherChanges, int referenceChangeCount, ProjectVersionChange versionChange)
        {
            if (hasOtherChanges) return ProjectStatus.Modified;
            bool hasReferenceChanges = referenceChangeCount > 0;
            bool hasVersionChange = versionChange != null;
            if (hasReferenceChanges && hasVersionChange) return ProjectStatus.NuGetOrProjectReferenceAndVersionChanges;
            if (hasReferenceChanges) return ProjectStatus.NuGetOrProjectReferenceChanges;
            if (hasVersionChange) return ProjectStatus.IsVersionChangeOnly;
            return ProjectStatus.Clean;
        }

        // =================================================================================================
        // Git Operations (status parsing / diff helpers)
        // =================================================================================================
        private static List<string> ParseGitStatusOutput(string output)
        {
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> files = new List<string>();
            DebugHelper.Log($"GetChangedFiles: Git status returned {lines.Length} lines", nameof(GitService));
            foreach (string line in lines)
            {
                string file = ParseGitStatusLine(line);
                if (!string.IsNullOrEmpty(file))
                {
                    files.Add(file);
                    DebugHelper.Log($"GetChangedFiles: Added file: '{file}' (from line: '{line}')", nameof(GitService));
                }
            }

            DebugHelper.Log($"GetChangedFiles: Successfully parsed {files.Count} changed files", nameof(GitService));
            return files;
        }

        private static string ParseGitStatusLine(string line)
        {
            try
            {
                if (line.Length <= 3) return null;
                string file = line.Substring(3).Trim();
                if (string.IsNullOrWhiteSpace(file)) return null;
                if (file.StartsWith("\"") && file.EndsWith("\"") && file.Length > 2) file = UnescapeGitFilename(file);
                if (file.IndexOfAny(new char[] { '\0' }) >= 0) return null;
                return file;
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"GetChangedFiles: Error processing line '{line}': {ex.Message}", nameof(GitService));
                return null;
            }
        }

        private static string UnescapeGitFilename(string quotedFile)
        {
            string file = quotedFile.Substring(1, quotedFile.Length - 2);
            return file.Replace("\\\"", "\"")
                       .Replace("\\\\", "\\")
                       .Replace("\\t", "\t")
                       .Replace("\\n", "\n")
                       .Replace("\\r", "\r");
        }

        private static string FindGitRootFromDirectory(string startDir)
        {
            try
            {
                DebugHelper.Log($"FindGitRoot: Starting search from directory: '{startDir}'", nameof(GitService));
                string dir = startDir;
                int levels = 0;
                while (!string.IsNullOrEmpty(dir) && levels < _max_DIRECTORY_LEVELS)
                {
                    DebugHelper.Log($"FindGitRoot: Checking level {levels}: '{dir}'", nameof(GitService));
                    if (!Directory.Exists(dir)) break;
                    if (HasGitRepository(dir))
                    {
                        DebugHelper.Log($"FindGitRoot: Found Git repository at: '{dir}'", nameof(GitService));
                        return dir;
                    }

                    string parentDir = Path.GetDirectoryName(dir);
                    if (parentDir == dir) break;
                    dir = parentDir;
                    levels++;
                }

                DebugHelper.Log($"FindGitRoot: No Git repository found after checking {levels} levels", nameof(GitService));
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"FindGitRoot: Exception occurred: {ex.Message}", nameof(GitService));
            }

            return null;
        }

        private static bool HasGitRepository(string directory)
        {
            string gitDir = Path.Combine(directory, ".git");
            return Directory.Exists(gitDir) || File.Exists(gitDir);
        }

        private static Task<string> GetGitDiffAsync(string repoRoot, string file)
        {
            string rel = GetRelativePath(repoRoot, file);
            return RunGitCommandAsync(repoRoot, "diff \"" + rel + "\"");
        }

        private static async Task<string> RunGitCommandAsync(string workingDir, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process proc = Process.Start(psi))
            {
                string output = await proc.StandardOutput.ReadToEndAsync();
                proc.WaitForExit();
                return output;
            }
        }

        private static string GetRelativePath(string relativeTo, string path)
        {
            Uri pathUri = new Uri(path);
            if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
                relativeTo += Path.DirectorySeparatorChar;
            Uri folderUri = new Uri(relativeTo);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
