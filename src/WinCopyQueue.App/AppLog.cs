using System.IO;

namespace WinCopyQueue;

internal static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinCopyQueue");
    public static readonly string FilePath = Path.Combine(LogDirectory, "WinCopyQueue.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                var entry = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | {message}";
                if (exception is not null)
                {
                    entry += $" | {exception.GetType().Name}: {exception.Message}";
                }
                File.AppendAllText(FilePath, entry + Environment.NewLine);
            }
        }
        catch
        {
            // Dziennik nigdy nie może zatrzymać kolejki.
        }
    }
}
