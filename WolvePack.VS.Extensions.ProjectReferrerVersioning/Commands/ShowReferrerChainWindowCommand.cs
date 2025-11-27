using System;
using System.Collections.Generic;
using System.ComponentModel.Design;

using EnvDTE;

using EnvDTE80;

using Microsoft.VisualStudio.Shell;

using Task = System.Threading.Tasks.Task;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Commands;

internal sealed class ShowReferrerChainWindowCommand
{
    public const int CommandIdSingle = 0x0100;
    public const int CommandIdExtension = 0x0200;
    public static readonly Guid CommandSet = new("84B4ECA1-1947-43B4-8B75-BA68C8216341");
    private readonly AsyncPackage _package;

    private readonly OleMenuCommand _menuItemSingle;
    private readonly OleMenuCommand _menuItemExtension;

    private ShowReferrerChainWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        this._package = package ?? throw new ArgumentNullException(nameof(package));
        
        CommandID menuCommandIDSingle = new(CommandSet, CommandIdSingle);
        _menuItemSingle = new OleMenuCommand(this.Execute, menuCommandIDSingle);
        _menuItemSingle.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
        commandService.AddCommand(_menuItemSingle);

        // Extension menu item
        CommandID menuCommandIDExtension = new(CommandSet, CommandIdExtension);
        _menuItemExtension = new OleMenuCommand(this.ExecuteExtensionMenu, menuCommandIDExtension);
        _menuItemExtension.BeforeQueryStatus += (sender, e) =>
        {
            OleMenuCommand cmd = sender as OleMenuCommand;
            cmd.Visible = true;
            cmd.Enabled = true;
            cmd.Supported = true;
        };
        commandService.AddCommand(_menuItemExtension);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        System.Diagnostics.Debug.WriteLine("[***WP***]Registering ShowReferrerChainWindowCommand");

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
        {
            System.Diagnostics.Debug.WriteLine("[***WP***]Got OleMenuCommandService, adding commands.");
            new ShowReferrerChainWindowCommand(package, commandService);
            System.Diagnostics.Debug.WriteLine("[***WP***]Commands added.");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[***WP***]Failed to get OleMenuCommandService.");
        }
    }

    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if(Package.GetGlobalService(typeof(DTE)) is not DTE2 dte)
            return;

        UIHierarchy solutionExplorer = dte.ToolWindows.SolutionExplorer;
        Array selectedItems = (Array)solutionExplorer.SelectedItems;
        List<Project> selectedProjects = new();
        foreach(UIHierarchyItem item in selectedItems)
        {
            if(item.Object is Project project && IsCSharpProject(project))
            {
                selectedProjects.Add(project);
            }
        }

        if(selectedProjects.Count > 0)
        {
            // Create and show the main window with pre-selected projects
            UI.MainWindow window = new(selectedProjects);
            window.Show();
        }
    }

    private bool IsCSharpProject(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return string.Equals(project.Kind, Models.ProjectKinds.CSharpProject, StringComparison.OrdinalIgnoreCase);
    }

    private void MenuItem_BeforeQueryStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        System.Diagnostics.Debug.WriteLine("[***WP***] MenuItem_BeforeQueryStatus called");

        if(sender is not OleMenuCommand menuCommand)
            return;

        if(Package.GetGlobalService(typeof(DTE)) is not DTE2 dte)
        {
            menuCommand.Visible = false;
            menuCommand.Enabled = false;
            return;
        }

        UIHierarchy solutionExplorer = dte.ToolWindows.SolutionExplorer;
        Array selectedItems = (Array)solutionExplorer.SelectedItems;
        int projectCount = 0;
        foreach(UIHierarchyItem item in selectedItems)
        {
            if(item.Object is Project project && IsCSharpProject(project))
            {
                projectCount++;
            }
        }

        int id = ((OleMenuCommand)sender).CommandID.ID;
        if(id == 0x0100)
        {
            menuCommand.Visible = projectCount == 1;
            menuCommand.Enabled = projectCount == 1;
        }
        else if(id == 0x0200)
        {
            menuCommand.Visible = true;
            menuCommand.Enabled = true;
        }
        else
        {
            menuCommand.Visible = false;
            menuCommand.Enabled = false;
        }
    }

    private void ExecuteExtensionMenu(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        
        if (Package.GetGlobalService(typeof(DTE)) is not DTE2)
            return;

        // Create and show the main window without pre-selected projects
        UI.MainWindow window = new();
        window.Show();
    }
}
