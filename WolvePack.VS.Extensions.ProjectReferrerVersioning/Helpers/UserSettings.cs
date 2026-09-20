using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;

public class UserSettings
{
    public string DefaultTheme { get; set; } = "Dark";
    public string DefaultLayout { get; set; } = "Standard (Tree)";
    public bool DebugEnabled { get; set; } = false;
    public bool MinimizeChainDrawing { get; set; } = false;
    public bool HideSubsequentVisits { get; set; } = false;
    public VersioningMode VersioningMode { get; set; } = VersioningMode.FourPart;
    /// <summary>When true, Git analysis compares against the branch's upstream so unpushed commits count as changes.</summary>
    public bool IncludeUnpushedCommits { get; set; } = true;

    public static VersioningMode ActiveVersioningMode { get; set; } = VersioningMode.FourPart;
    /// <summary>Current <see cref="IncludeUnpushedCommits"/> value, readable from services without a settings instance.</summary>
    public static bool ActiveIncludeUnpushedCommits { get; set; } = true;

    // SolutionName -> List of excluded project names
    public Dictionary<string, List<string>> ExcludedProjectsBySolution { get; set; } = new Dictionary<string, List<string>>();

    private static string SettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WolvePack.ProjectReferrerVersioning");
    private static string SettingsFilePath => Path.Combine(SettingsDirectory, "Settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                UserSettings settings = JsonConvert.DeserializeObject<UserSettings>(json) ?? new UserSettings();
                ActiveVersioningMode = settings.VersioningMode;
                ActiveIncludeUnpushedCommits = settings.IncludeUnpushedCommits;
                return settings;
            }
        }
        catch { }

        ActiveVersioningMode = VersioningMode.FourPart;
        ActiveIncludeUnpushedCommits = true;
        return new UserSettings();
    }

    public void Save()
    {
        try
        {
            ActiveVersioningMode = VersioningMode;
            ActiveIncludeUnpushedCommits = IncludeUnpushedCommits;
            if(!Directory.Exists(SettingsDirectory))
                Directory.CreateDirectory(SettingsDirectory);
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
}
