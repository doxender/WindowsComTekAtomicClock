// ComTekAtomicClock.UI.ViewModels.MainWindowViewModel
//
// Top-level VM. Loads settings.json on startup (creating defaults on
// first run per § 1.10), wraps each TabSettings in a TabViewModel,
// polls the Service lifecycle for the §1.9 banner, exposes commands
// for menu actions (About, Add Tab, Remove Tab, Tab Settings,
// Install Service, Uninstall Service).
//
// IPC integration (live last-sync status in a status bar) lands in
// the next commit.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using ComTekAtomicClock.Shared.Ipc;
using ComTekAtomicClock.Shared.Settings;
using ComTekAtomicClock.UI.Dialogs;
using ComTekAtomicClock.UI.Services;

namespace ComTekAtomicClock.UI.ViewModels;

[SupportedOSPlatform("windows")]
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _serviceStatePoll;

    // --- Live last-sync display state ---
    // Strategy: one DispatcherTimer at 1 Hz drives the status-bar
    // text. Every tick we re-render LastSyncText from the cached
    // SyncStatus so the relative-time component ("12s ago") refreshes
    // live. Every 5th tick we ALSO kick off an async IPC refresh of
    // the cached snapshot. A single IpcClient is held across calls,
    // reconnected on demand if the pipe closed (service stopped /
    // restarted). All IPC errors are swallowed back to a fallback
    // string so the status bar never blocks or shows a stack trace.
    private readonly DispatcherTimer _lastSyncTimer;
    private IpcClient? _ipcClient;
    private SyncStatus? _lastSyncStatus;
    private int _lastSyncTickCounter;
    private const int LastSyncFetchEveryNTicks = 5; // 5 s

    public MainWindowViewModel()
    {
        _settings = SettingsStore.LoadAppSettings();
        Tabs = new ObservableCollection<TabViewModel>(
            _settings.Tabs.Select(WrapTab));

        if (Tabs.Count == 0)
        {
            // Settings file existed but had no tabs (edge case);
            // synthesize the first-run default tab in memory.
            var defaults = SettingsStore.CreateDefaultAppSettings();
            var fresh = defaults.Tabs[0];
            _settings.Tabs.Add(fresh);
            Tabs.Add(WrapTab(fresh));
        }

        SelectedTab = Tabs[0];

        // Keep _settings.Tabs in lockstep with the Tabs ObservableCollection
        // — the Dragablz per-tab close button removes items directly from
        // ItemsSource (bypassing RemoveTabCommand), and Dragablz's
        // NewItemFactory adds items via the same path. This handler
        // mirrors the change into the underlying TabSettings list and
        // persists settings.json.
        Tabs.CollectionChanged += OnTabsCollectionChanged;

        // Poll the Service every 4 s. Inexpensive (just enumerates
        // the local SCM) and snappy enough that the banner reflects
        // reality within a heartbeat of an install or stop.
        _serviceStatePoll = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4),
        };
        _serviceStatePoll.Tick += (_, _) => RefreshServiceState();
        _serviceStatePoll.Start();

        // Live-last-sync timer (1 Hz). See _lastSyncTimer field doc
        // above for the cadence rationale.
        _lastSyncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _lastSyncTimer.Tick += OnLastSyncTick;
        _lastSyncTimer.Start();

        OpenAboutCommand            = new RelayCommand(OpenAbout);
        InstallServiceCommand       = new RelayCommand(LaunchInstallerAndPoll);
        UninstallServiceCommand     = new RelayCommand(LaunchUninstallerAndPoll);
        OpenTabSettingsCommand      = new RelayCommand(OpenTabSettings, _ => SelectedTab is not null);
        OpenTabSettingsForCommand   = new RelayCommand(OpenTabSettingsFor);
        OpenThemesPickerForCommand  = new RelayCommand(OpenThemesPickerFor);
        AddTabCommand               = new RelayCommand(AddTab);
        RemoveTabCommand            = new RelayCommand(RemoveTab, _ => Tabs.Count > 1);
        CloseTabCommand             = new RelayCommand(CloseTab);
        // v0.0.33 — explicit tabs↔windows commands replacing
        // Dragablz tear-away gesture.
        OpenInNewWindowCommand      = new RelayCommand(OpenInNewWindow);
        NewClockWindowCommand       = new RelayCommand(NewClockWindow);
        BringWindowIntoTabsCommand  = new RelayCommand(BringWindowIntoTabs);

        RefreshServiceState();
    }

    private TabViewModel WrapTab(TabSettings t) => new(t);

    /// <summary>The displayed tabs.</summary>
    public ObservableCollection<TabViewModel> Tabs { get; }

    private TabViewModel _selectedTab = null!;
    public TabViewModel SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (ReferenceEquals(_selectedTab, value)) return;
            _selectedTab = value;
            OnPropertyChanged();
        }
    }

    // --------------------------------------------------------------
    // Service lifecycle / banner
    // --------------------------------------------------------------

    private ServiceLifecycleState _serviceState = ServiceLifecycleState.NotInstalled;
    public ServiceLifecycleState ServiceState
    {
        get => _serviceState;
        private set
        {
            if (_serviceState == value) return;
            _serviceState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BannerVisible));
            OnPropertyChanged(nameof(BannerButtonText));
            OnPropertyChanged(nameof(BannerHeadlineText));
            OnPropertyChanged(nameof(ServiceStatusText));
        }
    }

    public bool BannerVisible => ServiceState != ServiceLifecycleState.Running;

    public string BannerHeadlineText => ServiceState switch
    {
        ServiceLifecycleState.NotInstalled        => "The time-sync service isn't installed.",
        ServiceLifecycleState.InstalledNotRunning => "The time-sync service is installed but not running.",
        _                                         => string.Empty,
    };

    public string BannerButtonText => ServiceState switch
    {
        ServiceLifecycleState.NotInstalled        => "Install and start the time-sync service",
        ServiceLifecycleState.InstalledNotRunning => "Start the time-sync service",
        _                                         => string.Empty,
    };

    /// <summary>Displayed in the bottom status bar.</summary>
    public string ServiceStatusText => ServiceState switch
    {
        ServiceLifecycleState.Running             => "Service: Running",
        ServiceLifecycleState.InstalledNotRunning => "Service: Installed but stopped",
        _                                         => "Service: Not installed",
    };

    // --------------------------------------------------------------
    // Live last-sync display (right-hand status bar item)
    // --------------------------------------------------------------

    private string _lastSyncText = "Last sync: pending…";

    /// <summary>
    /// Right-hand status-bar text. Re-rendered every second so the
    /// relative-time component stays current ("3s ago" → "4s ago").
    /// Source data is <see cref="_lastSyncStatus"/>, which is refreshed
    /// over IPC every <see cref="LastSyncFetchEveryNTicks"/> ticks.
    /// </summary>
    public string LastSyncText
    {
        get => _lastSyncText;
        private set
        {
            if (_lastSyncText == value) return;
            _lastSyncText = value;
            OnPropertyChanged();
        }
    }

    private void OnLastSyncTick(object? sender, EventArgs e)
    {
        _lastSyncTickCounter++;
        if (_lastSyncTickCounter >= LastSyncFetchEveryNTicks
            && ServiceState == ServiceLifecycleState.Running)
        {
            _lastSyncTickCounter = 0;
            // Fire-and-forget. The async method handles its own
            // exceptions (catch-all to a swallow) so we never let a
            // pipe hiccup propagate up the timer thread.
            _ = TryFetchLastSyncAsync();
        }
        LastSyncText = FormatLastSync(_lastSyncStatus, ServiceState);
    }

    private async Task TryFetchLastSyncAsync()
    {
        try
        {
            if (_ipcClient is null || !_ipcClient.IsConnected)
            {
                await DisposeIpcClientAsync().ConfigureAwait(false);
                _ipcClient = new IpcClient();
                await _ipcClient.ConnectAsync(timeoutMs: 1500).ConfigureAwait(false);
            }

            var req = IpcEnvelope.Create(IpcMessageType.LastSyncStatusRequest, "{}");
            var resp = await _ipcClient.SendRequestAsync(req, CancellationToken.None)
                                       .ConfigureAwait(false);
            if (resp is null) return;

            var status = IpcWireFormat.UnwrapPayload<SyncStatus>(resp);
            if (status is null) return;

            _lastSyncStatus = status;
        }
        catch
        {
            // Pipe transient errors are common during service
            // start/stop transitions. Drop the broken connection so
            // the next tick reconnects fresh; let the formatter
            // surface the stale-or-missing state to the user.
            await DisposeIpcClientAsync().ConfigureAwait(false);
        }
    }

    private async Task DisposeIpcClientAsync()
    {
        if (_ipcClient is null) return;
        try { await _ipcClient.DisposeAsync().ConfigureAwait(false); }
        catch { /* best effort */ }
        _ipcClient = null;
    }

    /// <summary>
    /// Render the cached <see cref="SyncStatus"/> + service state into
    /// a human-readable status-bar string. Pure function; no I/O.
    /// </summary>
    internal static string FormatLastSync(SyncStatus? status, ServiceLifecycleState state)
    {
        if (state != ServiceLifecycleState.Running)
            return "Last sync: service not running";

        if (status is null || status.AttemptedAtUtc == DateTimeOffset.MinValue)
            return "Last sync: pending…";

        var ago = DateTimeOffset.UtcNow - status.AttemptedAtUtc;
        var agoText = FormatAgo(ago);

        if (status.Success)
        {
            if (status.OffsetSeconds is double offset)
                return $"Last sync: {agoText} ({FormatDrift(offset)})";
            return $"Last sync: {agoText}";
        }

        // Failure path. Trim the error message so it doesn't push the
        // status bar to two lines on a narrow window.
        var err = status.ErrorMessage ?? "unknown error";
        if (err.Length > 60) err = err[..57] + "…";
        return $"Last sync failed: {err}";
    }

    private static string FormatAgo(TimeSpan ago)
    {
        if (ago.TotalSeconds < 0)     return "(future timestamp?)";
        if (ago.TotalSeconds < 5)     return "just now";
        if (ago.TotalSeconds < 60)    return $"{(int)ago.TotalSeconds}s ago";
        if (ago.TotalMinutes < 60)    return $"{(int)ago.TotalMinutes}m ago";
        if (ago.TotalHours   < 24)    return $"{(int)ago.TotalHours}h ago";
        return $"{(int)ago.TotalDays}d ago";
    }

    private static string FormatDrift(double seconds)
    {
        // Sign: positive offset = our clock was FAST relative to
        // NIST and was pulled back (corrected by `-offset`). Negative
        // offset = we were slow and were pushed forward. Display the
        // *correction direction* (intuitive for users).
        var sign = seconds >= 0 ? "−" : "+";
        var abs  = Math.Abs(seconds);
        if (abs < 1.0)   return $"corrected {sign}{abs * 1000:F1} ms";
        if (abs < 60.0)  return $"corrected {sign}{abs:F2} s";
        return $"corrected {sign}{abs / 60:F1} min";
    }

    // --------------------------------------------------------------
    // Commands
    // --------------------------------------------------------------

    public RelayCommand InstallServiceCommand    { get; }
    public RelayCommand UninstallServiceCommand  { get; }
    public RelayCommand OpenAboutCommand         { get; }
    public RelayCommand OpenTabSettingsCommand   { get; }
    /// <summary>
    /// Opens TabSettings for the tab passed as the command parameter
    /// (used by the per-tab ▾ arrow). Distinct from OpenTabSettingsCommand
    /// which targets the SelectedTab.
    /// </summary>
    public RelayCommand OpenTabSettingsForCommand { get; }
    /// <summary>
    /// Opens the Themes gallery dialog (12-tile grid) for the tab passed
    /// as the command parameter. Bound to the per-tab "?" -> Themes…
    /// menu item. Picking a tile mutates TabViewModel.Theme and the
    /// dialog returns DialogResult=true so we persist.
    /// </summary>
    public RelayCommand OpenThemesPickerForCommand { get; }
    public RelayCommand AddTabCommand            { get; }
    public RelayCommand RemoveTabCommand         { get; }
    /// <summary>Removes the tab passed as the command parameter (used by
    /// the per-tab ✕ overlay button on the clock face).</summary>
    public RelayCommand CloseTabCommand          { get; }

    /// <summary>
    /// v0.0.33: migrate the tab passed as command parameter from the
    /// main strip to a new free-floating clock window. Bound to the
    /// per-tab right-click → "Open in new window" menu item.
    /// </summary>
    public RelayCommand OpenInNewWindowCommand   { get; }

    /// <summary>
    /// v0.0.33: spawn a brand-new free-floating clock window with a
    /// fresh tab. Bound to the "+ New window" toolbar button. Distinct
    /// from <see cref="AddTabCommand"/>, which adds a tab to the main
    /// strip in-place.
    /// </summary>
    public RelayCommand NewClockWindowCommand    { get; }

    /// <summary>
    /// v0.0.33: migrate a clock back from a floating window to the
    /// main strip. Command parameter is the TabViewModel hosted by
    /// the floating window. Closes the floating window after re-adding
    /// the tab to the strip.
    /// </summary>
    public RelayCommand BringWindowIntoTabsCommand { get; }

    private void OpenAbout(object? _)
    {
        var dlg = new AboutDialog
        {
            Owner = Application.Current?.MainWindow,
        };
        dlg.ShowDialog();
    }

    private void OpenTabSettings(object? _)
    {
        if (SelectedTab is null) return;
        OpenTabSettingsCore(SelectedTab);
    }

    private void OpenTabSettingsFor(object? param)
    {
        if (param is TabViewModel vm) OpenTabSettingsCore(vm);
    }

    private void OpenTabSettingsCore(TabViewModel tab)
    {
        var dlg = new TabSettingsDialog(tab)
        {
            Owner = Application.Current?.MainWindow,
        };
        var result = dlg.ShowDialog();
        if (result == true)
        {
            // v0.0.33: just persist. Native WPF TabControl honors
            // PropertyChanged on the {Binding Label} ItemTemplate
            // binding automatically when TabViewModel.TimeZoneId
            // setter raises PropertyChanged(nameof(Label)). The
            // v0.0.32 imperative SetTabHeaderInAllDisplays walk is
            // gone along with Dragablz.
            PersistAfterDialog();
        }
    }

    private void OpenThemesPickerFor(object? param)
    {
        if (param is not TabViewModel vm) return;
        var dlg = new ThemesDialog(vm)
        {
            Owner = Application.Current?.MainWindow,
        };
        var result = dlg.ShowDialog();
        if (result == true)
        {
            // The dialog mutated TabViewModel.Theme directly; mirror to
            // disk via SettingsStore.SaveAppSettings.
            PersistAfterDialog();
        }
    }

    private void PersistAfterDialog()
    {
        try
        {
            SettingsStore.SaveAppSettings(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current?.MainWindow!,
                $"Settings were updated in memory but could not be saved to disk.\n\n{ex.Message}",
                "Save failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CloseTab(object? param)
    {
        if (param is not TabViewModel vm) return;

        // Closing the last tab in the main window shuts the app down
        // (rather than leaving an empty MainWindow with no clock face
        // on screen). Tabs in floating windows are handled by
        // Dragablz's TabEmptiedHandler (closes the floating window).
        if (Tabs.Count == 1 && Tabs.Contains(vm))
        {
            Application.Current?.Shutdown();
            return;
        }

        // Removing from Tabs cascades to _settings.Tabs + persistence
        // via the OnTabsCollectionChanged handler.
        Tabs.Remove(vm);
    }

    private void AddTab(object? _)
    {
        var vm = CreateNewTab();
        Tabs.Add(vm);   // OnTabsCollectionChanged mirrors to _settings.Tabs + persists
        SelectedTab = vm;
    }

    private void RemoveTab(object? _)
    {
        if (Tabs.Count <= 1) return; // menu-driven remove keeps at least one tab
        if (SelectedTab is null) return;

        var index = Tabs.IndexOf(SelectedTab);
        Tabs.Remove(SelectedTab);   // OnTabsCollectionChanged mirrors + persists
        SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
    }

    private TabViewModel CreateNewTab()
    {
        var newTab = new TabSettings
        {
            TimeZoneId = "UTC",
            Theme      = _settings.Global.DefaultTheme,
            TimeFormat = TimeFormatMode.Auto,
        };
        return WrapTab(newTab);
    }

    // --------------------------------------------------------------
    // v0.0.33: tabs ↔ floating-windows transitions
    //
    // Replaces Dragablz's drag-tear-away gesture with explicit,
    // user-initiated commands. Open floating windows are tracked in
    // _openFloatingWindows so the "Bring back into tabs" reverse
    // operation can find the right window to close. Tab data is
    // SHARED across modes — the same TabViewModel instance is the
    // floating window's DataContext while it's "out," then re-attached
    // to the Tabs collection when brought back.
    // --------------------------------------------------------------

    private readonly List<FloatingClockWindow> _openFloatingWindows = new();

    /// <summary>
    /// Right-click → "Open in new window": pop the tab out of the main
    /// strip and into a new FloatingClockWindow. Settings.json is
    /// updated via OnTabsCollectionChanged (since we Remove from Tabs).
    /// </summary>
    private void OpenInNewWindow(object? param)
    {
        if (param is not TabViewModel vm) return;

        // Don't allow opening the LAST tab in a new window — that would
        // leave the main window with an empty tab strip. Same guard as
        // CloseTab.
        if (Tabs.Count <= 1)
        {
            MessageBox.Show(
                Application.Current?.MainWindow!,
                "This is the last tab. Add another tab first, or use “+ New window” to spawn a fresh clock without removing this one.",
                "Can't move the last tab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Tabs.Remove(vm);   // OnTabsCollectionChanged mirrors + persists
        SpawnFloatingWindow(vm);
    }

    /// <summary>
    /// "+ New window" toolbar button: create a fresh tab and immediately
    /// host it in a new FloatingClockWindow. Does NOT add the tab to
    /// the main strip (a dual presence would be confusing).
    /// </summary>
    private void NewClockWindow(object? _)
    {
        var vm = CreateNewTab();
        // Persist the underlying TabSettings so the clock survives
        // restart even if the user never brings it back into tabs.
        // (Window-position persistence across restart is a Phase-2
        // todo — see CONTEXT.md.)
        if (!_settings.Tabs.Contains(vm.Settings))
            _settings.Tabs.Add(vm.Settings);
        TryPersist();
        SpawnFloatingWindow(vm);
    }

    /// <summary>
    /// "Bring back into tabs" menu item on a floating window: re-attach
    /// the floating window's TabViewModel to the main strip and close
    /// the window.
    /// </summary>
    private void BringWindowIntoTabs(object? param)
    {
        if (param is not TabViewModel vm) return;

        // Already in the main strip? (Defensive — shouldn't happen.)
        if (!Tabs.Contains(vm))
        {
            Tabs.Add(vm);   // OnTabsCollectionChanged mirrors + persists
            SelectedTab = vm;
        }

        // Find and close the floating window hosting this tab.
        var window = _openFloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Tab, vm));
        if (window is not null)
        {
            _openFloatingWindows.Remove(window);
            window.Close();
        }
    }

    /// <summary>
    /// Construct, register, and show a FloatingClockWindow for the
    /// given TabViewModel. Tracks Closed so the registry stays clean
    /// and persistence reflects the actual open set.
    /// </summary>
    private void SpawnFloatingWindow(TabViewModel vm)
    {
        var window = new FloatingClockWindow(vm);
        window.Closed += (_, _) =>
        {
            _openFloatingWindows.Remove(window);
            // If the user closed the window via the title-bar X (NOT
            // via "Bring back into tabs"), the tab is gone — purge
            // its underlying TabSettings from settings.json so the
            // clock doesn't reappear on restart. The tab is "in tabs"
            // iff the Tabs collection contains it.
            if (!Tabs.Contains(vm))
            {
                _settings.Tabs.Remove(vm.Settings);
                TryPersist();
            }
        };
        _openFloatingWindows.Add(window);
        window.Show();
    }

    private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Mirror inserts/removes into _settings.Tabs so settings.json
        // stays in lockstep regardless of who mutated the collection
        // (Dragablz close button, Dragablz "+" via NewItemFactory, or
        // our menu commands).
        if (e.OldItems is not null)
        {
            foreach (TabViewModel vm in e.OldItems)
                _settings.Tabs.Remove(vm.Settings);
        }
        if (e.NewItems is not null)
        {
            foreach (TabViewModel vm in e.NewItems)
            {
                if (!_settings.Tabs.Contains(vm.Settings))
                    _settings.Tabs.Add(vm.Settings);
            }
        }
        TryPersist();
    }

    private void TryPersist()
    {
        try { SettingsStore.SaveAppSettings(_settings); }
        catch { /* best-effort; UI continues with in-memory state */ }
    }

    private void LaunchInstallerAndPoll(object? _ = null)
    {
        var result = ServiceLauncher.LaunchServiceInstaller();
        if (!result.Started)
        {
            MessageBox.Show(
                Application.Current?.MainWindow!,
                result.ErrorMessage ?? "Unknown error.",
                "Could not start the time-sync service",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        QuickPollServiceState();
    }

    private void LaunchUninstallerAndPoll(object? _ = null)
    {
        var confirm = MessageBox.Show(
            Application.Current?.MainWindow!,
            "This will stop and remove the ComTek Atomic Clock Windows Service\n" +
            "and delete %ProgramData%\\ComTekAtomicClock\\.\n\n" +
            "Your per-user settings (%APPDATA%\\ComTekAtomicClock\\settings.json)\n" +
            "will be preserved.\n\nContinue?",
            "Uninstall the time-sync service",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var result = ServiceLauncher.LaunchServiceUninstaller(purgeUserData: false);
        if (!result.Started)
        {
            MessageBox.Show(
                Application.Current?.MainWindow!,
                result.ErrorMessage ?? "Unknown error.",
                "Could not uninstall the time-sync service",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        QuickPollServiceState();
    }

    private void QuickPollServiceState()
    {
        // Helper runs asynchronously. Re-poll every second for ~10 s
        // so the banner / status bar reflects reality promptly.
        var attempts = 0;
        var quickPoll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        quickPoll.Tick += (_, _) =>
        {
            attempts++;
            RefreshServiceState();
            if (attempts >= 10) quickPoll.Stop();
        };
        quickPoll.Start();
    }

    private void RefreshServiceState()
    {
        ServiceState = ServiceStateChecker.Check();
    }

    // --------------------------------------------------------------
    // INotifyPropertyChanged
    // --------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
