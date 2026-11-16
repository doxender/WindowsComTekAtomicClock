// Regression tests for the v1.1.8 fix: ExePaths must find the Service and
// installer-helper exes in the Inno Setup install layout
//   {app}\UI\  {app}\Service\  {app}\Installer\
// Before, it probed only the UI's own folder (the MSIX layout) and the dev
// bin folders, so on an installed copy the banner's "Start the time-sync
// service" button failed with "Could not locate
// ComTekAtomicClock.ServiceInstaller.exe" (Dan's box, 2026-11-16).

using System.IO;
using ComTekAtomicClock.UI.Services;

namespace ComTekAtomicClock.Tests;

public class ExePathsTests
{
    private const string InstallerExe     = "ComTekAtomicClock.ServiceInstaller.exe";
    private const string InstallerProject = "ComTekAtomicClock.ServiceInstaller";
    private const string ServiceExe       = "ComTekAtomicClock.Service.exe";
    private const string ServiceProject   = "ComTekAtomicClock.Service";

    private const string InstalledUiDir   = @"C:\Program Files\ComTekAtomicClock\UI";
    private const string DevUiDir         = @"C:\repo\windows\src\ComTekAtomicClock.UI\bin\Debug\net8.0-windows";

    private static Func<string, bool> Only(params string[] present)
    {
        var set = new HashSet<string>(present, StringComparer.OrdinalIgnoreCase);
        return set.Contains;
    }

    [Fact]
    public void InstalledLayout_FindsInstallerHelper()
    {
        var expected = @"C:\Program Files\ComTekAtomicClock\Installer\" + InstallerExe;
        var found = ExePaths.Resolve(InstalledUiDir, InstallerExe, InstallerProject, "Installer", Only(expected));
        Assert.Equal(expected, found);
    }

    [Fact]
    public void InstalledLayout_FindsService()
    {
        var expected = @"C:\Program Files\ComTekAtomicClock\Service\" + ServiceExe;
        var found = ExePaths.Resolve(InstalledUiDir, ServiceExe, ServiceProject, "Service", Only(expected));
        Assert.Equal(expected, found);
    }

    [Fact]
    public void SiblingInUiFolder_WinsOverInstalledLayout()
    {
        var sibling   = InstalledUiDir + @"\" + InstallerExe;
        var installed = @"C:\Program Files\ComTekAtomicClock\Installer\" + InstallerExe;
        var found = ExePaths.Resolve(InstalledUiDir, InstallerExe, InstallerProject, "Installer", Only(sibling, installed));
        Assert.Equal(sibling, found);
    }

    [Fact]
    public void DevLayout_FindsDebugBuildOfTheOtherProject()
    {
        var expected = @"C:\repo\windows\src\ComTekAtomicClock.ServiceInstaller\bin\Debug\net8.0-windows\" + InstallerExe;
        var found = ExePaths.Resolve(DevUiDir, InstallerExe, InstallerProject, "Installer", Only(expected));
        Assert.Equal(expected, found);
    }

    [Fact]
    public void DevLayout_FallsBackToReleaseBuild()
    {
        var expected = @"C:\repo\windows\src\ComTekAtomicClock.Service\bin\Release\net8.0-windows\" + ServiceExe;
        var found = ExePaths.Resolve(DevUiDir, ServiceExe, ServiceProject, "Service", Only(expected));
        Assert.Equal(expected, found);
    }

    [Fact]
    public void NothingPresent_ReturnsNull()
    {
        Assert.Null(ExePaths.Resolve(InstalledUiDir, InstallerExe, InstallerProject, "Installer", Only()));
    }

    [Fact]
    public void RealFileSystem_InstalledLayout_IsFound()
    {
        // The same probe against a real temporary tree shaped like the
        // Inno install, using the production File.Exists check.
        var root = Path.Combine(Path.GetTempPath(), "ctac-exepaths-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "UI"));
            Directory.CreateDirectory(Path.Combine(root, "Installer"));
            var helper = Path.Combine(root, "Installer", InstallerExe);
            File.WriteAllBytes(helper, Array.Empty<byte>());

            var found = ExePaths.Resolve(Path.Combine(root, "UI"), InstallerExe, InstallerProject, "Installer", File.Exists);

            Assert.Equal(Path.GetFullPath(helper), found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
