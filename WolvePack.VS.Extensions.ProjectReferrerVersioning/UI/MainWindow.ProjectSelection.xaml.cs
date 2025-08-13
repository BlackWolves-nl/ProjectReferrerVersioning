using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using EnvDTE;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.UI.Converters;

using static Microsoft.VisualStudio.Threading.AsyncReaderWriterLock;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI
{
    public partial class MainWindow
    {
        private ObservableCollection<ProjectModel> _allProjects;
        private ObservableCollection<ProjectModel> _filteredProjects;
        public ObservableCollection<ProjectModel> FilteredProjects
        {
            get => _filteredProjects;
            set => _filteredProjects = value;
        }
        private string _filterText = "";
        private bool _isAnalysisComplete = false;
        private void FillProjectSelectionLegendColors()
        {
            ProjectStatusToColorConverter converter = ProjectStatusToColorConverter.Instance;
            LegendSelectionInitialRect.Fill = (Brush)converter.Convert(ProjectStatus.Initial, typeof(Brush), null, null);
            LegendSelectionNugetRect.Fill = (Brush)converter.Convert(ProjectStatus.NuGetOrProjectReferenceChanges, typeof(Brush), null, null);
            LegendSelectionModifiedRect.Fill = (Brush)converter.Convert(ProjectStatus.Modified, typeof(Brush), null, null);
            LegendSelectionCleanRect.Fill = (Brush)converter.Convert(ProjectStatus.Clean, typeof(Brush), null, null);
            LegendSelectionNugetAndVersionRect.Fill = (Brush)converter.Convert(ProjectStatus.NuGetOrProjectReferenceAndVersionChanges, typeof(Brush), null, null);
            LegendSelectionVersionOnlyRect.Fill = (Brush)converter.Convert(ProjectStatus.IsVersionChangeOnly, typeof(Brush), null, null);
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredProjects != null)
            {
                foreach (ProjectModel project in _filteredProjects)
                {
                    project.IsSelected = true;
                }
            }
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredProjects != null)
            {
                foreach (ProjectModel project in _filteredProjects)
                {
                    project.IsSelected = false;
                }
            }
        }

        private void SelectModifiedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredProjects != null)
            {
                foreach (ProjectModel project in _filteredProjects)
                {
                    project.IsSelected = project.Status == ProjectStatus.Modified || 
                                       project.Status == ProjectStatus.NuGetOrProjectReferenceChanges;
                }
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filterText = ((TextBox)sender).Text ?? "";
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if(_allProjects == null) return;

            // Always ensure we're on the UI thread for this method
            if(Application.Current?.Dispatcher.CheckAccess() != true)
            {
                _ = Application.Current?.Dispatcher.BeginInvoke(new Action(ApplyFilter));
                return;
            }

            // Now we're safely on UI thread - can access UI elements
            List<ProjectModel> filtered = _allProjects.Where(p =>
                string.IsNullOrEmpty(_filterText) ||
                p.Name.ToLowerInvariant().Contains(_filterText.ToLowerInvariant())).ToList();

            // Update the filtered collection
            FilteredProjects.Clear();
            foreach(ProjectModel project in filtered)
            {
                FilteredProjects.Add(project);
            }
        }

        private async Task SetStatusTextAsync(string text)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            StatusTextBlock.Text = text;
        }

        private async Task LoadProjectsWithProgressAsync(List<Project> preSelectedProjects)
        {
            _isAnalysisComplete = false;
            UpdateGenerateButtonState();
            preSelectedProjects = preSelectedProjects ?? new List<Project>();
            try
            {
                await SetStatusTextAsync("Discovering projects...");
                List<ProjectModel> projectModels = await ProjectDiscoveryService.GetBasicSolutionProjectsAsync();
                await SetStatusTextAsync($"Found {projectModels.Count} projects, creating project list...");
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                InitializeProjectModels(projectModels, preSelectedProjects);
                ApplyFilter();
                UpdateStatusAndCounts();
                UpdateGenerateButtonState();

                await SetStatusTextAsync($"Ready - {projectModels.Count} projects loaded, analyzing Git status...");

                // Step 3: Background Git analysis with progress updates
                _ = AnalyzeGitStatusAsync(projectModels);
            }
            catch(Exception ex)
            {
                await SetStatusTextAsync($"Error loading projects: {ex.Message}");
                throw;
            }
        }

        private void InitializeProjectModels(List<ProjectModel> projectModels, List<Project> preSelectedProjects)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // Get current solution name for exclusions
            string solutionName = null;
            try
            {
                EnvDTE80.DTE2 dte = (EnvDTE80.DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE));
                solutionName = dte?.Solution?.FullName ?? "";
            }
            catch { solutionName = ""; }

            List<string> excluded = !string.IsNullOrEmpty(solutionName) && _userSettings.ExcludedProjectsBySolution != null && _userSettings.ExcludedProjectsBySolution.ContainsKey(solutionName)
                ? _userSettings.ExcludedProjectsBySolution[solutionName]
                : new List<string>();

            foreach(Project project in preSelectedProjects)
            {
                ProjectModel model = projectModels.FirstOrDefault(p => p.UniqueName == project.UniqueName);
                if(model != null)
                {
                    model.IsSelected = true;
                }
            }

            foreach(ProjectModel projectModel in projectModels)
            {
                projectModel.PropertyChanged += ProjectInfo_PropertyChanged;
                // Set exclusion state from settings
                projectModel.IsExcludedFromVersionUpdates = excluded.Contains(projectModel.Name);
            }

            _allProjects = new ObservableCollection<ProjectModel>(projectModels);
            FilteredProjects = new ObservableCollection<ProjectModel>(projectModels);
        }

        private async Task AnalyzeGitStatusAsync(List<ProjectModel> projectModels)
        {
            if (projectModels.Count == 0) return;

            // Step 1: Find git root once from solution location
            string solutionRepoRoot = await FindSolutionGitRootAsync();
            
            if (string.IsNullOrEmpty(solutionRepoRoot))
            {
                // No git repository found, set all projects to Initial status
                foreach (ProjectModel project in projectModels)
                {
                    project.Status = ProjectStatus.Initial;
                }

                await SetStatusTextAsync($"Ready - {projectModels.Count} projects loaded, no Git repository detected.");
                UpdateStatusAndCounts();
                ApplyFilter();
                _ = GenerateReferrersAsync();
                return;
            }

            await SetStatusTextAsync($"Ready - {projectModels.Count} projects loaded, analyzing Git status for repository...");

            try
            {
                // Step 2: Get all changed files in the repository once
                List<string> allChangedFiles = await GitService.GetAllChangedFilesInRepoAsync(solutionRepoRoot);
                
                await SetStatusTextAsync($"Ready - {projectModels.Count} projects loaded, analyzing {allChangedFiles.Count} changed files...");

                // Step 3: Analyze each project against the shared changed files list
                int analyzed = 0;
                foreach (ProjectModel project in projectModels)
                {
                    try
                    {
                        string analyzingText = $"Ready - {projectModels.Count} projects loaded, analyzing project {analyzed + 1}/{projectModels.Count}: {project.Name}";
                        await SetStatusTextAsync(analyzingText);

                        (ProjectStatus status, List<ReferenceChange> referenceChanges) = await GitService.AnalyzeProjectWithChangedFilesAsync(project, solutionRepoRoot, allChangedFiles);
                        project.Status = status;
                        project.ReferenceChanges = referenceChanges;
                    }
                    catch (Exception ex)
                    {
                        project.Status = ProjectStatus.Initial;
                        DebugHelper.Log($"Git analysis error for {project.Name}: {ex}", nameof(MainWindow));
                    }

                    analyzed++;
                }
            }
            catch (Exception ex)
            {
                // If git analysis fails entirely, set all to Initial
                foreach (ProjectModel project in projectModels)
                {
                    project.Status = ProjectStatus.Initial;
                }

                DebugHelper.Log($"Git analysis failed: {ex}", nameof(MainWindow));
            }

            await SetStatusTextAsync($"Ready - {projectModels.Count} projects loaded, Git analysis complete.");
            UpdateStatusAndCounts();
            ApplyFilter();

            // After git analysis is done, start referrer generation
            _ = GenerateReferrersAsync();
        }

        private async Task<string> FindSolutionGitRootAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            
            try
            {
                EnvDTE80.DTE2 dte = (EnvDTE80.DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE));
                string solutionPath = dte?.Solution?.FullName;
                
                DebugHelper.Log($"DTE Solution FullName: '{solutionPath}'", nameof(MainWindow));
                
                if (!string.IsNullOrEmpty(solutionPath))
                {
                    // Try primary method
                    string gitRoot = GitService.FindGitRootForSolutionOrProjectFile(solutionPath);
                    if (!string.IsNullOrEmpty(gitRoot))
                    {
                        DebugHelper.Log($"Found Git root from solution path: '{gitRoot}'", nameof(MainWindow));
                        return gitRoot;
                    }
                    
                    DebugHelper.Log($"No Git root found from solution path, trying fallback methods", nameof(MainWindow));
                    
                    // Fallback 1: Try from solution directory directly
                    string solutionDir = Path.GetDirectoryName(solutionPath);
                    DebugHelper.Log($"Solution directory: '{solutionDir}'", nameof(MainWindow));
                    
                    if (!string.IsNullOrEmpty(solutionDir) && Directory.Exists(solutionDir))
                    {
                        gitRoot = GitService.FindGitRootForSolutionOrProjectFile(Path.Combine(solutionDir, "dummy.txt"));
                        if (!string.IsNullOrEmpty(gitRoot))
                        {
                            DebugHelper.Log($"Found Git root from solution directory: '{gitRoot}'", nameof(MainWindow));
                            return gitRoot;
                        }
                    }
                    
                    // Fallback 2: Try finding any project and use its path
                    if (_allProjects != null && _allProjects.Count > 0)
                    {
                        ProjectModel firstProject = _allProjects.First();
                        if (!string.IsNullOrEmpty(firstProject.FileName))
                        {
                            DebugHelper.Log($"Trying Git root from first project: '{firstProject.FileName}'", nameof(MainWindow));
                            gitRoot = GitService.FindGitRootForSolutionOrProjectFile(firstProject.FileName);
                            if (!string.IsNullOrEmpty(gitRoot))
                            {
                                DebugHelper.Log($"Found Git root from project file: '{gitRoot}'", nameof(MainWindow));
                                return gitRoot;
                            }
                        }
                    }
                }
                else
                {
                    DebugHelper.Log($"Solution path is null or empty", nameof(MainWindow));
                    
                    // Fallback 3: Try current working directory
                    string currentDir = Environment.CurrentDirectory;
                    DebugHelper.Log($"Trying current directory: '{currentDir}'", nameof(MainWindow));
                    string gitRoot = GitService.FindGitRootForSolutionOrProjectFile(Path.Combine(currentDir, "dummy.txt"));
                    if (!string.IsNullOrEmpty(gitRoot))
                    {
                        DebugHelper.Log($"Found Git root from current directory: '{gitRoot}'", nameof(MainWindow));
                        return gitRoot;
                    }
                }
                
                DebugHelper.Log($"No Git repository found after trying all methods", nameof(MainWindow));
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"Error finding solution git root: {ex}", nameof(MainWindow));
            }

            return null;
        }

        private async Task GenerateReferrersAsync()
        {
            Dictionary<string, ProjectModel> nameToProject = _allProjects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            // Clear existing referrers
            foreach(ProjectModel project in _allProjects)
                project.Referrers.Clear();

            int total = _allProjects.Count;
            int processed = 0;

            foreach(ProjectModel project in _allProjects)
            {
                // Update status for the current project being processed
                string refStatus = $"Ready - {total} projects loaded, generating referrers...{project.Name}";
                await SetStatusTextAsync(refStatus);

                foreach(string referencedName in project.ProjectReferences)
                {
                    if(nameToProject.TryGetValue(referencedName, out ProjectModel referencedProject))
                    {
                        referencedProject.Referrers.Add(project);
                    }
                }

                processed++;
            }

            await SetStatusTextAsync($"Ready - {total} projects loaded, referrer generation complete.");
            _isAnalysisComplete = true;
            UpdateGenerateButtonState();
        }

        private void ProjectInfo_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(ProjectModel.IsSelected))
            {
                UpdateGenerateButtonState();
                UpdateStatusAndCounts();
            }
        }

        private void UpdateStatusAndCounts()
        {
            if(_allProjects != null)
            {
                int total = _allProjects.Count;
                int selected = _allProjects.Count(p => p.IsSelected);
                int clean = _allProjects.Count(p => p.Status == ProjectStatus.Clean);
                int modified = _allProjects.Count(p => p.Status == ProjectStatus.Modified);
                int nugetOnly = _allProjects.Count(p => p.Status == ProjectStatus.NuGetOrProjectReferenceChanges);

                ProjectCountText.Text = $"Projects: {selected}/{total} selected | Clean: {clean} | Modified: {modified} | NuGet/Refs: {nugetOnly}";
            }
        }

        private void UpdateGenerateButtonState()
        {
            if(_allProjects != null)
            {
                GenerateButton.IsEnabled = _isAnalysisComplete && _allProjects.Any(p => p.IsSelected);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all caches when refreshing

            _ = InitializeWindowAsync(null); // Refresh without pre-selection
        }

        private void ExcludedCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (sender is CheckBox checkBox && checkBox.DataContext is ProjectModel project)
            {
                // Get current solution name (same as used in ExtractAllProjectDataQuickly)
                string solutionName;
                try
                {
                    EnvDTE80.DTE2 dte = (EnvDTE80.DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE));
                    solutionName = dte?.Solution?.FullName ?? "";
                }
                catch { solutionName = ""; }

                if (string.IsNullOrEmpty(solutionName))
                    return;

                // Toggle exclusion
                project.IsExcludedFromVersionUpdates = checkBox.IsChecked == true;

                // Update settings
                if (_userSettings.ExcludedProjectsBySolution == null)
                    _userSettings.ExcludedProjectsBySolution = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();

                List<string> excludedList = _userSettings.ExcludedProjectsBySolution.ContainsKey(solutionName)
                    ? _userSettings.ExcludedProjectsBySolution[solutionName]
                    : (_userSettings.ExcludedProjectsBySolution[solutionName] = new System.Collections.Generic.List<string>());

                if (project.IsExcludedFromVersionUpdates)
                {
                    if (!excludedList.Contains(project.Name))
                        excludedList.Add(project.Name);
                }
                else
                {
                    excludedList.Remove(project.Name);
                }

                _userSettings.Save();
                ApplyFilter(); // Refresh UI
            }
        }
    }
}
