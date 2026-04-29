// ComTekAtomicClock.UI.MainWindow
//
// Code-behind for the top-level window. The bulk of the logic lives
// in MainWindowViewModel; the only things in this file are the
// startup wiring (DataContext) and a couple of UI gestures that are
// awkward to express in XAML alone (gear button -> Popup with the
// tab's DataContext, File -> Exit).

using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls.Primitives;
using ComTekAtomicClock.UI.ViewModels;

namespace ComTekAtomicClock.UI;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
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
    }

    private void ExitMenu_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// The gear button in each tab's header. Sets the shared
    /// SettingsPopup's DataContext to the clicked tab's TabViewModel
    /// (read from the Button.Tag, which the DataTemplate binds to
    /// the tab's data context) and opens the popup anchored at the
    /// gear button.
    /// </summary>
    private void GearButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.Tag is not TabViewModel tabVm) return;

        SettingsPopup.DataContext     = tabVm;
        SettingsPopup.PlacementTarget = btn;
        SettingsPopup.Placement       = PlacementMode.Bottom;
        SettingsPopup.IsOpen          = true;
    }
}
