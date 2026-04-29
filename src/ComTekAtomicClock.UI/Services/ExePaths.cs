// ComTekAtomicClock.UI.Services.ExePaths
//
// Locate sibling executables (the Service and the privileged
// installer helper) from the running UI process. In production
// (MSIX install per § 2.7) all three exes live in the same folder
// so sibling lookup is direct. In dev the bin/ directories diverge,
// so we also probe the canonical
//   <repo>/windows/src/<project>/bin/Debug/net8.0-windows/<exe>
// layout.

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
    {
        return FindSiblingOrDevPath(ServiceInstallerExeName, "ComTekAtomicClock.ServiceInstaller");
    }

    /// <summary>
    /// Resolve the path to ComTekAtomicClock.Service.exe.
    /// Returns null if not findable.
    /// </summary>
    public static string? FindService()
    {
        return FindSiblingOrDevPath(ServiceExeName, "ComTekAtomicClock.Service");
    }

    private static string? FindSiblingOrDevPath(string exeName, string projectName)
    {
        var uiExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(uiExe)) return null;

        var uiDir = Path.GetDirectoryName(uiExe);
        if (string.IsNullOrEmpty(uiDir)) return null;

        // 1. Sibling in the same folder (production MSIX layout).
        var sibling = Path.Combine(uiDir, exeName);
        if (File.Exists(sibling))
            return Path.GetFullPath(sibling);

        // 2. Dev layout. The UI exe lives at:
        //    .../src/ComTekAtomicClock.UI/bin/Debug/net8.0-windows/<exe>
        // So src/ is four directories up from uiDir.
        try
        {
            var srcDir = Path.GetFullPath(Path.Combine(uiDir, "..", "..", "..", ".."));
            if (string.Equals(Path.GetFileName(srcDir), "src", StringComparison.OrdinalIgnoreCase))
            {
                var devCandidate = Path.Combine(
                    srcDir, projectName, "bin", "Debug", "net8.0-windows", exeName);
                if (File.Exists(devCandidate))
                    return Path.GetFullPath(devCandidate);

                // Also try Release.
                var devReleaseCandidate = Path.Combine(
                    srcDir, projectName, "bin", "Release", "net8.0-windows", exeName);
                if (File.Exists(devReleaseCandidate))
                    return Path.GetFullPath(devReleaseCandidate);
            }
        }
        catch
        {
            // Ignore path traversal failures and fall through to null.
        }

        return null;
    }
}
