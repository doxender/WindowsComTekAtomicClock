using ComTekAtomicClock.Service;

var builder = Host.CreateApplicationBuilder(args);

// Wire the host into the Windows Service control manager. With this call
// the same exe runs interactively (for `dotnet run` debugging) AND as the
// LocalSystem service registered by ComTekAtomicClock.ServiceInstaller
// (per requirements.txt § 1.6, § 1.9).
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ComTekAtomicClockSvc";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
