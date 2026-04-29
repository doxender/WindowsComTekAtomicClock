// ComTekAtomicClock.UI.ViewModels.MainWindowViewModel
//
// Top-level view-model for MainWindow. Loads settings.json on startup
// (creating defaults on first run per § 1.10), wraps each TabSettings
// in a TabViewModel, polls the Service lifecycle state on a timer for
// the §1.9 banner, and exposes commands for the Help -> About menu
// and the banner's install button.
//
// Scope of this commit (Step 6):
//   - Tabs and per-tab settings popover bindings.
//   - Service-state polling -> banner visibility + button label.
//   - About command.
//   - Install/start helper invocation.
//
// Deferred to Step 6b / later commits:
//   - Real IPC client integration (last-sync status display).
//   - Color picker UI / overrides.
//   - Free-floating windows (§ 1.3).
//   - Tray icon (§ 1.7).

using System.Collections.ObjectModel;
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
            _settings.Tabs.Select(t => new TabViewModel(t)));
        if (Tabs.Count == 0)
        {
            // Settings file existed but had no tabs (edge case);
            // synthesize the first-run default tab in memory.
            var defaults = SettingsStore.CreateDefaultAppSettings();
            var fresh = defaults.Tabs[0];
            _settings.Tabs.Add(fresh);
            Tabs.Add(new TabViewModel(fresh));
        }

        SelectedTab = Tabs[0];

        // Poll the Service every 4 s. Inexpensive (just enumerates
        // the local SCM) and snappy enough that the banner reflects
        // reality within a heartbeat of the user installing or
        // stopping the service.
        _serviceStatePoll = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4),
        };
        _serviceStatePoll.Tick += (_, _) => RefreshServiceState();
        _serviceStatePoll.Start();

        OpenAboutCommand    = new RelayCommand(OpenAbout);
        InstallServiceCommand = new RelayCommand(LaunchInstallerAndPoll);

        RefreshServiceState();
    }

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

    public RelayCommand InstallServiceCommand { get; }
    public RelayCommand OpenAboutCommand     { get; }

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

        // Helper runs asynchronously after UAC consent. Re-poll a few
        // times over the next ~10 s so the banner dismisses promptly
        // once the service comes up.
        var attempts = 0;
        var quickPoll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        quickPoll.Tick += (_, _) =>
        {
            attempts++;
            RefreshServiceState();
            if (ServiceState == ServiceLifecycleState.Running || attempts >= 10)
                quickPoll.Stop();
        };
        quickPoll.Start();
    }

    private void OpenAbout(object? _ = null)
    {
        var dlg = new AboutDialog
        {
            Owner = Application.Current?.MainWindow,
        };
        dlg.ShowDialog();
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
