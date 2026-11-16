// Regression tests for the v1.1.8 fix: ComTekAtomicClock.ServiceInstaller
// must treat both `uninstall` (passed by tools/installer.iss [UninstallRun]
// and documented in SPEC §20) and `--uninstall` (passed by the UI) as
// uninstall mode. Before, only `--uninstall` counted, so an Add/Remove
// Programs uninstall ran install mode, starting a stopped service instead
// of removing it.

using ComTekAtomicClock.Shared.Installer;

namespace ComTekAtomicClock.Tests;

public class ServiceInstallerArgsTests
{
    [Theory]
    [InlineData("uninstall")]     // the Inno uninstaller's [UninstallRun] line
    [InlineData("--uninstall")]   // the UI's ServiceLauncher.LaunchServiceUninstaller
    [InlineData("UNINSTALL")]
    public void EitherUninstallForm_IsUninstallMode(string verb)
    {
        Assert.True(ServiceInstallerArgs.IsUninstall(new[] { verb }));
    }

    [Fact]
    public void InnoInstallCommandLine_IsInstallMode()
    {
        var args = new[] { "install", "--service-exe", @"C:\Program Files\ComTekAtomicClock\Service\ComTekAtomicClock.Service.exe" };
        Assert.False(ServiceInstallerArgs.IsUninstall(args));
    }

    [Fact]
    public void NoArguments_IsInstallMode()
    {
        Assert.False(ServiceInstallerArgs.IsUninstall(Array.Empty<string>()));
    }

    [Fact]
    public void PurgeUserData_OnlyWhenFlagged()
    {
        Assert.True(ServiceInstallerArgs.IsPurgeUserData(new[] { "uninstall", "--purge-user-data" }));
        Assert.False(ServiceInstallerArgs.IsPurgeUserData(new[] { "uninstall" }));
    }
}
