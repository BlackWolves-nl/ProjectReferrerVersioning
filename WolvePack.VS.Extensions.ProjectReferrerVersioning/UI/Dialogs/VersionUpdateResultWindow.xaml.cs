using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI.Dialogs;

public partial class VersionUpdateResultWindow : Window
{
    private class ResultLine { public string Text { get; set; } public bool IsError { get; set; } }
    private readonly VersionUpdateResult _result;
    private List<ResultLine> _allLines;
    private bool _isLoaded;

    public VersionUpdateResultWindow(VersionUpdateResult result)
    {
        InitializeComponent();
        _result = result ?? new VersionUpdateResult();
        BuildLines();
        UpdateCounts();
        // Defer ApplyFilter until Loaded to avoid null control references from early Checked events
        ResultsItemsControl.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ApplyFilter();
    }

    private void ItemContainerGenerator_StatusChanged(object sender, System.EventArgs e)
    {
        if (ResultsItemsControl.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            for (int i = 0; i < ResultsItemsControl.Items.Count; i++)
            {
                object item = ResultsItemsControl.Items[i];
                ContentPresenter cont = (ContentPresenter)ResultsItemsControl.ItemContainerGenerator.ContainerFromIndex(i);
                if (cont == null) continue;
                TextBlock tb = FindDescendant<TextBlock>(cont);
                if (tb != null && item != null)
                {
                    bool isError = (bool)item.GetType().GetProperty("IsError").GetValue(item, null);
                    tb.Foreground = isError ? new SolidColorBrush(Color.FromRgb(229, 83, 83)) : new SolidColorBrush(Color.FromRgb(76, 175, 80));
                }
            }
        }
    }

    private static T FindDescendant<T>(DependencyObject root) 
        where T : DependencyObject
    {
        if (root == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            T deeper = FindDescendant<T>(child);
            if (deeper != null) return deeper;
        }

        return null;
    }

    private void BuildLines()
    {
        _allLines = new List<ResultLine>();
        foreach (string s in _result.Successes) _allLines.Add(new ResultLine { Text = s, IsError = false });
        foreach (string e in _result.Errors) _allLines.Add(new ResultLine { Text = e, IsError = true });
    }

    private void UpdateCounts()
    {
        CountsTextBlock.Text = $"Successes: {_result.Successes.Count}  |  Errors: {_result.Errors.Count}";
    }

    private void ApplyFilter()
    {
        if (!_isLoaded) return; // avoid running before Loaded
        bool showSuccesses = ShowSuccessesCheckBox?.IsChecked == true;
        bool showErrors = ShowErrorsCheckBox?.IsChecked == true;
        List<ResultLine> lines = _allLines.Where(l => (l.IsError && showErrors) || (!l.IsError && showSuccesses)).ToList();
        ResultsItemsControl.ItemsSource = lines.Select(l => new { l.Text, l.IsError }).ToList();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(string.Join(Environment.NewLine, _allLines.Select(l => l.Text)));
    }

    private void CopyErrors_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(string.Join(Environment.NewLine, _allLines.Where(l => l.IsError).Select(l => l.Text)));
    }

    private void CopySuccesses_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(string.Join(Environment.NewLine, _allLines.Where(l => !l.IsError).Select(l => l.Text)));
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
