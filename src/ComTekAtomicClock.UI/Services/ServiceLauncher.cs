// ComTekAtomicClock.UI.Services.ServiceLauncher
//
// Launches the privileged ComTekAtomicClock.ServiceInstaller helper
// when the §1.9 banner button is clicked. Process.Start with UseShell-
// Execute=true is what triggers Windows' UAC consent prompt for the
// helper's `requireAdministrator` manifest. The helper does its work
// (sc.exe create / start) and exits; we do not wait synchronously
// because the UAC dialog blocks the thread.

using System.Diagnostics;
using System.Runtime.Versioning;

namespace ComTekAtomicClock.UI.Services;

[SupportedOSPlatform("windows")]
public static class ServiceLauncher
{
    /// <summary>
    /// Result of launching the privileged helper.
    /// </summary>
    public sealed record LaunchResult(bool Started, string? ErrorMessage);

    /// <summary>
    /// Launch ComTekAtomicClock.ServiceInstaller.exe in install mode
    /// with the resolved Service exe path. Returns immediately after
    /// the process is started (or fails to start). The helper runs to
    /// completion in the background; the UI should re-poll service
    /// state shortly afterwards.
    /// </summary>
    public static LaunchResult LaunchServiceInstaller()
    {
        var installerPath = ExePaths.FindServiceInstaller();
        if (installerPath is null)
        {
            return new LaunchResult(false,
                "Could not locate ComTekAtomicClock.ServiceInstaller.exe. " +
                "Build all four projects first.");
        }

        var serviceExe = ExePaths.FindService();
        if (serviceExe is null)
        {
            return new LaunchResult(false,
                "Could not locate ComTekAtomicClock.Service.exe. " +
                "Build all four projects first.");
        }

        return LaunchHelper(installerPath, $"--service-exe \"{serviceExe}\"");
    }

    /// <summary>
    /// Launch ComTekAtomicClock.ServiceInstaller.exe in uninstall
    /// mode. Stops + deletes the Windows Service and removes
    /// %ProgramData%\ComTekAtomicClock\. With <paramref name="purgeUserData"/>
    /// also removes %APPDATA%\ComTekAtomicClock\ (per-user
    /// settings.json). UAC consent fires the same way as install.
    /// </summary>
    public static LaunchResult LaunchServiceUninstaller(bool purgeUserData)
    {
        var installerPath = ExePaths.FindServiceInstaller();
        if (installerPath is null)
        {
            return new LaunchResult(false,
                "Could not locate ComTekAtomicClock.ServiceInstaller.exe.");
        }

        var arguments = purgeUserData
            ? "--uninstall --purge-user-data"
            : "--uninstall";
        return LaunchHelper(installerPath, arguments);
    }

    private static LaunchResult LaunchHelper(string exePath, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName        = exePath,
                Arguments       = arguments,
                UseShellExecute = true, // required for the UAC consent prompt to fire
                Verb            = "runas",
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return new LaunchResult(false, "Process.Start returned null.");

            return new LaunchResult(true, null);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new LaunchResult(false, "Elevation was cancelled by the user.");
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, $"Failed to launch helper: {ex.Message}");
        }
    }
}
