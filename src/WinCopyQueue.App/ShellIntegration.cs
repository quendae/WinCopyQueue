using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace WinCopyQueue;

public static class ShellIntegration
{
    private const string BackgroundKey = @"Software\Classes\Directory\Background\shell\WinCopyQueue.Paste";
    private const string DirectoryKey = @"Software\Classes\Directory\shell\WinCopyQueue.Paste";
    private const string LegacyBackgroundKey = @"Software\Classes\Directory\Background\shell\QueuePaste";
    private const string LegacyDirectoryKey = @"Software\Classes\Directory\shell\QueuePaste";

    public static void Install()
    {
        RemoveLegacyRegistration();
        var launchCommand = GetLaunchCommand();
        var iconPath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        WriteVerb(BackgroundKey, launchCommand, iconPath, "%V");
        WriteVerb(DirectoryKey, launchCommand, iconPath, "%1");

        using var verification = Registry.CurrentUser.OpenSubKey(BackgroundKey + @"\command");
        if (verification?.GetValue(string.Empty) is not string command || string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("Windows nie potwierdził zapisu polecenia menu kontekstowego.");
        }
    }

    public static void Uninstall()
    {
        Registry.CurrentUser.DeleteSubKeyTree(BackgroundKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(DirectoryKey, throwOnMissingSubKey: false);
        RemoveLegacyRegistration();
    }

    private static void RemoveLegacyRegistration()
    {
        Registry.CurrentUser.DeleteSubKeyTree(LegacyBackgroundKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(LegacyDirectoryKey, throwOnMissingSubKey: false);
    }

    private static string GetLaunchCommand()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Nie można ustalić ścieżki programu.");

        if (Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return $"\"{processPath}\" \"{Assembly.GetExecutingAssembly().Location}\"";
        }

        return $"\"{processPath}\"";
    }

    private static void WriteVerb(string keyPath, string launchCommand, string iconPath, string targetToken)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue("MUIVerb", Localization.Text("ExplorerPaste"));
        key.SetValue("Icon", $"{iconPath},0");
        key.SetValue("MultiSelectModel", "Single");

        using var command = key.CreateSubKey("command");
        command.SetValue(string.Empty, $"{launchCommand} --paste \"{targetToken}\"");
    }
}
