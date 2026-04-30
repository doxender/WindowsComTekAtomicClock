// ComTekAtomicClock.UI.FloatingClockWindow
//
// Host for tabs torn off the main strip. Created by AppInterTabClient
// when Dragablz fires GetNewHost. The window's TabablzControl is
// exposed publicly as FloatingTabs so the IInterTabClient can return
// a reference to it via NewTabHost<Window>.
//
// The ✕ / ? overlay buttons (Themes / Help / About) are duplicated
// here from MainWindow.xaml so torn-away tabs keep the same
// affordances. Each handler dispatches via the MainWindow's view
// model to reuse persistence and dialog ownership.
//
// Per-tab settings (timezone, theme) are reachable on a torn-off tab
// the same way as the main window: right-click the tab header or
// double-click it. Wiring those gestures into the floating window's
// tab strip is still deferred — drag the tab back to the main strip
// to edit timezone today, or use the ? button -> Themes… for a quick
// theme switch.

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
    public FloatingClockWindow()
    {
        InitializeComponent();
    }

    // ----------------------------------------------------------------
    // Overlay buttons (✕ / ?). All operations target the TabViewModel
    // bound to the originating Button (DataContext walks via the
    // DataTemplate that wraps each tab's content).
    // ----------------------------------------------------------------

    /// <summary>
    /// ✕ overlay -> close this tab. Closing the last tab in a
    /// floating window dismisses the window via Dragablz's
    /// ConsolidateOrphanedItems behavior; closing it when other tabs
    /// remain just removes this one from the strip.
    /// </summary>
    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.DataContext is not TabViewModel vm) return;

        // Floating windows host their tabs in a non-bound
        // TabablzControl (Dragablz manages items directly via
        // tear-away). Removing from FloatingTabs.Items is the safe
        // path — closing the last one fires Dragablz's empty-handler.
        FloatingTabs.Items.Remove(vm);
        if (FloatingTabs.Items.Count == 0)
            Close();
    }

    /// <summary>
    /// ? overlay -> open the attached ContextMenu (Themes / Help / About).
    /// Identical pattern to MainWindow.HelpButton_Click — kept duplicated
    /// rather than reaching across windows for a private helper.
    /// </summary>
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.ContextMenu is null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement       = PlacementMode.Bottom;
        btn.ContextMenu.IsOpen          = true;
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

    /// <summary>
    /// Themes… opens the gallery for the tab the menu was raised from.
    /// We re-dispatch through the MainWindow's view model so the
    /// SettingsStore.SaveAppSettings path stays single-sourced (the
    /// floating window doesn't own the AppSettings instance).
    /// </summary>
    private void ThemesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTabFromContextMenuClick(sender, out var vm)) return;
        var mainVm = (Application.Current?.MainWindow as MainWindow)
            ?.GetViewModel();
        mainVm?.OpenThemesPickerForCommand.Execute(vm);
    }

    /// <summary>
    /// Walks MenuItem -> ContextMenu -> PlacementTarget (the help
    /// button on the tab content) -> DataContext (the bound
    /// TabViewModel). Mirrors MainWindow.TryGetTabFromContextMenuClick
    /// — kept private and duplicated rather than promoted to a
    /// shared static helper, since both copies are tiny.
    /// </summary>
    private static bool TryGetTabFromContextMenuClick(object sender, out TabViewModel vm)
    {
        vm = null!;
        if (sender is not System.Windows.Controls.MenuItem mi) return false;

        DependencyObject? cursor = mi;
        while (cursor is not null and not System.Windows.Controls.ContextMenu)
            cursor = LogicalTreeHelper.GetParent(cursor);
        if (cursor is not System.Windows.Controls.ContextMenu ctx) return false;

        if (ctx.PlacementTarget is FrameworkElement target &&
            target.DataContext is TabViewModel tabVm)
        {
            vm = tabVm;
            return true;
        }
        return false;
    }
}
