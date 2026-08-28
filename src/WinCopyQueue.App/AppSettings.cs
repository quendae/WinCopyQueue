using System.IO;
using System.Text.Json;

namespace WinCopyQueue;

public sealed class UserSettings
{
    public string? Language { get; set; }
    public bool VerifyIntegrity { get; set; }
}

public static class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinCopyQueue",
        "settings.json");

    public static UserSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                Current = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath)) ?? new UserSettings();
            }
        }
        catch (Exception exception)
        {
            AppLog.Write("Nie udało się odczytać ustawień; używam wartości domyślnych.", exception);
            Current = new UserSettings();
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            AppLog.Write("Nie udało się zapisać ustawień.", exception);
        }
    }
}
