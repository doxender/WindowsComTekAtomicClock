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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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
    /// torn-away tab routes through the main VM so SaveAppSettings
    /// stays single-sourced.
    /// </summary>
    internal MainWindowViewModel? GetViewModel() => _vm;

    /// <summary>
    /// Force the bound TextBlock in <paramref name="tab"/>'s tab-strip
    /// header to re-pull from <see cref="TabViewModel.Label"/>.
    ///
    /// Background: changing <see cref="TabViewModel.TimeZoneId"/>
    /// raises PropertyChanged for Label, and the ItemTemplate in
    /// MainWindow.xaml binds {Binding Label}. In a stock WPF
    /// ItemsControl the header would refresh automatically. This
    /// version of Dragablz's TabablzControl doesn't propagate that
    /// PropertyChanged through to the rendered tab strip — the only
    /// way the new label appeared was tearing the tab off (which
    /// re-templated it in a fresh container).
    ///
    /// v0.0.14 tried to force a re-template by assigning the tab back
    /// to its same Tabs[idx] slot to fire CollectionChanged.Replace.
    /// That crashed the process: TabablzControl.OnItemsChanged throws
    /// NotImplementedException for Replace.
    ///
    /// This method takes a different route: walk the existing tab
    /// container, find the TextBlock whose Text binding targets Label,
    /// and call BindingExpression.UpdateTarget() on it. No collection
    /// mutation, no container recycling, no Dragablz Replace path.
    /// UpdateLayout() at the end nudges the tab strip to re-arrange
    /// in case the new text is wider than the old.
    /// </summary>
    internal void RefreshTabHeader(TabViewModel tab)
    {
        var container = MainTabs.ItemContainerGenerator.ContainerFromItem(tab);
        if (container is not FrameworkElement fe) return;

        var refreshed = 0;
        foreach (var node in EnumerateVisualDescendants(fe))
        {
            // Fully-qualified — the XAML's <TextBlock> is the WPF
            // primitive, not Wpf.Ui's themed TextBlock subclass that
            // also lives in scope via the ui: namespace.
            if (node is System.Windows.Controls.TextBlock tb)
            {
                var be = BindingOperations.GetBindingExpression(
                    tb, System.Windows.Controls.TextBlock.TextProperty);
                if (be?.ParentBinding.Path?.Path == nameof(TabViewModel.Label))
                {
                    be.UpdateTarget();
                    refreshed++;
                }
            }
        }

        // Re-measure / re-arrange the container so a longer label
        // (e.g., "UTC" → "Europe/Kiev") doesn't get clipped at the
        // old width.
        if (refreshed > 0)
            fe.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> EnumerateVisualDescendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var grandchild in EnumerateVisualDescendants(child))
                yield return grandchild;
        }
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

    // ----------------------------------------------------------------
    // Help / About menu (the "?" button under the "x" on each tab)
    // ----------------------------------------------------------------

    /// <summary>
    /// Click handler on the "?" overlay button: opens its attached
    /// ContextMenu so the user gets a 2-item menu (Help / About).
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
        if (TryGetTabFromContextMenuClick(sender, out var vm))
            _vm.OpenThemesPickerForCommand.Execute(vm);
    }
}
