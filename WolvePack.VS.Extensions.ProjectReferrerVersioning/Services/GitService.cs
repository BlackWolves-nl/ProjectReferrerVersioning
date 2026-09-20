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

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

/// <summary>
/// Service for Git operations and project analysis.
/// Consolidates all git queries so the rest of the code does not have to spawn processes or parse output.
/// </summary>
public static class GitService
{
    private const int _max_DIRECTORY_LEVELS = 20;
    private const string _head_REF = "HEAD";

    // Comparison ref per repository root ("HEAD", or the upstream branch when the branch tracks one).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _baseRefCache = new();

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
    /// Returns all changed (added/modified/deleted) files: everything reported by git status --porcelain
    /// plus everything changed by unpushed commits (when the branch tracks an upstream).
    /// </summary>
    public static async Task<List<string>> GetAllChangedFilesInRepoAsync(string repoRoot)
    {
        DebugHelper.Log($"GetChangedFiles: Starting Git status check in '{repoRoot}'", nameof(GitService));
        try
        {
            // Analysis runs start here, so this is where the cached comparison ref is refreshed.
            string baseRef = await GetAnalysisBaseRefAsync(repoRoot, refresh: true);

            string output = await RunGitCommandAsync(repoRoot, "status --porcelain");
            List<string> files = ParseGitStatusOutput(output);
            if (baseRef == _head_REF) return files;

            // Unpushed commits: files changed between the upstream and the working tree that git status
            // does not report (already committed locally).
            string committedOutput = await RunGitCommandAsync(repoRoot, "diff --name-only -z " + baseRef);
            HashSet<string> knownFiles = new(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in committedOutput.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string normalized = file.Replace('/', Path.DirectorySeparatorChar);
                if (knownFiles.Add(normalized)) files.Add(normalized);
            }

            DebugHelper.Log($"GetChangedFiles: {files.Count} changed files against '{baseRef}'", nameof(GitService));
            return files;
        }
        catch (Exception ex)
        {
            DebugHelper.Log($"GetChangedFiles: Error getting changed files: {ex.Message}", nameof(GitService));
            return new List<string>();
        }
    }

    /// <summary>
    /// Comparison ref for project analysis: honours the "include unpushed commits" user setting,
    /// falling back to HEAD (uncommitted changes only) when it is turned off.
    /// </summary>
    private static Task<string> GetAnalysisBaseRefAsync(string repoRoot, bool refresh)
    {
        return UserSettings.ActiveIncludeUnpushedCommits
            ? ResolveBaseRefAsync(repoRoot, refresh)
            : Task.FromResult(_head_REF);
    }

