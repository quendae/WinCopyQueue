using WinCopyQueue;
using WinCopyQueue.Core;

namespace WinCopyQueue.App.SmokeTests;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Localization.Initialize("pl");
        Assert(Localization.Languages.Count == 8 && Localization.Text("EmptyTitle") == "Kolejka jest pusta",
            "Lista języków lub polskie tłumaczenie nie zostały załadowane.");
        Localization.SetLanguage("ja");
        Assert(Localization.Text("EmptyTitle") == "キューは空です",
            "Zmiana języka w czasie działania nie odświeża tłumaczeń.");
        Localization.SetLanguage("pl");

        var conflict = new FileConflictSnapshot(
            Guid.NewGuid(),
            TransferOperation.Copy,
            @"C:\Source\example.txt",
            @"C:\Destination\example.txt",
            2048,
            1024,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-1));

        var window = new ConflictWindow(conflict);
        Assert(window.SourceSizeText.Contains("2 KB", StringComparison.Ordinal),
            "Dialog nie sformatował rozmiaru nowego pliku.");
        Assert(window.DestinationSizeText.Contains("1 KB", StringComparison.Ordinal),
            "Dialog nie sformatował rozmiaru istniejącego pliku.");
        window.ContentRendered += (_, _) => window.Close();
        window.ShowDialog();

        Console.WriteLine("PASS: dialog konfliktu ładuje się i pokazuje porównanie rozmiaru oraz daty.");

        var queue = new TransferQueueService();
        var queueWindow = new QueueWindow(queue);
        queueWindow.Show();
        queueWindow.BeginShutdown();
        queueWindow.Close();
        queueWindow.ShowQueue();
        Assert(!queueWindow.IsVisible,
            "Zamknięty panel kolejki został ponownie pokazany przez spóźnione zdarzenie traya.");
        queueWindow.Dispose();
        queue.DisposeAsync().AsTask().GetAwaiter().GetResult();

        Console.WriteLine("PASS: spóźnione wywołanie traya nie otwiera zamkniętego panelu kolejki.");
        Console.WriteLine("PASS: automatyczna lokalizacja i osiem języków są dostępne.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
