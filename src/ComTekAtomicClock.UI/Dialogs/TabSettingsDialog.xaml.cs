// ComTekAtomicClock.UI.Dialogs.TabSettingsDialog
//
// Modal dialog for editing a single tab's TimeZone and Theme. Replaces
// the inline gear popover from Step 6 (which we flagged as ugly /
// confused with the per-tab UI). This dialog buffers changes locally
// and only commits them to the bound TabViewModel when the user
// clicks Save — Cancel discards.

using System.Runtime.Versioning;
using System.Windows;
using ComTekAtomicClock.Shared.Settings;
using ComTekAtomicClock.UI.ViewModels;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI.Dialogs;

[SupportedOSPlatform("windows")]
public partial class TabSettingsDialog : FluentWindow
{
    private readonly TabViewModel _tabVm;

    public TabSettingsDialog(TabViewModel tabVm)
    {
        _tabVm = tabVm ?? throw new ArgumentNullException(nameof(tabVm));
        InitializeComponent();

        // Initialize the controls' values from the tab's current state.
        TimezoneCombo.SelectedValue = tabVm.TimeZoneId;
        ThemeCombo.SelectedItem     = tabVm.Theme;

        EditingTabLabel.Text = $"Editing: {tabVm.Label}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Pull the dialog's working values back into the TabViewModel,
        // which propagates to the underlying TabSettings record. The
        // caller is responsible for persisting AppSettings to disk.
        if (TimezoneCombo.SelectedValue is string ianaId &&
            !string.IsNullOrWhiteSpace(ianaId))
        {
            _tabVm.TimeZoneId = ianaId;
        }
        if (ThemeCombo.SelectedItem is Theme theme)
        {
            _tabVm.Theme = theme;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
