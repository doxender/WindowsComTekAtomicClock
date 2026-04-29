// ComTekAtomicClock.UI.MainWindow
//
// Code-behind. Constructs MainWindowViewModel as DataContext, wires
// the Ctrl+, accelerator, and handles the per-tab interactions that
// XAML can't express cleanly: double-click to open settings, and the
// right-click context menu items (settings + close).

using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
    // Tab interactions
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
    /// Right-click context menu -> Tab settings…
    /// </summary>
    private void TabContextSettings_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetTabFromContextMenuClick(sender, out var vm) && _vm is not null)
            _vm.OpenTabSettingsForCommand.Execute(vm);
    }

    /// <summary>
    /// Right-click context menu -> Close tab.
    /// </summary>
    private void TabContextClose_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetTabFromContextMenuClick(sender, out var vm) && _vm is not null)
            _vm.CloseTabCommand.Execute(vm);
    }

    /// <summary>
    /// Walk from the clicked MenuItem up to the ContextMenu, then to
    /// its PlacementTarget (the DragablzItem the user right-clicked),
    /// and read its DataContext (the bound TabViewModel). The
    /// fully-qualified System.Windows.Controls types disambiguate
    /// against Wpf.Ui's MenuItem / ContextMenu, both of which our
    /// XAML uses (the Style.Setter Property="ContextMenu" creates a
    /// System.Windows.Controls.ContextMenu, not a ui: one).
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