    /// <summary>
    /// Resolves the ref that diffs are compared against: the branch's upstream when it tracks one
    /// (so unpushed commits are included), otherwise HEAD (uncommitted changes only).
    /// Cached per repository and refreshed at the start of each analysis run.
    /// </summary>
    private static async Task<string> ResolveBaseRefAsync(string repoRoot, bool refresh)
    {
        if (!refresh && _baseRefCache.TryGetValue(repoRoot, out string cached)) return cached;

        string upstream = (await RunGitCommandAsync(repoRoot, "rev-parse --abbrev-ref --symbolic-full-name @{u}")).Trim();
        string baseRef = string.IsNullOrEmpty(upstream) ? _head_REF : upstream;
        _baseRefCache[repoRoot] = baseRef;
        DebugHelper.Log($"ResolveBaseRef: Comparing against '{baseRef}' in '{repoRoot}'", nameof(GitService));
        return baseRef;
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

    /// <summary>
    /// Returns the full unified diff for every changed file under the project's directory, plus the ref
    /// it was compared against. Covers staged and unstaged changes and untracked files (as additions);
    /// with <paramref name="includeUnpushedCommits"/> it also covers commits not yet pushed to the
    /// branch's upstream (falls back to HEAD when the branch tracks no upstream).
    /// </summary>
    public static async Task<(string Diff, string BaseRef)> GetProjectDiffAsync(ProjectModel projectModel, bool includeUnpushedCommits)
    {
        string projectDir = Path.GetDirectoryName(projectModel?.FileName ?? "");
        if (string.IsNullOrEmpty(projectDir)) return ("", _head_REF);

        string repoRoot = FindGitRootForSolutionOrProjectFile(projectModel.FileName);
        if (string.IsNullOrEmpty(repoRoot)) return ("", _head_REF);

        string baseRef = _head_REF;
        try
        {
            baseRef = includeUnpushedCommits ? await ResolveBaseRefAsync(repoRoot, refresh: false) : _head_REF;

            bool isRepoRoot = string.Equals(Path.GetFullPath(projectDir).TrimEnd('\\', '/'), Path.GetFullPath(repoRoot).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            string pathSpec = isRepoRoot ? "." : GetRelativePath(repoRoot, projectDir).TrimEnd('/', '\\');

            // Tracked changes (staged and unstaged, plus unpushed commits when comparing against an upstream).
            // Falls back to the working tree diff for repos without a HEAD commit.
            string trackedDiff = await RunGitCommandAsync(repoRoot, "diff " + baseRef + " -- \"" + pathSpec + "\"");
            if (string.IsNullOrEmpty(trackedDiff))
                trackedDiff = await RunGitCommandAsync(repoRoot, "diff -- \"" + pathSpec + "\"");

            // Untracked files are not part of git diff; render them as full-file additions.
            string untrackedOutput = await RunGitCommandAsync(repoRoot, "ls-files -z --others --exclude-standard -- \"" + pathSpec + "\"");
            List<string> untrackedFiles = untrackedOutput
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            string[] untrackedDiffs = await Task.WhenAll(untrackedFiles.Select(f =>
                RunGitCommandAsync(repoRoot, "diff --no-index -- /dev/null \"" + f + "\"")));

            string diff = string.Join("\n", new[] { trackedDiff }.Concat(untrackedDiffs).Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.TrimEnd('\n')));
            return (diff, baseRef);
        }
        catch (Exception ex)
        {
            DebugHelper.Log($"GetProjectDiff: Error getting diff for '{projectModel.Name}': {ex.Message}", nameof(GitService));
            return ("", baseRef);
        }
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
        List<string> changedFiles = new();
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
    /// Uses git diff --numstat to count added + deleted lines across all changed files belonging to a project
    /// (including unpushed commits when the branch tracks an upstream).
    /// </summary>
    private static async Task<int> CalculateChangedLinesAsync(string repoRoot, List<string> changedFiles)
    {
        if (changedFiles.Count == 0) return 0;
        try
        {
            string baseRef = await GetAnalysisBaseRefAsync(repoRoot, refresh: false);
            string diffNumstat = await RunGitCommandAsync(repoRoot,
                "diff " + baseRef + " --numstat -- " + string.Join(" ", changedFiles.Select(f => '"' + f + '"')));
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
        FileAnalysisResults results = new();

        // Analyze each file's diff concurrently instead of one git process at a time. Each task builds its own
        // local result (AnalyzeSingleFileAsync catches its own errors) so nothing is written to a shared object
        // from multiple tasks at once. RunGitCommandAsync's semaphore bounds real OS concurrency.
        FileDiffResult[] fileResults = await Task.WhenAll(changedFiles.Select(relFile => AnalyzeSingleFileAsync(repoRoot, relFile)));

        foreach (FileDiffResult fileResult in fileResults)
        {
            results.ReferenceChanges.AddRange(fileResult.NugetChanges);
            results.ReferenceChanges.AddRange(fileResult.RefChanges);
            results.VersionChanges.AddRange(fileResult.VersionChanges);
            if (fileResult.HasOtherChanges) results.HasOtherChanges = true;
        }

        return results;
    }

    private static async Task<FileDiffResult> AnalyzeSingleFileAsync(string repoRoot, string relFile)
    {
        try
        {
            string absFile = Path.Combine(repoRoot, relFile);
            string ext = Path.GetExtension(absFile).ToLowerInvariant();
            string fileName = Path.GetFileName(absFile);
            if (IsAnalyzableFile(ext, fileName))
            {
                string diff = await GetGitDiffAsync(repoRoot, absFile);
                return AnalyzeProjectFileDiff(absFile, diff);
            }

            return new FileDiffResult { HasOtherChanges = true };
        }
        catch (Exception ex)
        {
            DebugHelper.Log($"AnalyzeProject: Error processing changed file '{relFile}': {ex.Message}", nameof(GitService));
            return new FileDiffResult { HasOtherChanges = true };
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
        FileDiffResult result = new();
        DiffLineProcessor diffProcessor = new(file);
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
        List<ProjectVersionChange> combinedVersionChanges = new();
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
        List<string> files = new();
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

            // Renames/copies are reported as "old/path -> new/path" (each side optionally quoted).
            // We only care about the current (destination) path.
            int arrowIndex = file.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrowIndex >= 0)
            {
                file = file.Substring(arrowIndex + 4).Trim();
            }

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

    private static async Task<string> GetGitDiffAsync(string repoRoot, string file)
    {
        string rel = GetRelativePath(repoRoot, file);
        string baseRef = await GetAnalysisBaseRefAsync(repoRoot, refresh: false);
        return await RunGitCommandAsync(repoRoot, "diff " + baseRef + " -- \"" + rel + "\"");
    }

    private const int _gitCommandTimeoutMilliseconds = 30000;

    // Global cap on concurrent git.exe processes across all callers - lets call sites parallelize freely
    // (e.g. one Task per changed file) without risking dozens/hundreds of processes spawning at once.
    private static readonly System.Threading.SemaphoreSlim _gitProcessThrottle = new(4, 4);

    private static async Task<string> RunGitCommandAsync(string workingDir, string args, int timeoutMilliseconds = _gitCommandTimeoutMilliseconds)
    {
        await _gitProcessThrottle.WaitAsync();
        try
        {
            ProcessStartInfo psi = new("git", args)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            using (Process proc = Process.Start(psi))
            {
                Task<string> outputTask = proc.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = proc.StandardError.ReadToEndAsync();

                // Read stdout and stderr concurrently - draining only one while the other fills its OS
                // pipe buffer can deadlock the child process.
                Task readTask = Task.WhenAll(outputTask, errorTask);

                Task completed = await Task.WhenAny(readTask, Task.Delay(timeoutMilliseconds));
                if (completed != readTask)
                {
                    DebugHelper.Log($"RunGitCommandAsync: 'git {args}' in '{workingDir}' timed out after {timeoutMilliseconds}ms; killing process", nameof(GitService));
                    try { proc.Kill(); } catch { /* already exited */ }

                    return "";
                }

                // Streams have closed, which happens at/around process exit - bound this too in case the
                // process lingers after closing its handles.
                if (!proc.WaitForExit(5000))
                {
                    DebugHelper.Log($"RunGitCommandAsync: 'git {args}' in '{workingDir}' did not exit after streams closed; killing process", nameof(GitService));
                    try { proc.Kill(); } catch { /* already exited */ }

                    return "";
                }

                if (proc.ExitCode != 0)
                {
                    string errorOutput = await errorTask;
                    DebugHelper.Log($"RunGitCommandAsync: 'git {args}' exited with code {proc.ExitCode} in '{workingDir}': {errorOutput}", nameof(GitService));
                }

                return await outputTask;
            }
        }
        finally
        {
            _gitProcessThrottle.Release();
        }
    }

    private static string GetRelativePath(string relativeTo, string path)
    {
        Uri pathUri = new(path);
        if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
            relativeTo += Path.DirectorySeparatorChar;
        Uri folderUri = new(relativeTo);
        return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
    }
}
