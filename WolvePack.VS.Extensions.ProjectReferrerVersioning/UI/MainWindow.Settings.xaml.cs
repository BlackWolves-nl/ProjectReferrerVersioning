using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; // <-- Add this for VisualTreeHelper
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI;

public partial class MainWindow
{
    private readonly UserSettings _userSettings;

    private void ApplyUserSettingsToUI()
    {
        // Set default theme ComboBox
        foreach (ComboBoxItem item in DefaultThemeComboBox.Items)
        {
            if (item.Content.ToString() == _userSettings.DefaultTheme)
            {
                DefaultThemeComboBox.SelectedItem = item;
                break;
            }
        }
        // Set default layout ComboBox
        foreach (ComboBoxItem item in DefaultLayoutComboBox.Items)
        {
            if (item.Content.ToString() == _userSettings.DefaultLayout)
            {
                DefaultLayoutComboBox.SelectedItem = item;
                break;
            }
        }
        // Set debug enabled checkbox
        DebugEnabledCheckBox.IsChecked = _userSettings.DebugEnabled;
        
        // Set minimize chain drawing checkbox
        MinimizeChainDrawingCheckBox.IsChecked = _userSettings.MinimizeChainDrawing;
        // Set hide subsequent visits (persisted setting)
        HideSubsequentVisitsCheckBox.IsChecked = _userSettings.HideSubsequentVisits;
        HideVisitedCheckBox.IsChecked = _userSettings.HideSubsequentVisits; // initial sync for tree tab

        // Set versioning mode
        foreach(ComboBoxItem item in VersioningModeComboBox.Items)
        {
            if((string)item.Tag == _userSettings.VersioningMode.ToString())
            {
                VersioningModeComboBox.SelectedItem = item;
                break;
            }
        }
        // Apply debug setting to DebugHelper
        DebugHelper.DebugEnabled = _userSettings.DebugEnabled;
        UserSettings.ActiveVersioningMode = _userSettings.VersioningMode;
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _userSettings.DefaultTheme = (DefaultThemeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Dark";
        _userSettings.DefaultLayout = (DefaultLayoutComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Standard (Tree)";
        _userSettings.DebugEnabled = DebugEnabledCheckBox.IsChecked ?? false;
        _userSettings.MinimizeChainDrawing = MinimizeChainDrawingCheckBox.IsChecked ?? false;
        _userSettings.HideSubsequentVisits = (HideSubsequentVisitsCheckBox.IsChecked ?? false);
        if (HideVisitedCheckBox != null)
        {
            if (_drawingService is ReferrerChainDrawingServiceBase baseSvc)
                baseSvc.HideSubsequentVisits = _userSettings.HideSubsequentVisits;
            HideVisitedCheckBox.IsChecked = _userSettings.HideSubsequentVisits; // reflect persisted value
        }

        if(VersioningModeComboBox.SelectedItem is ComboBoxItem vmItem && vmItem.Tag is string tag)
        {
            if(Enum.TryParse(tag, out VersioningMode mode))
                _userSettings.VersioningMode = mode;
        }
        
        // Apply debug setting to DebugHelper immediately
        DebugHelper.DebugEnabled = _userSettings.DebugEnabled;
        
        _userSettings.Save();
        SettingsStatusTextBlock.Text = "Settings saved.";
        SetThemeFromSettings();
        
        // Regenerate tree with new minimize chain drawing setting if tree exists
        if (_lastGeneratedChains != null && _allProjects != null)
        {
            System.Collections.Generic.List<ProjectModel> selectedProjects = _allProjects.Where(p => p.IsSelected).ToList();
            if (selectedProjects.Count > 0)
            {
                _lastGeneratedChains = ReferrerChainService.BuildReferrerChains(selectedProjects, _userSettings.MinimizeChainDrawing);
                ReferrerTreeCanvas.Children.Clear();
                _drawingService.DrawChainsBase(ReferrerTreeCanvas, _lastGeneratedChains);
            }
        }
        // Redraw the tree with the new theme if possible
        else if (_drawingService != null && ReferrerTreeCanvas != null && _drawingService is ReferrerChainDrawingServiceBase baseService && baseService.LastRoots != null)
        {
            _drawingService.Theme = _currentTheme;
            _drawingService.DrawChainsBase(ReferrerTreeCanvas, baseService.LastRoots);
        }
    }

    private void DebugEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        bool debugEnabled = DebugEnabledCheckBox.IsChecked ?? false;
        DebugHelper.DebugEnabled = debugEnabled;
        DebugHelper.Log($"Debug logging {(debugEnabled ? "enabled" : "disabled")} via Settings UI", "Settings");
    }

    private void RefreshThemeResources(DependencyObject parent)
    {
        if (parent == null) return;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe)
            {
                if (fe.ReadLocalValue(FrameworkElement.StyleProperty) != DependencyProperty.UnsetValue)
                {
                    Style style = fe.Style;
                    fe.Style = null;
                    fe.Style = style;
                }

                fe.Resources = fe.Resources;
                fe.InvalidateVisual();
                fe.InvalidateProperty(FrameworkElement.StyleProperty);
                if (fe is Control ctrl)
                    ctrl.ApplyTemplate();
            }

            RefreshThemeResources(child);
        }
    }

    private void SetThemeFromSettings()
    {
        System.Collections.Generic.List<ResourceDictionary> themeDicts = Application.Current.Resources.MergedDictionaries
            .Where(d => d.Source != null && d.Source.OriginalString.Contains("Theme.")).ToList();
        foreach (ResourceDictionary dict in themeDicts)
            Application.Current.Resources.MergedDictionaries.Remove(dict);

        System.Collections.Generic.List<ResourceDictionary> localThemeDicts = this.Resources.MergedDictionaries
            .Where(d => d.Source != null && d.Source.OriginalString.Contains("Theme.")).ToList();
        foreach (ResourceDictionary dict in localThemeDicts)
            this.Resources.MergedDictionaries.Remove(dict);

        string themeName = _userSettings.DefaultTheme ?? "Dark";
        ResourceDictionary newDict = new()
        {
            Source = new Uri($"pack://application:,,,/WolvePack.VS.Extensions.ProjectReferrerVersioning;component/UI/Themes/Theme.{themeName}.xaml", UriKind.Absolute)
        };
        Application.Current.Resources.MergedDictionaries.Add(newDict);
        this.Resources.MergedDictionaries.Add(newDict);

        _currentTheme = ReferrerChainTheme.LoadThemeFromResources();

        if (_drawingService != null)
        {
            _drawingService.Theme = _currentTheme;
            ReferrerTreeCanvas.Background = _currentTheme.BackgroundBrush;
            UpdateTreeOutputLegendColors(_currentTheme);
        }

        RefreshThemeResources(this);
    }

    private void SetLayoutFromSettings()
    {
        foreach (ComboBoxItem item in LayoutComboBox.Items)
        {
            if (item.Content.ToString() == _userSettings.DefaultLayout)
            {
                LayoutComboBox.SelectedItem = item;
                break;
            }
        }

        switch (_userSettings.DefaultLayout)
        {
            case "Compact Horizontal":
                SetDrawingService(new CompactHorizontalOverlapReferrerChainDrawingService(_currentTheme));
                break;
            case "Compact Vertical":
                SetDrawingService(new CompactVerticalOverlapReferrerChainDrawingService(_currentTheme));
                break;
            case "Standard (Tree)":
            default:
                SetDrawingService(new StandardReferrerChainDrawingService(_currentTheme));
                break;
        }
    }
}
