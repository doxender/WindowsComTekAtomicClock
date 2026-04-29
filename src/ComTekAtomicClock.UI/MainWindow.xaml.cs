// ComTekAtomicClock.UI.MainWindow
//
// Code-behind for the top-level window. Almost all behavior lives in
// MainWindowViewModel — this file just constructs the VM, sets it as
// DataContext, wires the File -> Exit click, and registers the
// Ctrl+, accelerator for Tab settings (the input-gesture text on the
// menu item is informational only; WPF doesn't bind the accelerator
// just because the gesture text is set).

using System.Runtime.Versioning;
using System.Windows;
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

        // Ctrl+, opens the per-tab settings dialog. Common
        // convention on macOS, increasingly on Windows.
        InputBindings.Add(new KeyBinding(
            command: _vm.OpenTabSettingsCommand,
            key: Key.OemComma,
            modifiers: ModifierKeys.Control));
    }

}
