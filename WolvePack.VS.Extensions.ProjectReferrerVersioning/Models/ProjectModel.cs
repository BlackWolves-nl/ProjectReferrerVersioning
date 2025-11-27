using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;

using EnvDTE;

using Microsoft.VisualStudio.Shell;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;
using System.Xml.Linq;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

/// <summary>
/// Unified model for project discovery, UI binding, and Git status.
/// </summary>
public class ProjectModel : INotifyPropertyChanged
{
    // VS Project reference (UI thread only)
    public Project Project { get; }

    // Lightweight properties (safe for background thread)
    public string Name { get; set; }
    public string FullName { get; set; }
    public string FileName { get; set; }
    public string Kind { get; set; }
    public string UniqueName { get; set; }
    public bool IsCSharpProject { get; set; }
    public string Version { get; set; }

    // Project references (other projects this project depends on)
    public List<string> ProjectReferences { get; set; } = new List<string>();

    // Git/Reference change tracking
    public List<ReferenceChange> ReferenceChanges { get; set; } = new List<ReferenceChange>();

    public string FullPath => FullName ?? "";

    // Referrers: projects that reference this project
    public List<ProjectModel> Referrers { get; set; } = new List<ProjectModel>();

    // UI/Selection/Status
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    private ProjectStatus _status = ProjectStatus.Initial;
    public ProjectStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    private ProjectVersionChange _projectVersionChange;
    public ProjectVersionChange ProjectVersionChange
    {
        get => _projectVersionChange;
        set
        {
            if(value == null)
            {
                _projectVersionChange = value;
            }

            if (_projectVersionChange != value)
            {
                _projectVersionChange = value;
                // If set, update Version to OldVersion
                if (_projectVersionChange != null && !string.IsNullOrEmpty(_projectVersionChange.OldVersion))
                {
                    Version = _projectVersionChange.OldVersion;
                    OnPropertyChanged(nameof(Version));
                }

                OnPropertyChanged(nameof(ProjectVersionChange));
            }
        }
    }

    private bool _isExcludedFromVersionUpdates;
    public bool IsExcludedFromVersionUpdates
    {
        get => _isExcludedFromVersionUpdates;
        set
        {
            if (_isExcludedFromVersionUpdates != value)
            {
                _isExcludedFromVersionUpdates = value;
                OnPropertyChanged(nameof(IsExcludedFromVersionUpdates));
            }
        }
    }

    // Git change counts
    private int _gitChangedFileCount;
    public int GitChangedFileCount
    {
        get => _gitChangedFileCount;
        set
        {
            if (_gitChangedFileCount != value)
            {
                _gitChangedFileCount = value;
                OnPropertyChanged(nameof(GitChangedFileCount));
            }
        }
    }

    private int _gitChangedLineCount;
    public int GitChangedLineCount
    {
        get => _gitChangedLineCount;
        set
        {
            if (_gitChangedLineCount != value)
            {
                _gitChangedLineCount = value;
                OnPropertyChanged(nameof(GitChangedLineCount));
            }
        }
    }

    // --- Constructors ---

    /// <summary>
    /// Create from EnvDTE.Project (UI thread only)
    /// </summary>
    public ProjectModel(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        Project = project;
        Name = project.Name;
        FullName = project.FullName;
        FileName = project.FileName;
        Kind = project.Kind;
        UniqueName = project.UniqueName;
        IsCSharpProject = IsCSharpProjectStatic(project);
        ProjectReferences = GetProjectReferences(project);
        Version = GetProjectVersion(project);
    }

    /// <summary>
    /// Create from ProjectDto (background thread safe)
    /// </summary>
    public ProjectModel(ProjectModel dto)
    {
        Name = dto.Name;
        FullName = dto.FullName;
        FileName = dto.FileName;
        Kind = dto.Kind;
        UniqueName = dto.UniqueName;
        IsCSharpProject = dto.IsCSharpProject;
        ProjectReferences = new List<string>(dto.ProjectReferences);
        ReferenceChanges = new List<ReferenceChange>(dto.ReferenceChanges);
        Status = dto.Status;
        IsSelected = dto.IsSelected;
    }

