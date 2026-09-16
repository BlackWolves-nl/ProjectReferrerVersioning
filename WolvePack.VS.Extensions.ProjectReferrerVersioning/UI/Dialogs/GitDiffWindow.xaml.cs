using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Services;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI.Dialogs;

/// <summary>
/// Modal showing the git diff of all changed files belonging to a single project.
/// </summary>
public partial class GitDiffWindow : Window
{
    public enum DiffLineKind { FileHeader, Meta, Hunk, Context, Added, Removed }

    public class DiffLine
    {
        public DiffLineKind Kind { get; set; }
        public string Text { get; set; }
        public string OldLineNumber { get; set; }
        public string NewLineNumber { get; set; }
    }

    public class DiffFile
    {
        public string Path { get; set; }
        public string FileName => System.IO.Path.GetFileName(Path);
        public string Directory => System.IO.Path.GetDirectoryName(Path);
        public int Added { get; set; }
        public int Removed { get; set; }
        public string AddedText => $"+{Added}";
        public string RemovedText => $"-{Removed}";
        public int HeaderIndex { get; set; }
    }

    private static readonly Regex _hunkHeaderRegex = new(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.Compiled);

    private readonly ProjectModel _project;
    private string _rawDiff = "";

    public GitDiffWindow(ProjectModel project)
    {
        InitializeComponent();
        _project = project;
        Title = $"Git Changes - {project?.Name}";
        TitleTextBlock.Text = project?.Name ?? "Git Changes";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _rawDiff = await GitService.GetProjectDiffAsync(_project);

            (List<DiffLine> lines, List<DiffFile> files) = ParseDiff(_rawDiff);
            DiffListBox.ItemsSource = lines;
            FilesListBox.ItemsSource = files;

            FileCountTextBlock.Text = files.Count == 1 ? "1 file" : $"{files.Count} files";
            AddedCountTextBlock.Text = $"+{files.Sum(f => f.Added)}";
            RemovedCountTextBlock.Text = $"-{files.Sum(f => f.Removed)}";

            if (lines.Count == 0)
            {
                StatusTextBlock.Text = "No changes found.";
            }
            else
            {
                StatusTextBlock.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to load diff: {ex.Message}";
        }
    }

    /// <summary>
    /// Parses unified diff output into display lines (with old/new line numbers) and a per-file summary.
    /// </summary>
    private static (List<DiffLine> Lines, List<DiffFile> Files) ParseDiff(string diff)
    {
        List<DiffLine> lines = new();
        List<DiffFile> files = new();
        if (string.IsNullOrWhiteSpace(diff)) return (lines, files);

        DiffFile currentFile = null;
        bool inHunk = false;
        int oldLine = 0;
        int newLine = 0;

        string[] rawLines = diff.Replace("\r\n", "\n").Split('\n');
        foreach (string rawLine in rawLines)
        {
            string text = rawLine.Replace("\t", "    ");

            if (rawLine.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                currentFile = new DiffFile { Path = ParseFilePath(rawLine), HeaderIndex = lines.Count };
                files.Add(currentFile);
                inHunk = false;
                lines.Add(new DiffLine { Kind = DiffLineKind.FileHeader, Text = currentFile.Path });
                continue;
            }

            Match hunkMatch = _hunkHeaderRegex.Match(rawLine);
            if (hunkMatch.Success)
            {
                inHunk = true;
                oldLine = int.Parse(hunkMatch.Groups[1].Value);
                newLine = int.Parse(hunkMatch.Groups[2].Value);
                lines.Add(new DiffLine { Kind = DiffLineKind.Hunk, Text = text });
                continue;
            }

            if (!inHunk)
            {
                if (rawLine.Length > 0) lines.Add(new DiffLine { Kind = DiffLineKind.Meta, Text = text });
                continue;
            }

            if (rawLine.StartsWith("+", StringComparison.Ordinal))
            {
                lines.Add(new DiffLine { Kind = DiffLineKind.Added, Text = text, NewLineNumber = newLine++.ToString() });
                if (currentFile != null) currentFile.Added++;
            }
            else if (rawLine.StartsWith("-", StringComparison.Ordinal))
            {
                lines.Add(new DiffLine { Kind = DiffLineKind.Removed, Text = text, OldLineNumber = oldLine++.ToString() });
                if (currentFile != null) currentFile.Removed++;
            }
            else if (rawLine.StartsWith("\\", StringComparison.Ordinal))
            {
                lines.Add(new DiffLine { Kind = DiffLineKind.Meta, Text = text });
            }
            else if (rawLine.StartsWith(" ", StringComparison.Ordinal))
            {
                lines.Add(new DiffLine { Kind = DiffLineKind.Context, Text = text, OldLineNumber = oldLine++.ToString(), NewLineNumber = newLine++.ToString() });
            }
        }

        return (lines, files);
    }

    /// <summary>
    /// Extracts the destination path from a "diff --git a/old b/new" header.
    /// </summary>
    private static string ParseFilePath(string header)
    {
        string rest = header.Substring("diff --git ".Length);
        int bIndex = rest.LastIndexOf(" b/", StringComparison.Ordinal);
        if (bIndex < 0) bIndex = rest.LastIndexOf(" \"b/", StringComparison.Ordinal);
        string path = bIndex >= 0 ? rest.Substring(bIndex + 1) : rest;
        path = path.Trim('"');
        if (path.StartsWith("b/", StringComparison.Ordinal)) path = path.Substring(2);
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    private void FilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilesListBox.SelectedItem is not DiffFile file) return;
        ScrollViewer scrollViewer = FindDescendant<ScrollViewer>(DiffListBox);
        // Item based scrolling (virtualized list): vertical offset equals item index
        scrollViewer?.ScrollToVerticalOffset(file.HeaderIndex);
    }

    private void DiffListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // SelectedItems is in selection order; emit in document order instead
            HashSet<DiffLine> selectedSet = new(DiffListBox.SelectedItems.Cast<DiffLine>());
            IEnumerable<DiffLine> source = DiffListBox.ItemsSource as IEnumerable<DiffLine> ?? Enumerable.Empty<DiffLine>();
            IEnumerable<DiffLine> selected = source.Where(selectedSet.Contains);
            string text = string.Join(Environment.NewLine, selected.Select(l => l.Text));
            if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
            e.Handled = true;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
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

    private void CopyDiff_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_rawDiff)) Clipboard.SetText(_rawDiff);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
