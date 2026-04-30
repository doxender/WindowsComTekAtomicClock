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
using System.Windows.Threading;
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
    /// <summary>
    /// Per Dan's two-event rule (v0.0.32): the tab name is set at
    /// EXACTLY two events — on Load (when the ItemTemplate creates
    /// each tab's header TextBlock and the {Binding Label} reads
    /// the source freshly), and HERE, on the close of the Settings
    /// dialog. No PropertyChanged cascades, no timing-sensitive
    /// dispatch retries, no BindingExpression.UpdateTarget — those
    /// approaches (v0.0.21..v0.0.31) all proved unreliable in
    /// Dragablz's tab strip.
    ///
    /// This walks every Application window (main + every torn-off
    /// FloatingClockWindow), finds every TextBlock tagged
    /// "TabHeaderText" whose DataContext is the given tab, and
    /// imperatively sets its Text to the tab's current Label.
    /// Bypasses bindings entirely. The Tag identifies the right
    /// TextBlock unambiguously even after the binding gets
    /// disconnected by an earlier direct Text set.
    /// </summary>
    internal static void SetTabHeaderInAllDisplays(TabViewModel tab)
    {
        var newText = tab.Label;
        var setCount = 0;
        var windowCount = 0;

        if (Application.Current is { } app)
        {
            foreach (Window window in app.Windows)
            {
                windowCount++;
                foreach (var node in EnumerateVisualDescendants(window))
                {
                    if (node is System.Windows.Controls.TextBlock tb &&
                        Equals(tb.Tag, "TabHeaderText") &&
                        ReferenceEquals(tb.DataContext, tab))
                    {
                        tb.Text = newText;
                        // Re-measure the parent so a longer label
                        // (e.g., "UTC" → "Europe/Kiev") doesn't get
                        // clipped at the old width.
                        if (tb.Parent is FrameworkElement fe)
                            fe.InvalidateMeasure();
                        setCount++;
                    }
                }
            }
        }

        System.Diagnostics.Trace.WriteLine(
            $"[MainWindow] SetTabHeaderInAllDisplays(\"{newText}\"): set {setCount} TextBlock(s) across {windowCount} window(s)");
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
    /// Preview-tunnel single-click selection. Dan's observation:
    /// single clicks were missed but double-clicks always worked —
    /// indicating they went through different code paths. Single
    /// clicks were going through Dragablz's intrinsic click/drag
    /// classifier (which appears to misclassify short clicks as
    /// drag-starts that never complete), while double-clicks went
    /// through WPF's MouseDoubleClick event handled separately. By
    /// hooking PreviewMouseLeftButtonDown, we tunnel DOWN before
    /// Dragablz sees the event and force the selection
    /// unconditionally. e.Handled is intentionally NOT set so
    /// Dragablz still receives the event and can run its drag-tear
    /// gesture detection on subsequent MouseMove.
    /// </summary>
    private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe &&
            fe.DataContext is TabViewModel vm &&
            _vm is not null)
        {
            _vm.SelectedTab = vm;
        }
    }

    // TabContextSettings_Click and TabContextClose_Click handlers
    // were removed in v0.0.26 along with the right-click tab
    // ContextMenu that invoked them. The functionality remains
    // reachable via double-click / Ctrl+, / per-tab ✕ button.
    // TryFindTabFromContextMenuItem below is still used by the
    // ?-overlay menu handlers (Themes / Help / About).

    /// <summary>
    /// Find the <see cref="TabViewModel"/> behind a context-menu
    /// MenuItem click. Tries three strategies in order and returns
    /// on the first success — the layered approach is intentional
    /// because v0.0.22 testing showed the original PlacementTarget-walk
    /// (now fallback #2) was unreliable in some Dragablz scenarios:
    ///
    ///   1. The MenuItem's own DataContext. ContextMenu inherits
    ///      DataContext from its PlacementTarget, and that
    ///      propagates down to the MenuItem. When this works, it's
    ///      the cleanest path.
    ///   2. Walk up logical-tree to ContextMenu, then its
    ///      PlacementTarget's DataContext. Original v0.0.x approach.
    ///   3. Walk up logical-tree to ContextMenu, look at its parent
    ///      MenuItem(s) DataContext. Catches nested-menu structures
    ///      that step #2 misses.
    ///
    /// Trace-logs which strategy succeeded (or failed) so any future
    /// regression is visible without source changes.
    /// </summary>
    private static bool TryFindTabFromContextMenuItem(object sender, out TabViewModel vm)
    {
        vm = null!;
        if (sender is not System.Windows.Controls.MenuItem mi) return false;

        // Strategy 1: MenuItem.DataContext directly.
        if (mi.DataContext is TabViewModel viaContext)
        {
            System.Diagnostics.Trace.WriteLine("  strategy 1: MenuItem.DataContext");
            vm = viaContext;
            return true;
        }

        // Walk up to find the enclosing ContextMenu.
        DependencyObject? cursor = mi;
        while (cursor is not null and not System.Windows.Controls.ContextMenu)
            cursor = LogicalTreeHelper.GetParent(cursor);
        if (cursor is not System.Windows.Controls.ContextMenu ctx)
        {
            System.Diagnostics.Trace.WriteLine("  no ContextMenu ancestor found");
            return false;
        }

        // Strategy 2: ContextMenu.PlacementTarget.DataContext.
        if (ctx.PlacementTarget is FrameworkElement target &&
            target.DataContext is TabViewModel viaTarget)
        {
            System.Diagnostics.Trace.WriteLine(
                $"  strategy 2: PlacementTarget={target.GetType().Name}.DataContext");
            vm = viaTarget;
            return true;
        }

        // Strategy 3: ContextMenu.DataContext (inherited from PlacementTarget).
        if (ctx.DataContext is TabViewModel viaCtxData)
        {
            System.Diagnostics.Trace.WriteLine("  strategy 3: ContextMenu.DataContext");
            vm = viaCtxData;
            return true;
        }

        System.Diagnostics.Trace.WriteLine(
            $"  all strategies failed; PlacementTarget={ctx.PlacementTarget?.GetType().Name ?? "null"}");
        return false;
    }

    // Old name kept as an alias so the ?-overlay handlers
    // (ThemesMenuItem_Click etc.) keep compiling without changes.
    private static bool TryGetTabFromContextMenuClick(object sender, out TabViewModel vm)
        => TryFindTabFromContextMenuItem(sender, out vm);

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
