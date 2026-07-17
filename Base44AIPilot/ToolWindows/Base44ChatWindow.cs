using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Base44AIPilot.ToolWindows
{
    [Guid("D4E5F6A7-B8C9-0123-DEFA-234567890123")]
    public class Base44ChatWindow : ToolWindowPane
    {
        public Base44ChatWindow() : base(null)
        {
            Caption = "Base44 AI Pilot";
            Content = new Base44ChatWindowControl();
        }

        public void SetIntent(string intent, string autoPrompt, string promptHint)
        {
            var ctrl = Content as Base44ChatWindowControl;
            if (ctrl != null)
                ctrl.SetIntent(intent, autoPrompt, promptHint);
        }
    }
}
