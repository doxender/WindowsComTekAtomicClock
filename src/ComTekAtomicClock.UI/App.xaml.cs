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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Surface unhandled UI-thread exceptions instead of letting
        // them silently kill the process. v0.0.14 demonstrated this
        // gap: a Dragablz CollectionChanged.Replace edge case threw,
        // there was no debugger attached (Release build), and the
        // app just vanished. With this handler we get a MessageBox
        // showing the exception type + message + first few stack
        // frames, plus the option to keep going (e.Handled = true)
        // so a single recoverable mishap doesn't take the app down.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        TryStartService();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TryStopService();
        base.OnExit(e);
    }

    /// <summary>
    /// Last-resort UI-thread exception handler. Surfaces the exception
    /// in a MessageBox so silent crashes (especially in Release where
    /// no debugger / no JIT-debug dialog is in play) become visible.
    /// We mark Handled=true so the exception doesn't propagate up and
    /// kill the process — most WPF UI-thread exceptions (binding
    /// mishaps, dispatched-callback NPEs) are recoverable.
    /// </summary>
    private static void OnDispatcherUnhandledException(
        object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var ex = e.Exception;
        // Trim the stack to the first ~6 frames so the dialog stays
        // readable. Full stack still goes to debug output for anyone
        // attached.
        var stackLines = (ex.StackTrace ?? "(no stack)").Split('\n');
        var trimmed = string.Join("\n",
            stackLines.Take(6).Select(l => l.TrimEnd('\r')));
        if (stackLines.Length > 6) trimmed += "\n…";

        System.Diagnostics.Debug.WriteLine($"[UnhandledException] {ex}");

        MessageBox.Show(
            $"{ex.GetType().FullName}\n\n{ex.Message}\n\n{trimmed}",
            "ComTek Atomic Clock — unhandled exception",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
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
