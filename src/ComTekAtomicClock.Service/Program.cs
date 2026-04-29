using ComTekAtomicClock.Service;
using ComTekAtomicClock.Service.Ipc;

var builder = Host.CreateApplicationBuilder(args);

// Wire the host into the Windows Service control manager. With this call
// the same exe runs interactively (for `dotnet run` debugging) AND as the
// LocalSystem service registered by ComTekAtomicClock.ServiceInstaller
// (per requirements.txt § 1.6, § 1.9).
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ComTekAtomicClockSvc";
});

// IPC: stub request handler + named-pipe server (per § 2.4).
// StubIpcRequestHandler will be replaced with the real router when
// SyncWorker / confirmation flow land in subsequent commits.
builder.Services.AddSingleton<IIpcRequestHandler, StubIpcRequestHandler>();
builder.Services.AddHostedService<IpcServer>();

// Sync worker (still the dummy template for now; replaced in a later step).
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
