using System.Collections.Generic;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

public class ReferrerChainNode
{
    private ProjectModel _project;
    public ProjectModel Project 
    {
        get => _project;
        set
        {
            if(value.ProjectVersionChange != null)
            {
                NewVersion = value.ProjectVersionChange.NewVersion;                
            }

            _project = value;
        } 
    }
    public bool IsRoot { get; set; }
    public List<ReferrerChainNode> Referrers { get; set; } = new List<ReferrerChainNode>();
    public string NewVersion { get; set; } // Holds the new version if set via context menu
    public bool WasOriginallySelected { get; set; } // Indicates if this project was originally selected by the user

    public ReferrerChainNode(ProjectModel project, bool isRoot)
    {
        Project = project;
        IsRoot = isRoot;
        Referrers = new List<ReferrerChainNode>();
        WasOriginallySelected = false; // Initialize to false
    }
}
