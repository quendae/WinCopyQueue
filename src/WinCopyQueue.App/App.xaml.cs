using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WinCopyQueue.Core;
using MessageBox = System.Windows.MessageBox;

namespace WinCopyQueue;

public partial class WinCopyQueueApplication : System.Windows.Application
{
    private const string MutexName = "Local\\WinCopyQueue.1070D10E-85FD-42A6-B82B-0C29FD823683";
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private IpcServer? _ipcServer;
    private TransferQueueService? _transferQueue;
    private ExplorerPasteHook? _explorerHook;
    private TrayService? _tray;
    private QueueWindow? _queueWindow;
    private int _shutdownStarted;
    private bool _servicesStopped;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppSettings.Load();
        Localization.Initialize(AppSettings.Current.Language);
        AppLog.Write($"Start procesu. Argumenty: {string.Join(' ', e.Args)}");

        IpcCommand command;
        try
        {
            command = CommandLineParser.Parse(e.Args);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "WinCopyQueue", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        _instanceMutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            _ownsMutex = _instanceMutex.WaitOne(0, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        if (!_ownsMutex)
        {
            AppLog.Write("Wykryto działającą instancję; przekazuję polecenie przez pipe.");
            ForwardToRunningInstance(command);
            Shutdown();
            return;
        }

        try
        {
            ShellIntegration.Install();
            AppLog.Write("Integracja z Explorerem została zapisana w rejestrze.");
        }
        catch (Exception exception)
        {
            AppLog.Write("Błąd instalacji integracji z Explorerem.", exception);
        }

        _transferQueue = new TransferQueueService(ResolveConflictAsync);
        _queueWindow = new QueueWindow(_transferQueue);
        _tray = new TrayService(
            _transferQueue,
            () => _queueWindow.ShowQueue(),
            ShutdownApplication,
            RepairExplorerIntegration);
        _ipcServer = new IpcServer(HandleIpcCommandAsync);
        _ipcServer.Start();

        _explorerHook = new ExplorerPasteHook((destination, operation, sources) =>
            HandleTransferAsync(operation, destination, sources));
        try
        {
            _explorerHook.Start();
            AppLog.Write("Hook Ctrl+V działa.");
        }
        catch (Exception exception)
        {
            AppLog.Write("Błąd uruchomienia hooka Ctrl+V.", exception);
            _tray.ShowError("Skrót Ctrl+V nie został podłączony", exception.Message);
        }

        _tray.ShowInfo("WinCopyQueue działa", "Kopiuj lub wytnij pliki w Explorerze, a następnie użyj Ctrl+V w folderze docelowym.");
        _ = HandleIpcCommandAsync(command);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Interlocked.Exchange(ref _shutdownStarted, 1);
        try
        {
            StopServicesAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            AppLog.Write("Błąd końcowego sprzątania aplikacji.", exception);
        }

        if (_instanceMutex is not null)
        {
            if (_ownsMutex)
            {
                _instanceMutex.ReleaseMutex();
            }
            _instanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    private void ForwardToRunningInstance(IpcCommand command)
    {
        try
        {
            IpcClient.SendAsync(command).GetAwaiter().GetResult();
            AppLog.Write("Przekazanie polecenia do głównej instancji zakończone.");
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Nie udało się przekazać operacji do WinCopyQueue.\n\n{exception.Message}",
                "WinCopyQueue", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Task HandleIpcCommandAsync(IpcCommand command)
    {
        AppLog.Write($"Odebrano polecenie IPC: {command.Kind}, źródeł: {command.Sources?.Count ?? 0}, cel: {command.Destination}");
        if (command.Kind != IpcCommandKind.Enqueue || command.Sources is not { Count: > 0 } ||
            string.IsNullOrWhiteSpace(command.Destination))
        {
            return Task.CompletedTask;
        }

        return Dispatcher.InvokeAsync(() =>
            HandleTransferAsync(command.Operation, command.Destination, command.Sources)).Task.Unwrap();
    }

    private async Task HandleTransferAsync(
        TransferOperation operation,
        string destination,
        IReadOnlyList<string> sources)
    {
        var result = await _transferQueue!.EnqueueAsync(new TransferRequest(
            operation,
            destination,
            sources,
            AppSettings.Current.VerifyIntegrity));
        AppLog.Write($"Dodano sesję {result.Id}: {operation}, plików: {result.TotalFiles}, stan: {result.State}.");
        if (result.State == TransferSessionState.Failed)
        {
            _tray?.ShowError("Nie udało się dodać transferu", result.Error ?? "Nieznany błąd.");
            return;
        }

        if (operation == TransferOperation.Move)
        {
            ClipboardTransferReader.TryClear();
        }
    }

    private void RepairExplorerIntegration()
    {
        try
        {
            ShellIntegration.Install();
            _tray?.ShowInfo("Integracja naprawiona", "Polecenie „Wklej z WinCopyQueue” zostało ponownie zarejestrowane.");
        }
        catch (Exception exception)
        {
            _tray?.ShowError("Nie udało się naprawić integracji", exception.Message);
        }
    }

    private Task<ConflictResolution> ResolveConflictAsync(
        FileConflictSnapshot conflict,
        CancellationToken cancellationToken)
    {
        AppLog.Write($"Wykryto konflikt pliku: {conflict.SourcePath} -> {conflict.DestinationPath}");
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ConflictResolution.CancelSession);
        }

        return Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var dialog = new ConflictWindow(conflict);
                if (_queueWindow?.IsVisible == true)
                {
                    dialog.Owner = _queueWindow;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }

                using var registration = cancellationToken.Register(() =>
                    Dispatcher.BeginInvoke(dialog.CancelFromQueue));
                AppLog.Write("Pokazuję okno konfliktu.");
                dialog.ShowDialog();
                AppLog.Write($"Decyzja konfliktu: {dialog.Result}.");
                return dialog.Result;
            }
            catch (Exception exception)
            {
                AppLog.Write("Nie udało się wyświetlić okna konfliktu; plik zostanie pominięty.", exception);
                return ConflictResolution.Skip;
            }
        }).Task;
    }

    private void ShutdownApplication()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return;
        }

        // NotifyIcon wywołuje tę metodę wewnątrz obsługi menu WinForms. Najpierw
        // odłączamy callbacki, a sprzątanie odkładamy do kolejki Dispatchera.
        _tray?.BeginShutdown();
        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await StopServicesAsync();
            }
            catch (Exception exception)
            {
                AppLog.Write("Błąd podczas zamykania aplikacji.", exception);
            }
            finally
            {
                Shutdown();
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    private async Task StopServicesAsync()
    {
        if (_servicesStopped)
        {
            return;
        }
        _servicesStopped = true;

        _tray?.Dispose();
        _tray = null;

        _explorerHook?.Dispose();
        _explorerHook = null;

        if (_ipcServer is not null)
        {
            await _ipcServer.DisposeAsync();
            _ipcServer = null;
        }

        _queueWindow?.BeginShutdown();

        if (_transferQueue is not null)
        {
            await _transferQueue.DisposeAsync();
            _transferQueue = null;
        }

        _queueWindow?.Dispose();
        _queueWindow = null;
    }
}
