using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using EnvDTE;

using Microsoft.VisualStudio.Shell;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

/// <summary>
/// Service for discovering and analyzing projects in the solution - PERFORMANCE OPTIMIZED
/// </summary>
public static class ProjectDiscoveryService
{
    // Cache to avoid repeated expensive calls
    private static readonly object _cacheLock = new();
    private static List<ProjectModel> _cachedProjectModels;
    private static string _cachedSolutionPath;
    private static DateTime _lastCacheTime = DateTime.MinValue;
    private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Drops the cached project list so the next discovery re-reads the solution.
    /// </summary>
    public static void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedProjectModels = null;
            _cachedSolutionPath = null;
            _lastCacheTime = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Ultra-fast project discovery with minimal UI thread time.
    /// The cache is keyed on the open solution, so switching solutions (for example to another
    /// worktree of the same repository) never returns the previous solution's projects.
    /// </summary>
    public static async Task<List<ProjectModel>> GetBasicSolutionProjectsAsync()
    {
        // Quick UI thread burst - get all project data at once
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        string solutionPath = GetOpenSolutionPath();

        // Check cache first (thread-safe), but only for the solution it was built from
        lock (_cacheLock)
        {
            if (_cachedProjectModels != null &&
                string.Equals(_cachedSolutionPath, solutionPath, StringComparison.OrdinalIgnoreCase) &&
                DateTime.UtcNow - _lastCacheTime < _cacheExpiry)
            {
                return _cachedProjectModels.ToList(); // Return copy to avoid thread issues
            }
        }

        List<ProjectModel> projectModels = null;
        try
        {
            projectModels = ExtractAllProjectDataQuickly();
        }
        catch (Exception ex)
        {
            DebugHelper.Log($"Exception in GetBasicSolutionProjectsAsync: {ex}", nameof(ProjectDiscoveryService));
        }
        finally
        {
            // Cache the results
            lock (_cacheLock)
            {
                _cachedProjectModels = projectModels ?? new List<ProjectModel>();
                _cachedSolutionPath = solutionPath;
                _lastCacheTime = DateTime.UtcNow;
            }
        }

        return projectModels ?? new List<ProjectModel>();
    }

    /// <summary>
    /// Full path of the currently open solution, or an empty string when none is open.
    /// </summary>
    private static string GetOpenSolutionPath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return Package.GetGlobalService(typeof(DTE)) is EnvDTE80.DTE2 dte ? dte.Solution?.FullName ?? "" : "";
        }
        catch (Exception ex)
        {
            DebugHelper.Log($"Exception reading solution path: {ex}", nameof(ProjectDiscoveryService));
            return "";
        }
    }

    /// <summary>
    /// Single UI thread burst to extract all project data at once
    /// </summary>
    private static List<ProjectModel> ExtractAllProjectDataQuickly()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        List<ProjectModel> projectModels = new();
        
        try
        {
            if (Package.GetGlobalService(typeof(DTE)) is not EnvDTE80.DTE2 dte || dte.Solution?.Projects == null)
                return projectModels;

            // Use fast enumeration with minimal property access
            List<Project> allProjects = GetAllProjectsRecursivelyFast(dte.Solution.Projects);
            
            // Extract data in single UI thread pass
            foreach (Project project in allProjects)
            {
                try
                {
                    if (project == null) continue;

                    ProjectModel model = ProjectModel.FromProject(project);
                    if (model.IsCSharpProject)
                    {
                        projectModels.Add(model);
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.Log($"Exception in ExtractAllProjectDataQuickly (project loop): {ex}", nameof(ProjectDiscoveryService));
                    // Skip problematic projects
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.Log($"Exception in ExtractAllProjectDataQuickly: {ex}", nameof(ProjectDiscoveryService));
            // Return what we have
        }
        
        return projectModels.OrderBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Optimized recursive project enumeration with minimal property access
    /// </summary>
    private static List<Project> GetAllProjectsRecursivelyFast(Projects projects)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        List<Project> allProjects = new();
        
        try
        {
            foreach (Project project in projects)
            {
                if (project == null) continue;
                
                try
                {
                    allProjects.Add(project);
                    
                    // Quick solution folder check with minimal property access
                    if (IsSolutionFolderFast(project))
                    {
                        List<Project> subProjects = GetProjectsFromProjectItemsFast(project.ProjectItems);
                        allProjects.AddRange(subProjects);
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.Log($"Exception in GetAllProjectsRecursivelyFast (project loop): {ex}", nameof(ProjectDiscoveryService));
                    // Skip problematic projects
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.Log($"Exception in GetAllProjectsRecursivelyFast: {ex}", nameof(ProjectDiscoveryService));
            // Return what we have
        }
        
        return allProjects;
    }

    private static bool IsSolutionFolderFast(Project project)
    {
        try
        {
            return string.Equals(project.Kind, "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static List<Project> GetProjectsFromProjectItemsFast(ProjectItems projectItems)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        List<Project> projects = new();
        
        try
        {
            if (projectItems == null) return projects;
            
            foreach (ProjectItem item in projectItems)
            {
                if (item?.SubProject != null)
                {
                    projects.Add(item.SubProject);
                    
                    if (IsSolutionFolderFast(item.SubProject))
                    {
                        List<Project> subProjects = GetProjectsFromProjectItemsFast(item.SubProject.ProjectItems);
                        projects.AddRange(subProjects);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.Log($"Exception in GetProjectsFromProjectItemsFast: {ex}", nameof(ProjectDiscoveryService));
            // Return what we have
        }
        
        return projects;
    }
}
