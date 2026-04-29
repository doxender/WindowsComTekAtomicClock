// ComTekAtomicClock.Service.Ipc.IpcServer
//
// IHostedService that exposes the named pipe described in
// requirements.txt § 2.4. Loops accepting one client at a time;
// dispatches each incoming envelope through IIpcRequestHandler and
// writes any returned response back to the client.
//
// The pipe ACL (PipeSecurity below) grants ReadWrite to all
// interactive Users on the local machine and FullControl to the
// LocalSystem account that the Service itself runs under. Anonymous
// and remote connections are denied implicitly.

using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using ComTekAtomicClock.Shared.Ipc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ComTekAtomicClock.Service.Ipc;

[SupportedOSPlatform("windows")]
public sealed class IpcServer : BackgroundService
{
    private readonly ILogger<IpcServer> _logger;
    private readonly IIpcRequestHandler _handler;

    public IpcServer(ILogger<IpcServer> logger, IIpcRequestHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "IPC server starting on pipe '{PipeName}'.", PipeNames.UiToService);

        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("IPC client connected.");

                await HandleConnectionAsync(pipe, stoppingToken).ConfigureAwait(false);

                _logger.LogInformation("IPC client disconnected.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IPC accept-loop error; backing off 1 s.");
                try { await Task.Delay(1_000, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                pipe?.Dispose();
            }
        }

        _logger.LogInformation("IPC server stopped.");
    }

    /// <summary>
    /// Inner read/dispatch loop while a single client is connected.
    /// One request at a time; each is answered before the next is read.
    /// </summary>
    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        while (pipe.IsConnected && !ct.IsCancellationRequested)
        {
            IpcEnvelope? request;
            try
            {
                request = await IpcWireFormat.ReadAsync(pipe, ct).ConfigureAwait(false);
            }
            catch (EndOfStreamException eos)
            {
                _logger.LogDebug(eos, "IPC client closed mid-frame.");
                break;
            }

            if (request is null)
                break; // clean disconnect

            if (request.SchemaVersion != IpcSchema.CurrentVersion)
            {
                _logger.LogWarning(
                    "IPC request with unexpected schemaVersion={Got}, ours={Ours}; processing anyway.",
                    request.SchemaVersion, IpcSchema.CurrentVersion);
            }

            try
            {
                var response = await _handler.HandleAsync(request, ct).ConfigureAwait(false);
                if (response != null)
                    await IpcWireFormat.WriteAsync(pipe, response, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "IPC handler threw on request type={Type}; disconnecting client.",
                    request.Type);
                break;
            }
        }
    }

    /// <summary>
    /// Build a fresh pipe instance with the cross-user ACL described in
    /// requirements.txt § 2.4.
    /// </summary>
    private static NamedPipeServerStream CreatePipe()
    {
        var ps = new PipeSecurity();

        // Interactive Users on this machine: ReadWrite + Synchronize.
        var interactive = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
        ps.AddAccessRule(new PipeAccessRule(
            interactive,
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            AccessControlType.Allow));

        // The Service itself (LocalSystem) needs full control to manage
        // the pipe.
        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        ps.AddAccessRule(new PipeAccessRule(
            localSystem,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName: PipeNames.UiToService,
            direction: PipeDirection.InOut,
            maxNumberOfServerInstances: 4,
            transmissionMode: PipeTransmissionMode.Byte,
            options: PipeOptions.Asynchronous,
            inBufferSize: 64 * 1024,
            outBufferSize: 64 * 1024,
            pipeSecurity: ps);
    }
}
