// ComTekAtomicClock.UI.App
//
// Application lifecycle: when the UI launches, start the
// ComTekAtomicClockSvc Windows Service if it's installed but stopped.
// When the UI exits (last window closed), stop the service. The
// service's start mode is "demand" (set by ServiceInstaller.exe), so
// it does not auto-start at boot — the clock app owns its lifetime.
//
// Both Start() and Stop() are non-elevated; they work because
// ServiceInstaller sets a service ACL that grants Authenticated Users
// the SERVICE_START + SERVICE_STOP rights. If that ACL isn't present
// (older install) the calls quietly fail and the §1.9 banner surfaces
// the right next-step.

using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Threading;

namespace ComTekAtomicClock.UI;

[SupportedOSPlatform("windows")]
public partial class App : Application
{
    private const string ServiceName = "ComTekAtomicClockSvc";

    /// <summary>
    /// Subscribe to the three escape-hatches for unhandled exceptions
    /// HERE rather than in OnStartup — App's constructor runs before
    /// any WPF code, including before base.OnStartup processes the
    /// StartupUri and constructs MainWindow. v0.0.15's earlier
    /// subscription site (in OnStartup, after base.OnStartup) was
    /// too late: anything that threw during MainWindow's XAML parse,
    /// InitializeComponent, or Loaded handler escaped before the
    /// handler was hooked, and the process exited silently.
    ///
    /// The three handlers cover the three places an exception can
    /// escape:
    ///   · DispatcherUnhandledException — UI-thread (the dispatcher).
    ///     Most common case. Recovers by setting e.Handled=true so
    ///     a single recoverable mishap doesn't kill the app.
    ///   · AppDomain.UnhandledException — non-dispatcher threads
    ///     (background tasks, finalizers, native callbacks). These
    ///     are usually fatal — the runtime is mid-tear-down — but
    ///     we still surface the exception so the user knows what
    ///     happened instead of staring at a closed window.
    ///   · TaskScheduler.UnobservedTaskException — fire-and-forget
    ///     Task whose exception was never awaited (e.g.,
    ///     IpcClient.TryFetchLastSyncAsync invoked via `_ = ...`).
    ///     Marked Observed so the process doesn't FailFast.
    /// </summary>
    public App()
    {
        DispatcherUnhandledException                 += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException   += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException        += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TryStartService();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TryStopService();
        base.OnExit(e);
    }

    /// <summary>
    /// Dispatcher (UI-thread) handler. Marks Handled=true so a single
    /// recoverable mishap doesn't tear the app down.
    /// </summary>
    private static void OnDispatcherUnhandledException(
        object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowExceptionDialog(e.Exception, "Dispatcher");
        e.Handled = true;
    }

    /// <summary>
    /// AppDomain handler — fires for non-dispatcher-thread exceptions
    /// (background tasks, finalizers, native callbacks). These are
    /// usually fatal because the runtime is mid-tear-down by the time
    /// we're called, but surface the exception so the user knows what
    /// just killed the app. Dispatch to the UI thread because
    /// MessageBox.Show on a non-UI thread is unreliable.
    /// </summary>
    private static void OnAppDomainUnhandledException(
        object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception
                 ?? new Exception(e.ExceptionObject?.ToString() ?? "(non-Exception)");
        var origin = e.IsTerminating ? "AppDomain (fatal)" : "AppDomain";
        TrySurfaceOnUiThread(ex, origin);
    }

    /// <summary>
    /// TaskScheduler handler — fires when a Task's exception was never
    /// awaited (typical with our `_ = TryFetchLastSyncAsync()`
    /// fire-and-forget pattern in MainWindowViewModel). Mark Observed
    /// so the .NET runtime doesn't FailFast at the next GC.
    /// </summary>
    private static void OnUnobservedTaskException(
        object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TrySurfaceOnUiThread(e.Exception, "Task (unobserved)");
        e.SetObserved();
    }

    private static void TrySurfaceOnUiThread(Exception ex, string origin)
    {
        System.Diagnostics.Debug.WriteLine($"[UnhandledException:{origin}] {ex}");
        try
        {
            var d = Current?.Dispatcher;
            if (d is null || d.HasShutdownStarted) return;
            d.BeginInvoke(new Action(() => ShowExceptionDialog(ex, origin)));
        }
        catch { /* dispatcher may already be down; nothing more we can do */ }
    }

    /// <summary>
    /// Shared rendering for any unhandled exception. Trims the stack
    /// to the first ~6 frames so the dialog stays readable; full
    /// stack always goes to Debug output for anyone attached.
    /// </summary>
    private static void ShowExceptionDialog(Exception ex, string origin)
    {
        var stackLines = (ex.StackTrace ?? "(no stack)").Split('\n');
        var trimmed = string.Join("\n",
            stackLines.Take(6).Select(l => l.TrimEnd('\r')));
        if (stackLines.Length > 6) trimmed += "\n…";

        System.Diagnostics.Debug.WriteLine($"[UnhandledException:{origin}] {ex}");

        MessageBox.Show(
            $"{ex.GetType().FullName}  [{origin}]\n\n{ex.Message}\n\n{trimmed}",
            "ComTek Atomic Clock — unhandled exception",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    /// <summary>
    /// If the service is installed and not running, attempt to start
    /// it. Silent on failure — the §1.9 banner will reflect the state.
    /// </summary>
    private static void TryStartService()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            // Touch Status to force an SCM round-trip; throws if the
            // service is not installed.
            var status = sc.Status;
            if (status == ServiceControllerStatus.Stopped)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
        }
        catch
        {
            // Service not installed, no permissions, or transient SCM
            // hiccup. The MainWindow's polling timer + §1.9 banner
            // handle the visible UX.
        }
    }

    /// <summary>
    /// If the service is currently running, request a stop. Best-effort.
    /// </summary>
    private static void TryStopService()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.CanStop && sc.Status != ServiceControllerStatus.Stopped
                           && sc.Status != ServiceControllerStatus.StopPending)
            {
                sc.Stop();
                // Don't wait long; if the service is sluggish, we'd
                // rather the app exit promptly than hang the user.
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
            }
        }
        catch
        {
            // Best-effort. Already stopped, no permission, or service
            // deleted — none of these need to block app exit.
        }
    }
}
