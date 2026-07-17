using System;
using System.Runtime.InteropServices;
using System.Threading;
using Base44AIPilot.Commands;
using Base44AIPilot.Options;
using Base44AIPilot.ToolWindows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Base44AIPilot
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(Base44ChatWindow),
        Style = VsDockStyle.Tabbed,
        Window = "DocumentWell",
        Orientation = ToolWindowOrientation.Right)]
    [ProvideOptionPage(typeof(Base44OptionsPage), "Base44 AI Pilot", "General", 0, 0, true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class Base44AIPilotPackage : AsyncPackage
    {
        public static Base44AIPilotPackage Instance { get; private set; }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Initialize commands on background thread — GetServiceAsync is
            // background-safe. Commands will switch to UI thread internally
            // when their handlers fire.
            Instance = this;
            await Base44AICommands.InitializeAsync(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Instance = null;
            base.Dispose(disposing);
        }
    }
}
