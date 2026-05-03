// ComTekAtomicClock.UI.FloatingClockWindow
//
// v0.0.33: a free-floating clock window hosting ONE clock face. The
// previous (v0.0.14..v0.0.32) version hosted a Dragablz TabablzControl
// to receive torn-off tabs and re-tear them; with tear-away removed,
// this window's role is purely "this clock lives on my desktop, not
// in the main window's tab strip." The DataContext is the single
// TabViewModel for this clock, set by the caller via the constructor.
//
// Migration is bidirectional but explicit:
//   · Main window → floating: right-click tab → "Open in new window"
//     (handled in MainWindow.TabContextOpenInNewWindow_Click).
//   · Floating → main window: this window's "?" overlay menu →
//     "Bring back into tabs" (handler below).
//
// The window does NOT register itself for settings persistence; the
// owning MainWindowViewModel tracks open floating windows in memory
// and re-attaches them to the Tabs collection on close + persists.
// Window-position persistence (X / Y / Width / Height across restarts)
// is on the Phase-2 magnetic-snap todo list.

using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ComTekAtomicClock.UI.Dialogs;
using ComTekAtomicClock.UI.ViewModels;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI;

[SupportedOSPlatform("windows")]
public partial class FloatingClockWindow : FluentWindow
{
    private readonly TabViewModel _tab;
    private MainWindowViewModel? _subscribedMainVm;

    /// <summary>
    /// Constructs a floating clock window bound to <paramref name="tab"/>.
    /// </summary>
    public FloatingClockWindow(TabViewModel tab)
    {
        _tab = tab ?? throw new ArgumentNullException(nameof(tab));
        InitializeComponent();
        DataContext = _tab;

        // v0.0.36: pull the TimeSourceLabel / TimeSourceBadge from the
        // main window's view-model and subscribe to its PropertyChanged
        // so the floating window's clock face refreshes when the user
        // changes time source via the Settings dialog. The DataContext
        // here is the TabViewModel (per-clock state), so a normal
        // {Binding} can't reach the app-global TimeSource — wire it
        // through code-behind instead.
        var mainVm = (Application.Current?.MainWindow as MainWindow)?.GetViewModel();
        if (mainVm is not null)
        {
            _subscribedMainVm = mainVm;
            mainVm.PropertyChanged += OnMainVmPropertyChanged;
            ApplyTimeSourceFromVm(mainVm);
        }
        Closed += (_, _) =>
        {
            if (_subscribedMainVm is not null)
                _subscribedMainVm.PropertyChanged -= OnMainVmPropertyChanged;
            _subscribedMainVm = null;
        };
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm) return;
        if (e.PropertyName is nameof(MainWindowViewModel.TimeSource) or
                              nameof(MainWindowViewModel.TimeSourceLabel) or
                              nameof(MainWindowViewModel.TimeSourceBadge))
        {
            ApplyTimeSourceFromVm(vm);
        }
    }

    private void ApplyTimeSourceFromVm(MainWindowViewModel vm)
    {
        ClockFace.TimeSourceLabel = vm.TimeSourceLabel;
        ClockFace.TimeSourceBadge = vm.TimeSourceBadge;
    }

    /// <summary>
    /// The TabViewModel hosted by this window. Exposed so MainWindowViewModel
    /// can re-attach the tab to the main strip when the user picks
    /// "Bring back into tabs".
    /// </summary>
    internal TabViewModel Tab => _tab;

    // ----------------------------------------------------------------
    // Overlay button
    // ----------------------------------------------------------------

    /// <summary>
    /// v0.0.35: ⋯ overlay (Fluent SymbolRegular.MoreHorizontal20) →
    /// open the attached ContextMenu (Settings / Themes / Bring back
    /// into tabs / Help / About). Replaces the v0.0.34 stacked
    /// ✕ + ? button pair — ✕ was redundant with the OS title-bar
    /// close button, and consolidating the menu items under a single
    /// "more options" affordance is more discoverable.
    /// </summary>
    private void MoreOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.ContextMenu is null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement       = PlacementMode.Bottom;
        btn.ContextMenu.IsOpen          = true;
    }

    /// <summary>
    /// "Settings…" menu item (renamed from "Tab settings…" in v0.0.35
    /// because the word "tab" is wrong on a free-floating window).
    /// Opens the same Tab Settings dialog used by the in-strip tabs.
    ///
    /// v0.0.39: passes <c>owner: this</c> so the dialog centers over
    /// THIS floating window, not the main window. Earlier the dialog
    /// always appeared centered on MainWindow regardless of which
    /// window invoked it (Dan's Sunday-evening report).
    /// </summary>
    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var mainVm = (Application.Current?.MainWindow as MainWindow)?.GetViewModel();
        mainVm?.OpenTabSettingsForOwner(_tab, owner: this);
    }

    /// <summary>
    /// v0.0.39: "Themes…" routes through the explicit-owner overload
    /// for the same dialog-centering reason as Settings… above.
    /// </summary>
    private void ThemesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var mainVm = (Application.Current?.MainWindow as MainWindow)?.GetViewModel();
        mainVm?.OpenThemesPickerForOwner(_tab, owner: this);
    }

    /// <summary>
    /// "Bring back into tabs" → migrate this clock from a floating
    /// window back to the main window's tab strip. Dispatches to the
    /// main view-model so the tab list mutation + persistence happen
    /// in a single source of truth.
    /// </summary>
    private void BringIntoTabsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var mainVm = (Application.Current?.MainWindow as MainWindow)?.GetViewModel();
        mainVm?.BringWindowIntoTabsCommand.Execute(_tab);
        // The main VM closes our window after re-adding the tab; if
        // it didn't (defensive), fall through and the user can close
        // the window manually.
    }

    private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new HelpDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AboutDialog { Owner = this };
        dlg.ShowDialog();
    }
}
