// ComTekAtomicClock.UI.FloatingClockWindow
//
// Host for tabs torn off the main strip. Created by AppInterTabClient
// when Dragablz fires GetNewHost. The window's TabablzControl is
// exposed publicly as FloatingTabs so the IInterTabClient can return
// a reference to it via NewTabHost<Window>.

using System.Runtime.Versioning;
using System.Windows;
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

    /// <summary>
    /// Per-tab ▾ arrow opens TabSettings for the clicked tab.
    /// We do not have a MainWindowViewModel-style command here
    /// because floating windows are intentionally minimal — they
    /// just host one or more tabs and delegate everything else.
    /// </summary>
    private void TabSettingsArrow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.Tag is not TabViewModel tabVm) return;

        var dlg = new TabSettingsDialog(tabVm) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            // Persist via the main app settings (TabSettings is the
            // same record reference that lives in AppSettings.Tabs,
            // so it's already mutated; just trigger a save).
            try
            {
                Shared.Settings.SettingsStore.SaveAppSettings(
                    LoadCurrentSettings());
            }
            catch
            {
                // best-effort
            }
        }
    }

    private static Shared.Settings.AppSettings LoadCurrentSettings()
        => Shared.Settings.SettingsStore.LoadAppSettings();
}
