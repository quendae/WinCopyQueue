using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using WinCopyQueue.Core;

namespace WinCopyQueue;

public enum IpcCommandKind { Activate, Enqueue }

public sealed record IpcCommand(
    IpcCommandKind Kind,
    TransferOperation Operation = TransferOperation.Copy,
    string? Destination = null,
    IReadOnlyList<string>? Sources = null);

public static class CommandLineParser
{
    public static IpcCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new IpcCommand(IpcCommandKind.Activate);
        }

        if (args.Length >= 2 && args[0].Equals("--paste", StringComparison.OrdinalIgnoreCase))
        {
            if (!ClipboardTransferReader.TryRead(out var operation, out var sources))
            {
                throw new InvalidOperationException("Schowek nie zawiera plików ani folderów skopiowanych w Explorerze.");
            }
            return new IpcCommand(IpcCommandKind.Enqueue, operation, args[1], sources);
        }

        if (args.Length >= 3 &&
            (args[0].Equals("--copy", StringComparison.OrdinalIgnoreCase) ||
             args[0].Equals("--move", StringComparison.OrdinalIgnoreCase)))
        {
            var operation = args[0].Equals("--move", StringComparison.OrdinalIgnoreCase)
                ? TransferOperation.Move
                : TransferOperation.Copy;
            return new IpcCommand(IpcCommandKind.Enqueue, operation, args[1], args[2..]);
        }

        throw new ArgumentException(
            "Nieprawidłowe argumenty. Użyj: WinCopyQueue.exe --copy <cel> <źródła...>, " +
            "--move <cel> <źródła...> albo --paste <cel>.");
    }
}

public static class IpcClient
{
    private const string PipeName = "WinCopyQueue.Commands.v1";

    public static async Task SendAsync(IpcCommand command, CancellationToken cancellationToken = default)
    {
        AppLog.Write("IPC klient: łączę z pipe.");
        await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, cancellationToken).ConfigureAwait(false);
        AppLog.Write("IPC klient: połączono, wysyłam wiadomość.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(command);
        var header = BitConverter.GetBytes(payload.Length);
        await pipe.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await pipe.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
        AppLog.Write("IPC klient: wiadomość wysłana.");
    }

    internal static string Name => PipeName;
}

public sealed class IpcServer : IAsyncDisposable
{
    private readonly Func<IpcCommand, Task> _handler;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _serverTask;

    public IpcServer(Func<IpcCommand, Task> handler) => _handler = handler;

    public void Start() => _serverTask = Task.Run(ListenAsync);

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_serverTask is not null)
        {
            try { await _serverTask; }
            catch (OperationCanceledException) { }
        }
        _shutdown.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                AppLog.Write("IPC serwer: oczekuję na połączenie.");
                await using var pipe = new NamedPipeServerStream(
                    IpcClient.Name,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_shutdown.Token);
                AppLog.Write("IPC serwer: klient połączony.");
                var header = new byte[sizeof(int)];
                await pipe.ReadExactlyAsync(header, _shutdown.Token);
                var payloadLength = BitConverter.ToInt32(header);
                if (payloadLength is <= 0 or > 16 * 1024 * 1024)
                {
                    throw new InvalidDataException("Nieprawidłowa długość polecenia IPC.");
                }

                var payload = new byte[payloadLength];
                await pipe.ReadExactlyAsync(payload, _shutdown.Token);
                var command = JsonSerializer.Deserialize<IpcCommand>(payload);
                if (command is not null)
                {
                    AppLog.Write("IPC serwer: odebrano pełną wiadomość.");
                    await _handler(command);
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                AppLog.Write("IPC serwer: błąd pojedynczego wywołania.", exception);
            }
        }
    }
}
