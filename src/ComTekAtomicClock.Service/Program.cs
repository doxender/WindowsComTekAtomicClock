using ComTekAtomicClock.Service.Ipc;
using ComTekAtomicClock.Service.Sync;

var builder = Host.CreateApplicationBuilder(args);

// Wire the host into the Windows Service control manager. With this call
// the same exe runs interactively (for `dotnet run` debugging) AND as the
// LocalSystem service registered by ComTekAtomicClock.ServiceInstaller
// (per requirements.txt § 1.6, § 1.9).
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ComTekAtomicClockSvc";
});

// Shared sync state, queried by the IPC handler.
builder.Services.AddSingleton<SyncStateProvider>();

// IPC: state-aware request handler + named-pipe server (per § 2.4).
builder.Services.AddSingleton<IIpcRequestHandler, LiveIpcRequestHandler>();
builder.Services.AddHostedService<IpcServer>();

// SNTP sync loop against the NIST stratum-1 pool (per § 1.5).
builder.Services.AddHostedService<SyncWorker>();

var host = builder.Build();
host.Run();
