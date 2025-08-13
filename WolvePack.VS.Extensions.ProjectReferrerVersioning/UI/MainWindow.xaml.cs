using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using EnvDTE;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

using Window = System.Windows.Window;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI
{
    public partial class MainWindow : Window
    {
        public List<ReferrerChainNode> LastGeneratedChains => _lastGeneratedChains;
        public IReferrerChainDrawingService DrawingService => _drawingService;
        public System.Windows.Controls.Canvas ReferrerTreeCanvasPublic => ReferrerTreeCanvas;

        private List<ReferrerChainNode> _lastGeneratedChains;
        private ReferrerChainTheme _currentTheme;
        private IReferrerChainDrawingService _drawingService;

        public MainWindow(List<Project> preSelectedProjects = null)
        {
            InitializeComponent();
            _userSettings = UserSettings.Load();
            ApplyUserSettingsToUI();

            // Ensure DataContext is set for DataGrid binding
            this.DataContext = this;

            // Add UI refresh timer for better responsiveness
            System.Windows.Threading.DispatcherTimer refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            refreshTimer.Tick += (s, e) =>
            {
                // Force UI refresh periodically
                ProjectItemsGrid?.UpdateLayout();
            };
            refreshTimer.Start();

            _ = InitializeWindowAsync(preSelectedProjects);
        }

        private async Task InitializeWindowAsync(List<Project> preSelectedProjects)
        {
            try
            {
                StatusTextBlock.Text = "Loading projects...";

                // Load projects asynchronously with progress reporting
                await LoadProjectsWithProgressAsync(preSelectedProjects);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading projects: {ex.Message}";
                // Reduce debug noise in release builds
#if DEBUG
                DebugHelper.ShowError($"InitializeWindow error: {ex.Message}", "InitializeWindow");
#endif
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnAllRootNodesUpdated()
        {
            if (UpdateVersionsButton != null)
                UpdateVersionsButton.IsEnabled = true;
        }

        private void SetDrawingService(IReferrerChainDrawingService drawingService)
        {
            if (_drawingService != null)
            {
                _drawingService.AllRootNodesUpdated -= OnAllRootNodesUpdated;
            }

            _drawingService = drawingService;
            if (_drawingService != null)
            {
                _drawingService.Theme = _currentTheme; // Ensure theme is set
                ReferrerTreeCanvas.Background = _currentTheme.BackgroundBrush;
                drawingService.AllRootNodesUpdated += OnAllRootNodesUpdated;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply settings to main UI theme/layout on load
            ApplyUserSettingsToUI();
            SetThemeFromSettings();
            SetLayoutFromSettings(); // This will set the correct drawing service based on settings
            CanvasZoomHelper.Attach(ReferrerTreeCanvas); // Enable zoom only
            UpdateTreeOutputLegendColors(_drawingService.Theme); // Ensure legend is updated after drawing
            FillProjectSelectionLegendColors();
        }
    }
}