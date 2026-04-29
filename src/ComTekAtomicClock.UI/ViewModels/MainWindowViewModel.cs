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
using ComTekAtomicClock.Shared.Settings;
using ComTekAtomicClock.UI.Dialogs;
using ComTekAtomicClock.UI.Services;

namespace ComTekAtomicClock.UI.ViewModels;

[SupportedOSPlatform("windows")]
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _serviceStatePoll;

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

        OpenAboutCommand           = new RelayCommand(OpenAbout);
        InstallServiceCommand      = new RelayCommand(LaunchInstallerAndPoll);
        UninstallServiceCommand    = new RelayCommand(LaunchUninstallerAndPoll);
        OpenTabSettingsCommand     = new RelayCommand(OpenTabSettings, _ => SelectedTab is not null);
        AddTabCommand              = new RelayCommand(AddTab);
        RemoveTabCommand           = new RelayCommand(RemoveTab, _ => Tabs.Count > 1);

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
    // Commands
    // --------------------------------------------------------------

    public RelayCommand InstallServiceCommand   { get; }
    public RelayCommand UninstallServiceCommand { get; }
    public RelayCommand OpenAboutCommand        { get; }
    public RelayCommand OpenTabSettingsCommand  { get; }
    public RelayCommand AddTabCommand           { get; }
    public RelayCommand RemoveTabCommand        { get; }

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

        var dlg = new TabSettingsDialog(SelectedTab)
        {
            Owner = Application.Current?.MainWindow,
        };
        var result = dlg.ShowDialog();
        if (result == true)
        {
            // SaveAppSettings persists the in-memory changes the dialog
            // already wrote back into TabSettings via the TabViewModel.
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

    /// <summary>
    /// Factory passed to Dragablz's TabablzControl.NewItemFactory so the
    /// "+" button in the tab strip creates a fresh tab with the same
    /// defaults as the menu's "Add new tab" command.
    /// </summary>
    public Func<object> NewTabFactory => () => CreateNewTab();

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
