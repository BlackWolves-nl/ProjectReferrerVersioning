using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Navigation;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI;

public partial class MainWindow
{
    private void InitializeAboutTab()
    {
        // The csproj <Version> drives both the assembly version and the VSIX manifest version
        // (see Update-VsixManifestVersion.ps1), so the assembly version is the extension version.
        Version version = Assembly.GetExecutingAssembly().GetName().Version;
        AboutVersionTextBlock.Text = version?.ToString() ?? "Unknown";
    }

    private void AboutHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebugHelper.ShowError("Could not open link: " + ex.Message, "AboutHyperlink");
        }

        e.Handled = true;
    }
}
