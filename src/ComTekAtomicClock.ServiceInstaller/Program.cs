// ComTekAtomicClock.ServiceInstaller
//
// The privileged helper described in requirements.txt § 1.9. Always
// runs elevated (app.manifest enforces requireAdministrator).
//
// Modes:
//
//   Install (default — no `--uninstall`):
//     - Service running                -> no-op, exit 0
//     - Service installed but stopped  -> sc.exe start
//     - Service not installed          -> create %ProgramData%\
//                                         ComTekAtomicClock\ with
//                                         broad ACLs, then sc.exe
//                                         create + start
//
//   Uninstall (`--uninstall`):
//     - Stops the service if running.
//     - sc.exe delete to remove the SCM registration.
//     - Removes %ProgramData%\ComTekAtomicClock\ (per-machine config).
//     - With `--purge-user-data`, also removes the per-user
//       %APPDATA%\ComTekAtomicClock\ folder.
//
// Resolves the path to ComTekAtomicClock.Service.exe in this order
// (install mode only):
//   1. Explicit `--service-exe <path>` command-line argument.
//   2. Sibling exe in the same directory as this helper (the MSIX
//      package layout per § 2.7 puts both exes in the same folder).
//
// Exits 0 on success, 1 on any failure with the error written to
// stderr. The UI surfaces non-zero exit + stderr per § 1.9.

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;

[assembly: SupportedOSPlatform("windows")]

const string ServiceName       = "ComTekAtomicClockSvc";
const string DisplayName       = "ComTek Atomic Clock — time sync";
const string ProgramDataFolder = "ComTekAtomicClock";

var uninstall      = args.Any(a => a == "--uninstall");
var purgeUserData  = args.Any(a => a == "--purge-user-data");

try
{
    if (uninstall)
        return RunUninstall(purgeUserData);
    else
        return RunInstall(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    if (ex.InnerException is not null)
        Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
    return 1;
}

// =====================================================================
// Install
// =====================================================================

static int RunInstall(string[] args)
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
            throw new InvalidOperationException($"Unexpected ServiceState: {state}");
    }
}

// =====================================================================
// Uninstall
// =====================================================================

static int RunUninstall(bool purgeUserData)
{
    var state = DetectServiceState(ServiceName);
    Console.Out.WriteLine($"Detected state: {state}");

    if (state == ServiceState.NotInstalled)
    {
        Console.Out.WriteLine($"Service '{ServiceName}' is not installed; nothing to remove from SCM.");
    }
    else
    {
        if (state == ServiceState.Running)
        {
            Console.Out.WriteLine("Stopping service...");
            try
            {
                using var sc = new ServiceController(ServiceName);
                if (sc.Status != ServiceControllerStatus.Stopped &&
                    sc.Status != ServiceControllerStatus.StopPending)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: stop failed: {ex.Message}; continuing.");
            }
            Console.Out.WriteLine("Service stopped.");
        }

        Console.Out.WriteLine($"Deleting SCM registration for '{ServiceName}'...");
        RunSc($"delete {ServiceName}");
        Console.Out.WriteLine("sc.exe delete completed.");
    }

    // Remove per-machine config.
    var programDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        ProgramDataFolder);
    if (Directory.Exists(programDataDir))
    {
        Console.Out.WriteLine($"Removing {programDataDir}...");
        try
        {
            Directory.Delete(programDataDir, recursive: true);
            Console.Out.WriteLine("Removed.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: could not remove {programDataDir}: {ex.Message}");
        }
    }
    else
    {
        Console.Out.WriteLine($"{programDataDir} does not exist; skipping.");
    }

    // Optionally remove per-user settings.json directory.
    if (purgeUserData)
    {
        var userAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ProgramDataFolder);
        if (Directory.Exists(userAppData))
        {
            Console.Out.WriteLine($"Removing user-data directory {userAppData}...");
            try
            {
                Directory.Delete(userAppData, recursive: true);
                Console.Out.WriteLine("User-data directory removed.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: could not remove {userAppData}: {ex.Message}");
            }
        }
        else
        {
            Console.Out.WriteLine($"{userAppData} does not exist; skipping.");
        }
    }
    else
    {
        Console.Out.WriteLine("User-data directory preserved (pass --purge-user-data to remove).");
    }

    Console.Out.WriteLine("Uninstall complete.");
    return 0;
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
    var binPathQuoted = "\"" + binPath + "\"";
    var displayQuoted = "\"" + displayName + "\"";
    // start= demand : on-demand start mode (NOT auto-start at boot).
    // The UI starts the service on app launch and stops it on app
    // exit; we don't want it consuming a service slot when nobody
    // is using the clock app.
    var args = $"create {serviceName} binPath= {binPathQuoted} start= demand DisplayName= {displayQuoted}";
    RunSc(args);

    // Grant Authenticated Users (AU) the rights to query, start, and
    // stop this service so the unprivileged UI can manage it across
    // app launches without UAC. Built-in Administrators (BA) keep
    // full control; LocalSystem (SY) keeps read+interrogate.
    //
    // SDDL access rights for services:
    //   CC = SERVICE_QUERY_CONFIG       LC = SERVICE_QUERY_STATUS
    //   SW = SERVICE_ENUMERATE_DEPENDENTS  LO = SERVICE_INTERROGATE
    //   RP = SERVICE_START               WP = SERVICE_STOP
    //   CR = SERVICE_USER_DEFINED_CONTROL  RC = READ_CONTROL
    //
    // AU gets CCLCSWRPWPLOCRRC: query/start/stop/interrogate/read.
    var sddl =
        "D:(A;;CCLCSWRPWPLOCRRC;;;AU)" +
        "(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)" +
        "(A;;CCLCSWLOCRRC;;;SY)";
    RunSc($"sdset {serviceName} {sddl}");
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
