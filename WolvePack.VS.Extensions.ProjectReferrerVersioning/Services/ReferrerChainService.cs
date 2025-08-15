using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Services
{
    public static class ReferrerChainService
    {
        public static List<ReferrerChainNode> BuildReferrerChains(IEnumerable<ProjectModel> selectedProjects, bool minimizeChainDrawing = false)
        {
            List<ReferrerChainNode> result = new List<ReferrerChainNode>();
            HashSet<ProjectModel> visited = new HashSet<ProjectModel>();
            
            // Store the originally selected projects for root badge marking
            HashSet<ProjectModel> originallySelectedProjects = new HashSet<ProjectModel>(selectedProjects);
            
            // Build all chains first
            foreach (ProjectModel project in selectedProjects)
            {
                result.Add(BuildChainRecursive(project, true, visited));
            }

            // Mark all nodes that represent originally selected projects
            MarkOriginallySelectedProjects(result, originallySelectedProjects);

            // If minimize chain drawing is enabled, filter out chains where the root project
            // appears as a non-root node in other chains
            if (minimizeChainDrawing)
            {
                result = FilterMinimizedChains(result);
            }

            return result;
        }

        private static void MarkOriginallySelectedProjects(List<ReferrerChainNode> chains, HashSet<ProjectModel> originallySelectedProjects)
        {
            // Recursively mark all nodes that represent originally selected projects
            foreach (ReferrerChainNode chain in chains)
            {
                MarkOriginallySelectedProjectsRecursive(chain, originallySelectedProjects);
            }
        }

        private static void MarkOriginallySelectedProjectsRecursive(ReferrerChainNode node, HashSet<ProjectModel> originallySelectedProjects)
        {
            // Mark this node if it represents an originally selected project
            if (originallySelectedProjects.Contains(node.Project))
            {
                node.WasOriginallySelected = true; // Use new property instead of IsSelected
            }

            // Recursively mark children
            foreach (ReferrerChainNode child in node.Referrers)
            {
                MarkOriginallySelectedProjectsRecursive(child, originallySelectedProjects);
            }
        }

        private static List<ReferrerChainNode> FilterMinimizedChains(List<ReferrerChainNode> chains)
        {
            if (chains.Count <= 1)
                return chains; // No need to filter if we have 1 or fewer chains

            // Collect all selected projects from all chains
            HashSet<ProjectModel> allSelectedProjects = new HashSet<ProjectModel>();
            foreach (ReferrerChainNode chain in chains)
            {
                CollectAllProjectsInChain(chain, allSelectedProjects);
            }

            // Find the minimal set of chains that cover all selected projects
            return FindMinimalChainSet(chains, allSelectedProjects);
        }

        private static void CollectAllProjectsInChain(ReferrerChainNode node, HashSet<ProjectModel> projects)
        {
            projects.Add(node.Project);
            foreach (ReferrerChainNode child in node.Referrers)
            {
                CollectAllProjectsInChain(child, projects);
            }
        }

        private static List<ReferrerChainNode> FindMinimalChainSet(List<ReferrerChainNode> chains, HashSet<ProjectModel> allSelectedProjects)
        {
            // Create a mapping of which projects each chain covers
            Dictionary<ReferrerChainNode, HashSet<ProjectModel>> chainCoverage = new Dictionary<ReferrerChainNode, HashSet<ProjectModel>>();
            
            foreach (ReferrerChainNode chain in chains)
            {
                HashSet<ProjectModel> covered = new HashSet<ProjectModel>();
                CollectAllProjectsInChain(chain, covered);
                chainCoverage[chain] = covered;
            }

            // Find chains that are subsets of other chains and can be removed
            List<ReferrerChainNode> result = new List<ReferrerChainNode>();
            
            foreach (ReferrerChainNode chain in chains)
            {
                HashSet<ProjectModel> thisCoverage = chainCoverage[chain];
                
                // Check if this chain is a subset of any other chain
                bool isSubset = false;
                foreach (ReferrerChainNode otherChain in chains)
                {
                    if (chain == otherChain) continue;
                    
                    HashSet<ProjectModel> otherCoverage = chainCoverage[otherChain];
                    
                    // If this chain's coverage is a subset of another chain's coverage, skip it
                    if (thisCoverage.Count < otherCoverage.Count && thisCoverage.IsSubsetOf(otherCoverage))
                    {
                        isSubset = true;
                        break;
                    }
                }
                
                if (!isSubset)
                {
                    result.Add(chain);
                }
            }

            // If we filtered out all chains, return the original set to avoid empty result
            if (result.Count == 0)
                return chains;

            // After filtering, ensure version bumping logic accounts for originally selected projects
            // that may no longer be root nodes in the filtered chains
            EnsureOriginalRootsGetVersionBumps(result, allSelectedProjects);

            return result;
        }

        private static void EnsureOriginalRootsGetVersionBumps(List<ReferrerChainNode> filteredChains, HashSet<ProjectModel> allSelectedProjects)
        {
            // Find originally selected projects that are no longer root nodes in filtered chains
            HashSet<ProjectModel> currentRootProjects = new HashSet<ProjectModel>();
            foreach (ReferrerChainNode chain in filteredChains)
            {
                currentRootProjects.Add(chain.Project);
            }

            // For originally selected projects that are no longer roots, ensure they can still get version updates
            foreach (ReferrerChainNode chain in filteredChains)
            {
                EnsureOriginalRootsGetVersionBumpsRecursive(chain, allSelectedProjects, currentRootProjects);
            }
        }

        private static void EnsureOriginalRootsGetVersionBumpsRecursive(ReferrerChainNode node, HashSet<ProjectModel> allSelectedProjects, HashSet<ProjectModel> currentRootProjects)
        {
            // If this node represents an originally selected project but is no longer a root,
            // treat it as if it could be a root for version bump purposes
            if (allSelectedProjects.Contains(node.Project) && !currentRootProjects.Contains(node.Project))
            {
                // Intentionally left blank - flag already set elsewhere
            }

            foreach (ReferrerChainNode child in node.Referrers)
            {
                EnsureOriginalRootsGetVersionBumpsRecursive(child, allSelectedProjects, currentRootProjects);
            }
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
            // Only update if the project is not excluded from version updates and has version changes
            if (!node.Project.IsExcludedFromVersionUpdates && 
                node.Project.ProjectVersionChange == null && 
                !string.IsNullOrWhiteSpace(node.NewVersion) && 
                node.Project != null && 
                !string.IsNullOrWhiteSpace(node.Project.Version) && 
                node.NewVersion != node.Project.Version)
            {
                bool success = false;
                string oldVersion = node.Project.Version;
                string requestedVersion = node.NewVersion; // Could be 3-part in three-part mode
                bool threePart = Helpers.UserSettings.ActiveVersioningMode == Models.VersioningMode.ThreePart;
                string versionForWrite = requestedVersion;
                if (threePart && requestedVersion.Split('.').Length == 3)
                {
                    versionForWrite = requestedVersion + ".0"; // normalize to 4-part for assembly/file versions
                }

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
                            string assemblyVersionValue = versionForWrite; // always 4-part
                            text = System.Text.RegularExpressions.Regex.Replace(text, "\\[assembly:\\s*AssemblyVersion\\(\\\"[^\\\"]+\\\"\\)\\]", "[assembly: AssemblyVersion(\"" + assemblyVersionValue + "\")]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            text = System.Text.RegularExpressions.Regex.Replace(text, "\\[assembly:\\s*AssemblyFileVersion\\(\\\"[^\\\"]+\\\"\\)\\]", "[assembly: AssemblyFileVersion(\"" + assemblyVersionValue + "\")]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
                            if (assemblyVersion != null && assemblyVersion.Value != versionForWrite)
                            {
                                assemblyVersion.Value = versionForWrite; // always 4-part
                                changed = true;
                            }

                            XElement fileVersion = pg.Element("FileVersion");
                            if (fileVersion != null && fileVersion.Value != versionForWrite)
                            {
                                fileVersion.Value = versionForWrite; // always 4-part
                                changed = true;
                            }

                            XElement version = pg.Element("Version");
                            if (version != null)
                            {
                                string desiredProjectVersion = threePart ? (requestedVersion.Split('.').Length == 3 ? requestedVersion : versionForWrite) : versionForWrite;
                                if (version.Value != desiredProjectVersion)
                                {
                                    version.Value = desiredProjectVersion; // 3-part if three-part mode else 4-part
                                    changed = true;
                                }
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
                    result.Errors.Add(node.Project.Name + ": ERROR " + oldVersion + " -> " + versionForWrite + " (" + ex.Message + ")");
                }

                if (success)
                {
                    // Normalize in-memory versions to what was written (use 4-part to avoid mismatch logic)
                    node.Project.Version = versionForWrite;
                    node.NewVersion = versionForWrite;
                    result.Successes.Add(node.Project.Name + ": " + oldVersion + " -> " + versionForWrite);
                    progress?.Invoke("Updated " + node.Project.Name + ": " + oldVersion + " -> " + versionForWrite);
                }
            }
            else if (node.Project.IsExcludedFromVersionUpdates && !string.IsNullOrWhiteSpace(node.NewVersion))
            {
                // For excluded projects, just report that they were skipped but version was selected
                string oldVersion = node.Project.Version ?? "0.0.0.0";
                string newVersion = node.NewVersion;
                result.Successes.Add(node.Project.Name + ": " + oldVersion + " -> " + newVersion + " (EXCLUDED - version selected but not applied)");
                progress?.Invoke("Skipped " + node.Project.Name + ": " + oldVersion + " -> " + newVersion + " (excluded from updates)");
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
