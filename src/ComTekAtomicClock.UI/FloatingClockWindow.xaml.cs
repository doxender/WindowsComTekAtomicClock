// ComTekAtomicClock.UI.FloatingClockWindow
//
// Host for tabs torn off the main strip. Created by AppInterTabClient
// when Dragablz fires GetNewHost. The window's TabablzControl is
// exposed publicly as FloatingTabs so the IInterTabClient can return
// a reference to it via NewTabHost<Window>.
//
// Per-tab settings (timezone, theme) are reachable from a torn-off
// window the same way as the main window: right-click the tab header
// or double-click it. Wiring those gestures here would require
// duplicating the MainWindow handlers — deferred. For now, drag the
// tab back to the main strip if you need to edit it.

using System.Runtime.Versioning;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI;

[SupportedOSPlatform("windows")]
public partial class FloatingClockWindow : FluentWindow
{
    public FloatingClockWindow()
    {
        InitializeComponent();
    }
}
