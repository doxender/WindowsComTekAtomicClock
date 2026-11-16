// ComTekAtomicClock.UI.Services.ExePaths
//
// Locate sibling executables (the Service and the privileged
// installer helper) from the running UI process. Three layouts are
// probed, in this order:
//
//   1. Same folder as the UI exe: the MSIX layout (§ 2.7), where all
//      three exes share one folder.
//   2. The Inno Setup install layout (tools/installer.iss), where each
//      exe has its own subfolder under the install root:
//        {app}\UI\ComTekAtomicClock.UI.exe
//        {app}\Service\ComTekAtomicClock.Service.exe
//        {app}\Installer\ComTekAtomicClock.ServiceInstaller.exe
//      v1.1.8: this probe is new. Without it an installed copy found
//      neither exe, so the §1.9 banner button failed with "Could not
//      locate ComTekAtomicClock.ServiceInstaller.exe".
//   3. The dev layout, where the bin/ directories diverge:
//        <repo>/windows/src/<project>/bin/{Debug|Release}/net8.0-windows/<exe>

using System.IO;

namespace ComTekAtomicClock.UI.Services;

public static class ExePaths
{
    private const string ServiceExeName          = "ComTekAtomicClock.Service.exe";
    private const string ServiceInstallerExeName = "ComTekAtomicClock.ServiceInstaller.exe";

    /// <summary>
    /// Resolve the path to ComTekAtomicClock.ServiceInstaller.exe.
    /// Returns null if not findable.
    /// </summary>
    public static string? FindServiceInstaller()
        => FindFromRunningUi(ServiceInstallerExeName, "ComTekAtomicClock.ServiceInstaller", "Installer");

    /// <summary>
    /// Resolve the path to ComTekAtomicClock.Service.exe.
    /// Returns null if not findable.
    /// </summary>
    public static string? FindService()
        => FindFromRunningUi(ServiceExeName, "ComTekAtomicClock.Service", "Service");

    private static string? FindFromRunningUi(string exeName, string projectName, string installedSubfolder)
    {
        var uiExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(uiExe)) return null;

        var uiDir = Path.GetDirectoryName(uiExe);
        if (string.IsNullOrEmpty(uiDir)) return null;

        return Resolve(uiDir, exeName, projectName, installedSubfolder, File.Exists);
    }

    /// <summary>
    /// The probe itself, kept apart from <see cref="Environment.ProcessPath"/>
    /// and the file system so its order can be unit-tested. Returns the
    /// first of <see cref="Candidates"/> that <paramref name="exists"/>
    /// accepts, or null.
    /// </summary>
    internal static string? Resolve(
        string uiDir, string exeName, string projectName, string installedSubfolder,
        Func<string, bool> exists)
    {
        foreach (var candidate in Candidates(uiDir, exeName, projectName, installedSubfolder))
        {
            if (exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Full paths to probe, in order: sibling (MSIX), installed layout
    /// (Inno), dev Debug, dev Release.
    /// </summary>
    internal static IReadOnlyList<string> Candidates(
        string uiDir, string exeName, string projectName, string installedSubfolder)
    {
        var candidates = new List<string>();
        try
        {
            // 1. Sibling in the same folder (MSIX layout).
            candidates.Add(Path.GetFullPath(Path.Combine(uiDir, exeName)));

            // 2. Inno Setup install layout: uiDir is {app}\UI, so the other
            //    exes sit in sibling subfolders of its parent.
            candidates.Add(Path.GetFullPath(Path.Combine(uiDir, "..", installedSubfolder, exeName)));

            // 3. Dev layout. The UI exe lives at
            //    .../src/ComTekAtomicClock.UI/bin/Debug/net8.0-windows/<exe>
            //    so src/ is four directories up from uiDir.
            var srcDir = Path.GetFullPath(Path.Combine(uiDir, "..", "..", "..", ".."));
            if (string.Equals(Path.GetFileName(srcDir), "src", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.Combine(srcDir, projectName, "bin", "Debug", "net8.0-windows", exeName));
                candidates.Add(Path.Combine(srcDir, projectName, "bin", "Release", "net8.0-windows", exeName));
            }
        }
        catch
        {
            // Ignore path traversal failures and probe what we have.
        }
        return candidates;
    }
}
