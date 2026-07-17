using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using System.Windows;
using Base44AIPilot.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Base44AIPilot.Commands
{
    internal sealed class Base44AICommands
    {
        public const int CmdAskAI           = 0x0100;
        public const int CmdAnalyzeSolution = 0x0101;
        public const int CmdGenerateFeature = 0x0102;
        public const int CmdRefactor        = 0x0103;
        public const int CmdExplainFlow     = 0x0104;
        public const int CmdSettings        = 0x0105;

        public static readonly Guid CommandSet = new Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901");

        private readonly AsyncPackage _package;

        private Base44AICommands(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package       = package       ?? throw new ArgumentNullException("package");
            commandService = commandService ?? throw new ArgumentNullException("commandService");

            RegisterCommand(commandService, CmdAskAI,           OnAskAI);
            RegisterCommand(commandService, CmdAnalyzeSolution, OnAnalyzeSolution);
            RegisterCommand(commandService, CmdGenerateFeature, OnGenerateFeature);
            RegisterCommand(commandService, CmdRefactor,        OnRefactor);
            RegisterCommand(commandService, CmdExplainFlow,     OnExplainFlow);
            RegisterCommand(commandService, CmdSettings,        OnSettings);
        }

        private static void RegisterCommand(OleMenuCommandService svc, int cmdId, EventHandler handler)
        {
            svc.AddCommand(new MenuCommand(handler, new CommandID(CommandSet, cmdId)));
        }

        private void OnAskAI(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OpenChat("answer_code_qa", null, null);
        }

        private void OnAnalyzeSolution(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OpenChat("analyze_solution",
                "Perform a complete analysis of this .NET MVC solution: architecture pattern, " +
                "file inventory, and at least 3 actionable recommendations with file-path citations.",
                null);
        }

        private void OnGenerateFeature(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OpenChat("generate_feature", null,
                "Describe the feature to generate (e.g. 'Product CRUD with Category and Price fields'):");
        }

        private void OnRefactor(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OpenChat("refactor_code", null,
                "Describe what to refactor (e.g. 'Extract business logic from HomeController into a ProductService'):");
        }

        private void OnExplainFlow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OpenChat("explain_flow", null,
                "What flow do you want explained? (e.g. 'How does a POST to /Products/Create flow through the system?'):");
        }

        private void OnSettings(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package.ShowOptionPage(typeof(Options.Base44OptionsPage));
        }

        private void OpenChat(string intent, string autoPrompt, string promptHint)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var window = _package.FindToolWindow(typeof(Base44ChatWindow), 0, true) as Base44ChatWindow;
            if (window?.Frame == null) return;
            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
            (window.Content as Base44ChatWindowControl)?.SetIntent(intent, autoPrompt, promptHint);
        }

        // InitializeAsync: runs on background thread during package init.
        // GetServiceAsync is background-safe. We dispatch registration to
        // the UI thread via Application.Current.Dispatcher so we never need
        // to reference Microsoft.VisualStudio.Threading explicitly.
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var commandServiceObj = await package.GetServiceAsync(typeof(IMenuCommandService));

            Application.Current.Dispatcher.Invoke(() =>
            {
                var commandService = commandServiceObj as OleMenuCommandService;
                if (commandService == null)
                    return;
                new Base44AICommands(package, commandService);
            });
        }
    }
}