    // --- Static helpers ---

    public static ProjectModel FromProject(Project project)
    {
        return new ProjectModel(project);
    }

    private static bool IsCSharpProjectStatic(Project project)
    {
        try
        {
            string[] csharpGuids = {
                "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", // Classic C# project
                "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}", // SDK-style C# project
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", // C# project (legacy)
                "{603C0E0B-DB56-11DC-BE95-000D561079B0}"  // ASP.NET MVC projects
            };
            return csharpGuids.Any(guid => string.Equals(project.Kind, guid, StringComparison.OrdinalIgnoreCase)) ||
                   !string.IsNullOrEmpty(project.FileName) && project.FileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                   !string.IsNullOrEmpty(project.FullName) && project.FullName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static List<string> GetProjectReferences(Project project)
    {
        List<string> references = new();
        try
        {
            if (project.Object is VSLangProj.VSProject vsProject && vsProject.References != null)
            {
                foreach (VSLangProj.Reference reference in vsProject.References)
                {
                    if (reference.SourceProject != null)
                    {
                        DebugHelper.Log($"Project {project.Name} references project {reference.SourceProject.Name}", "ProjectReference");
                        references.Add(reference.SourceProject.Name);
                    }
                    else
                    {
                        DebugHelper.Log($"Project {project.Name} has non-project reference: {reference.Name}", "ProjectReference");
                    }
                }
            }
        }
        catch
        {
            // Ignore reference enumeration errors
        }

        return references;
    }

    private static string GetProjectVersion(Project project)
    {
        return ReadVersionFromCsproj(project?.FileName);
    }

    /// <summary>
    /// Reads the version from a .csproj file, ensuring the result has 4 segments (Major.Minor.Patch.Revision).
    /// Checks <Version>, <AssemblyVersion>, and <FileVersion> elements.
    /// </summary>
    /// <param name="fileName">The path to the .csproj file.</param>
    /// <returns>A version string with 4 segments, e.g. "1.2.3.4". Returns "0.0.0.0" if not found or on error.</returns>
    private static string ReadVersionFromCsproj(string fileName)
    {
        if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
        {
            try
            {
                XDocument doc = XDocument.Load(fileName);
                XElement versionElement = doc.Descendants("Version").FirstOrDefault();
                XElement assemblyVersionElement = doc.Descendants("AssemblyVersion").FirstOrDefault();
                XElement fileVersionElement = doc.Descendants("FileVersion").FirstOrDefault();

                string version = versionElement?.Value;
                string assemblyVersion = assemblyVersionElement?.Value;
                string fileVersion = fileVersionElement?.Value;

                // Collect all non-null, non-empty version values
                List<string> versionValues = new();
                if (!string.IsNullOrEmpty(version)) versionValues.Add(version);
                if (!string.IsNullOrEmpty(assemblyVersion)) versionValues.Add(assemblyVersion);
                if (!string.IsNullOrEmpty(fileVersion)) versionValues.Add(fileVersion);

                // If more than one version value is present and not all are identical, throw
                if (versionValues.Count > 1 && versionValues.Distinct().Count() > 1)
                {
                    throw new InvalidOperationException($"Conflicting version values in csproj: Version='{version}', AssemblyVersion='{assemblyVersion}', FileVersion='{fileVersion}'");
                }

                string rawVersion = version ?? assemblyVersion ?? fileVersion ?? "0.0.0.0";

                string[] segments = rawVersion.Split('.');
                List<string> segList = new(segments);
                while (segList.Count < 4)
                {
                    segList.Add("0");
                }

                if (segList.Count > 4)
                {
                    segList = segList.GetRange(0, 4);
                }

                return string.Join(".", segList);
            }
            catch (Exception)
            {
                // Optionally log ex.Message for diagnostics
                return "0.0.0.0";
            }
        }

        return "0.0.0.0";
    }

    public static void RefreshVersionsFromCsproj(IEnumerable<ProjectModel> models)
    {
        foreach (ProjectModel model in models)
        {
            model.Version = ReadVersionFromCsproj(model.FileName);
        }
    }

    // --- INotifyPropertyChanged ---

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}