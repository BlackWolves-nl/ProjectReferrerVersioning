using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers
{
    public class UserSettings
    {
        public string DefaultTheme { get; set; } = "Dark";
        public string DefaultLayout { get; set; } = "Standard (Tree)";
        public bool DebugEnabled { get; set; } = false;

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
                    return JsonConvert.DeserializeObject<UserSettings>(json) ?? new UserSettings();
                }
            }
            catch { }

            return new UserSettings();
        }

        public void Save()
        {
            try
            {
                if(!Directory.Exists(SettingsDirectory))
                    Directory.CreateDirectory(SettingsDirectory);
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}
