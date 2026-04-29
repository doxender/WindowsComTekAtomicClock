// ComTekAtomicClock.UI.Services.ServiceStateChecker
//
// Read-only check of the ComTekAtomicClockSvc Windows Service state.
// ServiceController.GetServices() requires no elevation; any standard
// user can enumerate services. Used by MainWindowViewModel to decide
// whether to show the §1.9 banner and which label to put on its
// install/start button.

using System.Runtime.Versioning;
using System.ServiceProcess;

namespace ComTekAtomicClock.UI.Services;

[SupportedOSPlatform("windows")]
public enum ServiceLifecycleState
{
    /// <summary>Service is registered and currently running.</summary>
    Running,
    /// <summary>Service is registered but stopped, paused, or in a transient state.</summary>
    InstalledNotRunning,
    /// <summary>Service is not registered with the SCM at all.</summary>
    NotInstalled,
}

[SupportedOSPlatform("windows")]
public static class ServiceStateChecker
{
    public const string ServiceName = "ComTekAtomicClockSvc";

    /// <summary>Best-effort check of <see cref="ServiceName"/>'s lifecycle state.</summary>
    public static ServiceLifecycleState Check()
    {
        try
        {
            var svc = ServiceController.GetServices()
                .FirstOrDefault(s =>
                    string.Equals(s.ServiceName, ServiceName, StringComparison.OrdinalIgnoreCase));
            if (svc is null)
                return ServiceLifecycleState.NotInstalled;

            using (svc)
            {
                return svc.Status switch
                {
                    ServiceControllerStatus.Running       => ServiceLifecycleState.Running,
                    ServiceControllerStatus.StartPending  => ServiceLifecycleState.Running,
                    _                                     => ServiceLifecycleState.InstalledNotRunning,
                };
            }
        }
        catch
        {
            // If we can't read the service list at all (very rare,
            // policy-restricted environments), conservatively report
            // NotInstalled so the banner offers to install.
            return ServiceLifecycleState.NotInstalled;
        }
    }
}
