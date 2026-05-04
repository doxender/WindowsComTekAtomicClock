// ComTekAtomicClock.UI.MainWindow
//
// Code-behind. Constructs MainWindowViewModel as DataContext, wires
// the Ctrl+, accelerator, and handles the per-tab interactions that
// XAML can't express cleanly:
//
//   · TabItem_DoubleClick — open Tab Settings dialog.
//   · TabContextSettings_Click — right-click → Tab Settings.
//   · TabContextOpenInNewWindow_Click — right-click → migrate this
//       tab to a new free-floating clock window.
//   · HelpButton_Click — open the "?" overlay's context menu.
//   · ThemesMenuItem_Click / HelpMenuItem_Click / AboutMenuItem_Click —
//       handlers for the "?" overlay's menu items.
//
// As of v0.0.33 this file is dramatically smaller than v0.0.32:
// the imperative SetTabHeaderInAllDisplays + EnumerateVisualDescendants
// walker, the PreviewMouseLeftButtonDown click-rescue handler, the
// "two-event tab-name rule" comment, and the multi-strategy
// TryFindTabFromContextMenuItem helper are all gone. Native WPF
// TabControl handles single-click selection natively and refreshes
// {Binding Label} on PropertyChanged automatically — none of the
// Dragablz workarounds are needed.

using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ComTekAtomicClock.UI.Dialogs;
using ComTekAtomicClock.UI.ViewModels;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI;

[SupportedOSPlatform("windows")]
public partial class MainWindow : FluentWindow
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Accessor for sibling windows (notably FloatingClockWindow) that
    /// need to dispatch to a VM command — e.g., the Themes… picker on a
    /// floating window routes through the main VM so SaveAppSettings
    /// stays single-sourced.
    /// </summary>
    internal MainWindowViewModel? GetViewModel() => _vm;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = new MainWindowViewModel();
        DataContext = _vm;

        // Ctrl+, opens the per-tab settings dialog. Common convention
        // on macOS, increasingly on Windows.
        InputBindings.Add(new KeyBinding(
            command: _vm.OpenTabSettingsCommand,
            key: Key.OemComma,
            modifiers: ModifierKeys.Control));
    }

    // ----------------------------------------------------------------
    // Tab interactions — TabItem
    // ----------------------------------------------------------------

    /// <summary>
    /// Excel-style: double-click a tab header to open its settings.
    /// </summary>
    private void TabItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe &&
            fe.DataContext is TabViewModel vm &&
            _vm is not null)
        {
            _vm.OpenTabSettingsForCommand.Execute(vm);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Right-click on a tab header → directly open the Tab Settings
    /// dialog (matches v0.0.23 spec; replaces the brief v0.0.33
    /// two-item ContextMenu that Dan removed in v0.0.34 first-run
    /// feedback). PreviewMouseRightButtonDown so we capture before
    /// the (now-absent) ContextMenu trigger and so the user gets
    /// instant feedback rather than a transient menu flash.
    /// </summary>
    private void TabItem_PreviewRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe &&
            fe.DataContext is TabViewModel vm &&
            _vm is not null)
        {
            _vm.OpenTabSettingsForCommand.Execute(vm);
            e.Handled = true;
        }
    }

    /// <summary>
    /// "?" overlay → "Open in new window" menu item. Migrates this tab
    /// out of the main strip and into a new FloatingClockWindow. v0.0.34
    /// moved this here from the tab right-click menu (which was removed).
    /// </summary>
    private void OpenInNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (TryGetTabFromContextMenuItem(sender, out var vm))
            _vm.OpenInNewWindowCommand.Execute(vm);
    }

    // ----------------------------------------------------------------
    // ContextMenu sender resolution
    // ----------------------------------------------------------------

    /// <summary>
    /// Find the <see cref="TabViewModel"/> behind a context-menu
    /// MenuItem click. Native WPF ContextMenu inherits DataContext
    /// from its PlacementTarget, so the MenuItem.DataContext is the
    /// TabViewModel directly. We also fall back to walking up to the
    /// ContextMenu and looking at its PlacementTarget, in case future
    /// markup decouples the inheritance.
    /// </summary>
    private static bool TryGetTabFromContextMenuItem(object sender, out TabViewModel vm)
    {
        vm = null!;
        if (sender is not System.Windows.Controls.MenuItem mi) return false;

        // Strategy 1: MenuItem.DataContext directly.
        if (mi.DataContext is TabViewModel viaContext)
        {
            vm = viaContext;
            return true;
        }

        // Strategy 2: walk up to ContextMenu, then check its PlacementTarget.
        DependencyObject? cursor = mi;
        while (cursor is not null and not System.Windows.Controls.ContextMenu)
            cursor = LogicalTreeHelper.GetParent(cursor);
        if (cursor is System.Windows.Controls.ContextMenu ctx &&
            ctx.PlacementTarget is FrameworkElement target &&
            target.DataContext is TabViewModel viaTarget)
        {
            vm = viaTarget;
            return true;
        }

        return false;
    }

    // ----------------------------------------------------------------
    // Help / About menu (the "?" button on each tab)
    // ----------------------------------------------------------------

    /// <summary>
    /// Click handler on the "?" overlay button: opens its attached
    /// ContextMenu so the user gets a 3-item menu (Themes / Help / About).
    /// </summary>
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.ContextMenu is null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        btn.ContextMenu.IsOpen          = true;
    }

    /// <summary>
    /// v1.1.1 — Jolly Roger overlay button click handler. Mirrors the
    /// HelpButton pattern: opens the attached ContextMenu (Hora Chapín
    /// checkable + Almuerzo / Fini momentary demos). Visible only on
    /// CaptJohn (button Visibility is bound via BoolToVis on
    /// TabViewModel.IsCaptJohnTheme).
    /// </summary>
    private void JollyRogerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.ContextMenu is null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Top;
        btn.ContextMenu.IsOpen          = true;
    }

    /// <summary>
    /// v1.1.1 — Jolly Roger ContextMenu Closed handler. Clears any
    /// active CaptJohn demo mode so the noon / 5 PM pin only persists
    /// while the user has the popup open. Hora Chapín stays where the
    /// user set it (persistent toggle, not demo).
    /// </summary>
    private void JollyRogerMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ContextMenu menu) return;
        if (menu.DataContext is ViewModels.TabViewModel tabVm)
            tabVm.CaptJohnDemoMode = string.Empty;
    }

    private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new HelpDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.OpenAboutCommand.Execute(null);
    }

    /// <summary>
    /// "?" overlay -> Themes… menu item. Walks the ContextMenu back to
    /// the help button (its PlacementTarget) to find which TabViewModel
    /// initiated the menu, then dispatches to the VM command which
    /// opens the gallery and persists on a successful pick.
    /// </summary>
    private void ThemesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (TryGetTabFromContextMenuItem(sender, out var vm))
            _vm.OpenThemesPickerForCommand.Execute(vm);
    }
}
