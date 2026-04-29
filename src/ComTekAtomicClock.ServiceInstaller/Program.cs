// ComTekAtomicClock.ServiceInstaller
//
// The privileged helper described in requirements.txt § 1.9. Always
// runs elevated (app.manifest enforces requireAdministrator). Detects
// the current state of the ComTekAtomicClockSvc Windows Service and
// performs only the minimum action required:
//
//   - Service running                -> no-op, exit 0
//   - Service installed but stopped  -> sc.exe start
//   - Service not installed          -> create %ProgramData%\ComTekAtomicClock\
//                                       with broad ACLs, then sc.exe create + start
//
// Resolves the path to ComTekAtomicClock.Service.exe in this order:
//   1. Explicit `--service-exe <path>` command-line argument.
//   2. Sibling exe in the same directory as this helper (the MSIX
//      package layout per § 2.7 puts both exes in the same folder).
//
// Exits 0 on success, 1 on any failure with the error written to
// stderr. The UI surfaces non-zero exit + stderr as a toast/dialog
// per § 1.9.

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;

[assembly: SupportedOSPlatform("windows")]

const string ServiceName       = "ComTekAtomicClockSvc";
const string DisplayName       = "ComTek Atomic Clock — time sync";
const string ProgramDataFolder = "ComTekAtomicClock";

try
{
    var state = DetectServiceState(ServiceName);
    Console.Out.WriteLine($"Detected state: {state}");

    switch (state)
    {
        case ServiceState.Running:
            Console.Out.WriteLine($"Service '{ServiceName}' is already running. No action needed.");
            return 0;

        case ServiceState.InstalledStopped:
            Console.Out.WriteLine($"Service '{ServiceName}' is installed but stopped. Starting...");
            StartService(ServiceName);
            Console.Out.WriteLine("Service started successfully.");
            return 0;

        case ServiceState.NotInstalled:
        {
            Console.Out.WriteLine($"Service '{ServiceName}' is not installed. Installing...");
            var serviceExe = ResolveServiceExePath(args);
            Console.Out.WriteLine($"Service exe: {serviceExe}");

            EnsureProgramDataDirectory();
            Console.Out.WriteLine($"%ProgramData%\\{ProgramDataFolder}\\ ready.");

            CreateService(ServiceName, DisplayName, serviceExe);
            Console.Out.WriteLine("sc.exe create completed.");

            StartService(ServiceName);
            Console.Out.WriteLine("Service started successfully.");
            return 0;
        }

        default:
            // Should be unreachable since the enum is exhaustive above.
            throw new InvalidOperationException($"Unexpected ServiceState: {state}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    if (ex.InnerException is not null)
        Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
    return 1;
}

// =====================================================================
// Helpers
// =====================================================================

static ServiceState DetectServiceState(string serviceName)
{
    var svc = ServiceController
        .GetServices()
        .FirstOrDefault(s =>
            string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));

    if (svc is null) return ServiceState.NotInstalled;

    using (svc)
    {
        return svc.Status switch
        {
            ServiceControllerStatus.Running       => ServiceState.Running,
            ServiceControllerStatus.StartPending  => ServiceState.Running,
            _                                     => ServiceState.InstalledStopped,
        };
    }
}

static string ResolveServiceExePath(string[] args)
{
    // 1. Explicit argument wins.
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--service-exe")
        {
            var p = args[i + 1];
            if (!File.Exists(p))
                throw new FileNotFoundException(
                    $"--service-exe path does not exist: {p}");
            return Path.GetFullPath(p);
        }
    }

    // 2. Sibling in same directory as this helper.
    var thisDir = Path.GetDirectoryName(Environment.ProcessPath)
                  ?? throw new InvalidOperationException("Could not determine helper's directory.");
    var sibling = Path.Combine(thisDir, "ComTekAtomicClock.Service.exe");
    if (File.Exists(sibling))
        return sibling;

    throw new FileNotFoundException(
        $"Could not locate ComTekAtomicClock.Service.exe. " +
        $"Expected as a sibling of this helper at '{sibling}', " +
        $"or pass --service-exe <full path>.");
}

/// <summary>
/// Ensure %ProgramData%\ComTekAtomicClock\ exists with NTFS ACLs that
/// grant Authenticated Users Modify (so any logged-in user's
/// unprivileged UI can write service.json there). Inheritance flags
/// propagate the rule to files created in the directory later.
/// </summary>
static void EnsureProgramDataDirectory()
{
    var dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        ProgramDataFolder);

    Directory.CreateDirectory(dir);

    var di = new DirectoryInfo(dir);
    var acl = di.GetAccessControl();

    var authUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
    var rule = new FileSystemAccessRule(
        identity: authUsers,
        fileSystemRights: FileSystemRights.Modify | FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
        inheritanceFlags: InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
        propagationFlags: PropagationFlags.None,
        type: AccessControlType.Allow);

    acl.AddAccessRule(rule);
    di.SetAccessControl(acl);
}

static void CreateService(string serviceName, string displayName, string binPath)
{
    // sc.exe parsing of the create command requires the syntax
    // `param= value` with a SPACE after the `=`, no quotes around the
    // param name, and any value with spaces wrapped in quotes.
    var binPathQuoted = "\"" + binPath + "\"";
    var displayQuoted = "\"" + displayName + "\"";
    var args = $"create {serviceName} binPath= {binPathQuoted} start= auto DisplayName= {displayQuoted}";
    RunSc(args);
}

static void StartService(string serviceName)
{
    using var sc = new ServiceController(serviceName);
    if (sc.Status == ServiceControllerStatus.Running) return;
    if (sc.Status == ServiceControllerStatus.StartPending)
    {
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        return;
    }

    sc.Start();
    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
}

static void RunSc(string arguments)
{
    var psi = new ProcessStartInfo("sc.exe", arguments)
    {
        UseShellExecute        = false,
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        CreateNoWindow         = true,
    };

    using var proc = Process.Start(psi)
        ?? throw new InvalidOperationException("Could not launch sc.exe.");

    var stdout = proc.StandardOutput.ReadToEnd();
    var stderr = proc.StandardError.ReadToEnd();
    proc.WaitForExit();

    if (proc.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"sc.exe exited with code {proc.ExitCode}. " +
            $"Args: {arguments}. " +
            $"Stdout: {stdout.Trim()}. Stderr: {stderr.Trim()}.");
    }
}

// =====================================================================
// Types
// =====================================================================

internal enum ServiceState
{
    NotInstalled,
    InstalledStopped,
    Running,
}
