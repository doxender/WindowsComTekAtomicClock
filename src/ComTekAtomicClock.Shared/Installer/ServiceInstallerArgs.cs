// ComTekAtomicClock.Shared.Installer.ServiceInstallerArgs
//
// Mode parsing for ComTekAtomicClock.ServiceInstaller.exe, kept here
// rather than in the helper's top-level Program.cs so the test project
// can cover it. The helper's callers disagree on the verb's form, and
// both must work:
//   · tools/installer.iss [UninstallRun] and SPEC §20:  uninstall
//   · the UI (ServiceLauncher.LaunchServiceUninstaller): --uninstall
// v1.1.8: before, only the dashed form counted, so the Inno uninstaller's
// call fell through to INSTALL mode, which starts a stopped service
// instead of removing it.

namespace ComTekAtomicClock.Shared.Installer;

public static class ServiceInstallerArgs
{
    /// <summary>True for uninstall mode: <c>uninstall</c> or <c>--uninstall</c>, any case.</summary>
    public static bool IsUninstall(IEnumerable<string> args)
        => args.Any(a => string.Equals(a, "uninstall", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));

    /// <summary>True when <c>--purge-user-data</c> is present (used by uninstall mode only).</summary>
    public static bool IsPurgeUserData(IEnumerable<string> args)
        => args.Any(a => string.Equals(a, "--purge-user-data", StringComparison.OrdinalIgnoreCase));
}
