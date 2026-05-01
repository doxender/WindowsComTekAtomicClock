// ComTekAtomicClock.UI.Dialogs.TabSettingsDialog
//
// Modal dialog for editing settings. Two scopes in one dialog:
//
//   "THIS TAB"             — TimeZone + Theme; per-tab, persisted in
//                            user-scope %APPDATA%\ComTekAtomicClock\
//                            settings.json via the bound TabViewModel.
//
//   "ALL CLOCKS ON THIS PC" — Sync frequency (6 / 12 / 24 hr); machine-
//                            wide, persisted in
//                            %ProgramData%\ComTekAtomicClock\service.json
//                            which the LocalSystem Service re-reads on
//                            each sync iteration. The directory ACL is
//                            granted Modify-for-Authenticated-Users by
//                            the ServiceInstaller, so the unprivileged
//                            UI can write directly without IPC.
//
// Replaces the inline gear popover from Step 6 (which we flagged as
// ugly / confused with the per-tab UI). This dialog buffers changes
// locally and only commits them when the user clicks Save — Cancel
// discards both per-tab AND machine-wide edits.

using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using ComTekAtomicClock.Shared.Settings;
using ComTekAtomicClock.UI.ViewModels;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI.Dialogs;

[SupportedOSPlatform("windows")]
public partial class TabSettingsDialog : FluentWindow
{
    private readonly TabViewModel _tabVm;

    // Snapshot of service.json captured when the dialog opens.
    // Save mutates a copy of this and writes it back; Cancel discards.
    private ServiceConfig _serviceConfig;

    public TabSettingsDialog(TabViewModel tabVm)
    {
        _tabVm = tabVm ?? throw new ArgumentNullException(nameof(tabVm));
        InitializeComponent();

        // ---- Per-tab fields: initialize from the bound TabViewModel.
        TimezoneCombo.SelectedValue = tabVm.TimeZoneId;
        ThemeCombo.SelectedItem     = tabVm.Theme;
        EditingTabLabel.Text        = $"Editing: {tabVm.Label}";

        // ---- Machine-wide field: load service.json (or hardcoded
        // defaults if the file doesn't exist yet — first run before
        // anyone has saved a value).
        try
        {
            _serviceConfig = SettingsStore.LoadServiceConfig();
        }
        catch
        {
            // Treat any read failure (missing dir, parse error) as
            // "use defaults". Save will overwrite cleanly.
            _serviceConfig = new ServiceConfig();
        }

        SelectClosestSyncIntervalItem(_serviceConfig.SyncInterval);

        // v0.0.36: Time Source radio group. Source of truth is
        // service.json (read above into _serviceConfig); the
        // GlobalSettings.TimeSource on the UI side is kept in lockstep
        // by the Save handler. The radio reflects whatever's in
        // service.json on dialog open.
        if (_serviceConfig.TimeSource == TimeSource.Brazil)
            TimeSourceBrazil.IsChecked = true;
        else
            TimeSourceBoulder.IsChecked = true;
    }

    /// <summary>
    /// Pick the dropdown item whose Tag (hours) is closest to the
    /// stored interval. If service.json carries an out-of-band value
    /// (e.g., a power user hand-edited it to 1 hour, or the legacy
    /// hourly default is in effect), the closest of the three offered
    /// choices is selected. Saving will then overwrite with one of
    /// the three canonical values.
    /// </summary>
    private void SelectClosestSyncIntervalItem(TimeSpan interval)
    {
        var hours = interval.TotalHours;
        ComboBoxItem? closest = null;
        var closestDelta = double.MaxValue;

        foreach (var obj in SyncIntervalCombo.Items)
        {
            if (obj is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(),
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out var itemHours))
            {
                var delta = Math.Abs(hours - itemHours);
                if (delta < closestDelta)
                {
                    closest = item;
                    closestDelta = delta;
                }
            }
        }

        SyncIntervalCombo.SelectedItem = closest ?? SyncIntervalCombo.Items[1];
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // ---- Per-tab fields ----
        if (TimezoneCombo.SelectedValue is string ianaId &&
            !string.IsNullOrWhiteSpace(ianaId))
        {
            _tabVm.TimeZoneId = ianaId;
        }
        if (ThemeCombo.SelectedItem is Theme theme)
        {
            _tabVm.Theme = theme;
        }

        // ---- Machine-wide fields ----
        // Capture intent first; persist once at the end so a single
        // service.json write covers both sync-frequency and time-source
        // changes.
        var serviceConfigDirty = false;

        if (SyncIntervalCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(),
                         NumberStyles.Integer,
                         CultureInfo.InvariantCulture,
                         out var hours))
        {
            var newInterval = TimeSpan.FromHours(hours);
            if (newInterval != _serviceConfig.SyncInterval)
            {
                _serviceConfig.SyncInterval = newInterval;
                serviceConfigDirty = true;
            }
        }

        // v0.0.36: Time Source radio group → ServiceConfig.TimeSource.
        // The Atomic Lab badge + per-face source label re-render off
        // GlobalSettings.TimeSource (mirrored by MainWindowViewModel
        // when this dialog returns true), but the actual SNTP pool
        // walk is driven by ServiceConfig.TimeSource (read by Service
        // on its next loop iteration).
        var newSource = (TimeSourceBrazil.IsChecked == true)
            ? TimeSource.Brazil
            : TimeSource.Boulder;
        if (newSource != _serviceConfig.TimeSource)
        {
            _serviceConfig.TimeSource = newSource;
            serviceConfigDirty = true;
        }

        if (serviceConfigDirty)
        {
            try
            {
                SettingsStore.SaveServiceConfig(_serviceConfig);
            }
            catch (Exception ex)
            {
                // Likely cause: %ProgramData%\ComTekAtomicClock\
                // doesn't exist yet (Service was never installed),
                // or its ACL doesn't grant write to this user. Per-
                // tab fields still committed via TabViewModel above
                // — only the machine-wide save is in trouble.
                var reason = ex is UnauthorizedAccessException
                    ? "permission denied"
                    : ex is DirectoryNotFoundException
                        ? "the time-sync service isn't installed yet"
                        : ex.Message;
                System.Windows.MessageBox.Show(
                    this,
                    $"The per-tab settings were saved, but the sync-frequency / time-source change " +
                    $"could not be written to %ProgramData%\\ComTekAtomicClock\\service.json " +
                    $"({reason}).\n\nIt will be retried the next time you open Settings.",
                    "Could not save machine-wide settings",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        // Expose the chosen TimeSource for MainWindowViewModel's Save
        // path so it can mirror the value into AppSettings.Global and
        // trigger the on-face badge / label refresh.
        ChosenTimeSource = newSource;

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// v0.0.36: the TimeSource the user selected on this dialog.
    /// Read by <see cref="ViewModels.MainWindowViewModel.OpenTabSettingsCore"/>
    /// after a successful Save so the in-memory AppSettings.Global
    /// can be mirrored to match service.json.
    /// </summary>
    internal TimeSource ChosenTimeSource { get; private set; } = TimeSource.Boulder;

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
