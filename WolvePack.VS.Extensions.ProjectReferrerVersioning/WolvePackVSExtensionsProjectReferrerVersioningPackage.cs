using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

using Task = System.Threading.Tasks.Task;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("WolvePack.VS.Extensions.ProjectReferrerVersioning", "Displays project Referrer chains and automatic versioning", "2.5.0.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid("B3168E51-9239-4B33-B1AF-E40366A31044")] // MUST match .vsct and .vsixmanifest
    public sealed class WolvePackVSProjectReferrerVersioningPackage : AsyncPackage, IAsyncLoadablePackageInitialize
    {
        public const string PackageGuidString = "B3168E51-9239-4B33-B1AF-E40366A31044";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            System.Diagnostics.Debug.WriteLine("WolvePackVSProjectReferrerVersioningPackage: InitializeAsync started.");

            // Must switch to the main thread before accessing UI-thread-bound code
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            System.Diagnostics.Debug.WriteLine("[***WP***]WolvePackVSProjectReferrerVersioningPackage: Switched to main thread.");
            await Commands.ShowReferrerChainWindowCommand.InitializeAsync(this);
            System.Diagnostics.Debug.WriteLine("[***WP***]WolvePackVSProjectReferrerVersioningPackage: ShowReferrerChainWindowCommand.InitializeAsync completed.");
        }
    }
}