using System.ComponentModel;
using System.IO;
using System.Windows;
using WinCopyQueue.Core;

namespace WinCopyQueue;

public partial class ConflictWindow : Window
{
    private bool _decisionMade;

    public ConflictWindow(FileConflictSnapshot conflict)
    {
        InitializeComponent();
        SourceName = Path.GetFileName(conflict.SourcePath);
        SourcePath = conflict.SourcePath;
        SourceSizeText = $"{Localization.Text("Size")}: {FormatBytes(conflict.SourceSize)}";
        SourceDateText = $"{Localization.Text("Modified")}: {FormatDate(conflict.SourceModifiedUtc)}";
        DestinationName = Path.GetFileName(conflict.DestinationPath);
        DestinationPath = conflict.DestinationPath;
        DestinationSizeText = $"{Localization.Text("Size")}: {FormatBytes(conflict.DestinationSize)}";
        DestinationDateText = $"{Localization.Text("Modified")}: {FormatDate(conflict.DestinationModifiedUtc)}";
        DataContext = this;
    }

    public string SourceName { get; }
    public string SourcePath { get; }
    public string SourceSizeText { get; }
    public string SourceDateText { get; }
    public string DestinationName { get; }
    public string DestinationPath { get; }
    public string DestinationSizeText { get; }
    public string DestinationDateText { get; }
    public ConflictResolution Result { get; private set; } = ConflictResolution.Skip;
    public string this[string key] => Localization.Text(key);

    public void CancelFromQueue()
    {
        if (!_decisionMade)
        {
            Complete(ConflictResolution.CancelSession);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_decisionMade)
        {
            Result = ConflictResolution.Skip;
        }
        base.OnClosing(e);
    }

    private void Replace_Click(object sender, RoutedEventArgs e) =>
        Complete(ApplyToAllCheckBox.IsChecked == true
            ? ConflictResolution.ReplaceAll
            : ConflictResolution.Replace);

    private void Skip_Click(object sender, RoutedEventArgs e) =>
        Complete(ApplyToAllCheckBox.IsChecked == true
            ? ConflictResolution.SkipAll
            : ConflictResolution.Skip);

    private void CancelSession_Click(object sender, RoutedEventArgs e) =>
        Complete(ConflictResolution.CancelSession);

    private void Complete(ConflictResolution result)
    {
        Result = result;
        _decisionMade = true;
        DialogResult = true;
    }

    private static string FormatDate(DateTime utc) =>
        utc.ToLocalTime().ToString("g");

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.##} {units[unit]}";
    }
}
