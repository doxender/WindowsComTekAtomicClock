// ComTekAtomicClock.UI.Services.AppInterTabClient
//
// Dragablz IInterTabClient implementation that powers tab tear-away.
// When the user drags a tab out of the strip, Dragablz calls
// GetNewHost; we return a fresh FloatingClockWindow whose own
// TabablzControl receives the dragged tab. When the LAST tab leaves
// a window, TabEmptiedHandler returns CloseWindowOrLayoutBranch so
// the empty window self-disposes.
//
// All TabablzControls in the app (MainWindow.MainTabs and
// FloatingClockWindow.FloatingTabs) share the same default partition,
// so tabs can be dragged freely between any of them — main->floating,
// floating->main, floating->floating.

using System.Runtime.Versioning;
using System.Windows;
using Dragablz;

namespace ComTekAtomicClock.UI.Services;

[SupportedOSPlatform("windows")]
public sealed class AppInterTabClient : IInterTabClient
{
    public INewTabHost<Window> GetNewHost(
        IInterTabClient interTabClient,
        object partition,
        TabablzControl source)
    {
        var window = new FloatingClockWindow();
        return new NewTabHost<Window>(window, window.FloatingTabs);
    }

    public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
    {
        // Last tab dragged out -> close this window. If user closes
        // a window WITH a tab still in it, Dragablz's
        // ConsolidateOrphanedItems="True" on the remaining
        // TabablzControls will reattach the orphan to the main strip.
        return TabEmptiedResponse.CloseWindowOrLayoutBranch;
    }
}
