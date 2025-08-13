using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    public class VersionUpdateResult
    {
        public List<string> Successes { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public string Message => string.Join("\n", Successes.Concat(Errors));
    }

    public static class ReferrerChainService
    {
        public static List<ReferrerChainNode> BuildReferrerChains(IEnumerable<ProjectModel> selectedProjects)
        {
            List<ReferrerChainNode> result = new List<ReferrerChainNode>();
            HashSet<ProjectModel> visited = new HashSet<ProjectModel>();
            foreach (ProjectModel project in selectedProjects)
            {
                result.Add(BuildChainRecursive(project, true, visited));
            }

            return result;
        }

        private static ReferrerChainNode BuildChainRecursive(ProjectModel project, bool isRoot, HashSet<ProjectModel> visited)
        {
            ReferrerChainNode node = new ReferrerChainNode(project, isRoot);
            if (visited.Contains(project))
                return node; // Prevent infinite cycles, but still include the node
            visited.Add(project);
            foreach (ProjectModel referrer in project.Referrers)
            {
                node.Referrers.Add(BuildChainRecursive(referrer, false, visited));
            }

            visited.Remove(project);
            return node;
        }

        public static async Task<VersionUpdateResult> UpdateVersionsAsync(IEnumerable<ReferrerChainNode> chains, Action<string> progress = null)
        {
            VersionUpdateResult result = new VersionUpdateResult();
            foreach (ReferrerChainNode chain in chains ?? Enumerable.Empty<ReferrerChainNode>())
            {
                await UpdateNodeVersionRecursiveAsync(chain, result, progress);
            }

            return result;
        }

        private static async Task UpdateNodeVersionRecursiveAsync(ReferrerChainNode node, VersionUpdateResult result, Action<string> progress)
        {
            if (node.Project.ProjectVersionChange == null && !string.IsNullOrWhiteSpace(node.NewVersion) && node.Project != null && !string.IsNullOrWhiteSpace(node.Project.Version) && node.NewVersion != node.Project.Version)
            {
                bool success = false;
                string oldVersion = node.Project.Version;
                string newVersion = node.NewVersion;
                try
                {
                    // Update AssemblyInfo.cs
                    string projectDir = Path.GetDirectoryName(node.Project.FullName);
                    if (!string.IsNullOrEmpty(projectDir))
                    {
                        string assemblyInfoPath = Path.Combine(projectDir, "Properties", "AssemblyInfo.cs");
                        if (File.Exists(assemblyInfoPath))
                        {
                            string text = File.ReadAllText(assemblyInfoPath);
                            text = System.Text.RegularExpressions.Regex.Replace(text, "\\[assembly:\\s*AssemblyVersion\\(\\\"[^\\\"]+\\\"\\)\\]", "[assembly: AssemblyVersion(\"" + newVersion + "\")]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            text = System.Text.RegularExpressions.Regex.Replace(text, "\\[assembly:\\s*AssemblyFileVersion\\(\\\"[^\\\"]+\\\"\\)\\]", "[assembly: AssemblyFileVersion(\"" + newVersion + "\")]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            File.WriteAllText(assemblyInfoPath, text);
                            success = true;
                        }
                    }
                    // Update .csproj Version properties using XML
                    if (!string.IsNullOrEmpty(node.Project.FileName) && File.Exists(node.Project.FileName))
                    {
                        XDocument doc = XDocument.Load(node.Project.FileName);
                        IEnumerable<XElement> propertyGroups = doc.Descendants("PropertyGroup");
                        bool changed = false;
                        foreach (XElement pg in propertyGroups)
                        {
                            XElement assemblyVersion = pg.Element("AssemblyVersion");
                            if (assemblyVersion != null && assemblyVersion.Value != newVersion)
                            {
                                assemblyVersion.Value = newVersion;
                                changed = true;
                            }

                            XElement fileVersion = pg.Element("FileVersion");
                            if (fileVersion != null && fileVersion.Value != newVersion)
                            {
                                fileVersion.Value = newVersion;
                                changed = true;
                            }

                            XElement version = pg.Element("Version");
                            if (version != null && version.Value != newVersion)
                            {
                                version.Value = newVersion;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            doc.Save(node.Project.FileName);
                            success = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(node.Project.Name + ": ERROR " + oldVersion + " -> " + newVersion + " (" + ex.Message + ")");
                }

                if (success)
                {
                    result.Successes.Add(node.Project.Name + ": " + oldVersion + " -> " + newVersion);
                    progress?.Invoke("Updated " + node.Project.Name + ": " + oldVersion + " -> " + newVersion);
                }
            }

            if (node.Referrers != null)
            {
                foreach (ReferrerChainNode child in node.Referrers)
                {
                    await UpdateNodeVersionRecursiveAsync(child, result, progress);
                }
            }
        }
    }
}
