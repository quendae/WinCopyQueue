using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WinCopyQueue.Core;
using MessageBox = System.Windows.MessageBox;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WinCopyQueue;

public partial class QueueWindow : Window, INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(5);
    private readonly TransferQueueService _queue;
    private readonly DispatcherTimer _hideTimer;
    private bool _keepOpen;
    private bool _allowClose;
    private bool _disposed;
    private bool _hasClosed;
    private string _summaryText = "Brak aktywnych transferów";
    private string _pauseButtonText = "Wstrzymaj";
    private bool _pauseButtonEnabled;
    private Visibility _emptyVisibility = Visibility.Visible;
    private Visibility _listVisibility = Visibility.Collapsed;
    private bool _canClearCompleted;
    private bool _verifyIntegrity;
    private LanguageOption _selectedLanguage;

    public QueueWindow(TransferQueueService queue)
    {
        _selectedLanguage = Localization.Languages.First(item => item.Code == Localization.CurrentLanguage);
        _verifyIntegrity = AppSettings.Current.VerifyIntegrity;
        InitializeComponent();
        _queue = queue;
        DataContext = this;
        _hideTimer = new DispatcherTimer { Interval = AutoHideDelay };
        _hideTimer.Tick += HideTimerOnTick;
        _queue.SessionChanged += QueueOnSessionChanged;
        _queue.SessionRemoved += QueueOnSessionRemoved;
        Localization.LanguageChanged += LocalizationOnLanguageChanged;

        foreach (var snapshot in _queue.GetSessions())
        {
            ApplySnapshot(snapshot, showForNewSession: false);
        }

        RefreshHeader();
    }

    public ObservableCollection<QueueSessionRow> Sessions { get; } = [];
    public IReadOnlyList<LanguageOption> Languages => Localization.Languages;
    public string this[string key] => Localization.Text(key);
    public string VersionText => Localization.Format("Version", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || EqualityComparer<LanguageOption>.Default.Equals(_selectedLanguage, value))
            {
                return;
            }
            _selectedLanguage = value;
            Localization.SetLanguage(value.Code);
            AppSettings.Current.Language = value.Code;
            AppSettings.Save();
            try { ShellIntegration.Install(); } catch (Exception exception) { AppLog.Write("Nie udało się odświeżyć języka integracji z Explorerem.", exception); }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLanguage)));
        }
    }

    public bool VerifyIntegrity
    {
        get => _verifyIntegrity;
        set
        {
            if (!SetField(ref _verifyIntegrity, value))
            {
                return;
            }
            AppSettings.Current.VerifyIntegrity = value;
            AppSettings.Save();
        }
    }

    public bool CanClearCompleted
    {
        get => _canClearCompleted;
        private set => SetField(ref _canClearCompleted, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetField(ref _summaryText, value);
    }

    public string PauseButtonText
    {
        get => _pauseButtonText;
        private set => SetField(ref _pauseButtonText, value);
    }

    public bool PauseButtonEnabled
    {
        get => _pauseButtonEnabled;
        private set => SetField(ref _pauseButtonEnabled, value);
    }

    public Visibility EmptyVisibility
    {
        get => _emptyVisibility;
        private set => SetField(ref _emptyVisibility, value);
    }

    public Visibility ListVisibility
    {
        get => _listVisibility;
        private set => SetField(ref _listVisibility, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ShowQueue(bool keepOpen = true)
    {
        if (_disposed || _hasClosed)
        {
            return;
        }

        _keepOpen = keepOpen;
        _hideTimer.Stop();
        RefreshHeader();

        if (!IsVisible)
        {
            Show();
        }

        PositionNearTray();

        // Krótkie podniesienie ponad inne okna pokazuje panel bez odbierania fokusu Explorerowi.
        Topmost = true;
        Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed && !_hasClosed)
            {
                Topmost = false;
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    public void Dispose()
    {
        BeginShutdown();
        if (_hasClosed)
        {
            return;
        }

        Close();
    }

    public void BeginShutdown()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.SessionChanged -= QueueOnSessionChanged;
        _queue.SessionRemoved -= QueueOnSessionRemoved;
        Localization.LanguageChanged -= LocalizationOnLanguageChanged;
        _hideTimer.Stop();
        _hideTimer.Tick -= HideTimerOnTick;
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideQueue();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hasClosed = true;
        BeginShutdown();
        base.OnClosed(e);
    }

    private void QueueOnSessionChanged(object? sender, TransferSessionSnapshot snapshot)
    {
        if (_disposed || _hasClosed)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!_disposed && !_hasClosed)
                {
                    ApplySnapshot(snapshot, showForNewSession: true);
                }
            });
            return;
        }

        ApplySnapshot(snapshot, showForNewSession: true);
    }

    private void QueueOnSessionRemoved(object? sender, Guid sessionId)
    {
        if (_disposed || _hasClosed)
        {
            return;
        }
        Dispatcher.BeginInvoke(() =>
        {
            var row = Sessions.FirstOrDefault(item => item.Id == sessionId);
            if (row is not null)
            {
                Sessions.Remove(row);
                RefreshHeader();
            }
        });
    }

    private void LocalizationOnLanguageChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VersionText)));
        foreach (var row in Sessions)
        {
            row.RefreshLocalization();
            if (row.IsExpanded)
            {
                row.SetFiles(_queue.GetSessionFiles(row.Id));
            }
        }
        RefreshHeader();
    }

    private void ApplySnapshot(TransferSessionSnapshot snapshot, bool showForNewSession)
    {
        var row = Sessions.FirstOrDefault(item => item.Id == snapshot.Id);
        var isNew = row is null;
        var wasActive = row?.IsActive ?? false;
        var refreshExpandedFiles = row?.NeedsFileListRefresh(snapshot) ?? false;

        if (row is null)
        {
            row = new QueueSessionRow(snapshot);
            var firstFinished = Sessions.TakeWhile(item => item.IsActive).Count();
            Sessions.Insert(firstFinished, row);
        }
        else
        {
            row.Update(snapshot);
            if (refreshExpandedFiles)
            {
                row.SetFiles(_queue.GetSessionFiles(snapshot.Id));
            }
        }

        if (wasActive && !row.IsActive)
        {
            Sessions.Remove(row);
            var firstFinished = Sessions.TakeWhile(item => item.IsActive).Count();
            Sessions.Insert(firstFinished, row);
        }

        RefreshHeader();

        if (isNew && row.IsActive && showForNewSession)
        {
            ShowQueue(keepOpen: false);
        }

        if (Sessions.Any(item => item.IsActive))
        {
            _hideTimer.Stop();
        }
        else if (IsVisible && !_keepOpen)
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }

    private void RefreshHeader()
    {
        var active = Sessions.Count(item => item.IsActive);
        SummaryText = active switch
        {
            0 => Localization.Text("SummaryNone"),
            1 => Localization.Text("SummaryOne"),
            _ => Localization.Format("SummaryMany", active)
        };
        PauseButtonText = _queue.IsPaused ? Localization.Text("Resume") : Localization.Text("Pause");
        PauseButtonEnabled = active > 0;
        CanClearCompleted = Sessions.Any(item => item.State == TransferSessionState.Completed);
        EmptyVisibility = Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListVisibility = Sessions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_queue.IsPaused)
        {
            _queue.Resume();
        }
        else
        {
            _queue.Pause();
        }
        RefreshHeader();
    }

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: QueueSessionRow row })
        {
            return;
        }

        row.SetExpanded(!row.IsExpanded);
        if (row.IsExpanded)
        {
            row.SetFiles(_queue.GetSessionFiles(row.Id));
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QueueSessionRow row } && row.IsActive)
        {
            _queue.Cancel(row.Id);
        }
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e) => _queue.ClearCompleted();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: QueueSessionRow row })
        {
            return;
        }

        var message = row.State == TransferSessionState.Paused
            ? Localization.Text("RemovePausedConfirm")
            : Localization.Text("RemoveFinishedConfirm");
        if (MessageBox.Show(message, Localization.Text("RemoveConfirmTitle"), MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _queue.Remove(row.Id);
        }
    }

    private void FilePause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: QueueFileRow file })
        {
            return;
        }

        if (file.State == TransferFileState.Paused)
        {
            _queue.ResumeFile(file.SessionId, file.FullPath);
        }
        else
        {
            _queue.PauseFile(file.SessionId, file.FullPath);
        }
    }

    private void FileCancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QueueFileRow file })
        {
            _queue.CancelFile(file.SessionId, file.FullPath);
        }
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideQueue();

    private void HideQueue()
    {
        if (_disposed || _hasClosed)
        {
            return;
        }

        _keepOpen = false;
        _hideTimer.Stop();
        Hide();
    }

    private void HideTimerOnTick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        if (!_disposed && !_hasClosed && !_keepOpen && !Sessions.Any(item => item.IsActive))
        {
            Hide();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsVisible)
        {
            PositionNearTray();
        }
    }

    private void PositionNearTray()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 12, workArea.Right - ActualWidth - 16);
        Top = Math.Max(workArea.Top + 12, workArea.Bottom - ActualHeight - 16);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class QueueSessionRow : INotifyPropertyChanged
{
    private static readonly MediaBrush ActiveBrush = new MediaSolidColorBrush(MediaColor.FromRgb(88, 185, 240));
    private static readonly MediaBrush PausedBrush = new MediaSolidColorBrush(MediaColor.FromRgb(246, 196, 83));
    private static readonly MediaBrush CompletedBrush = new MediaSolidColorBrush(MediaColor.FromRgb(99, 210, 143));
    private static readonly MediaBrush FailedBrush = new MediaSolidColorBrush(MediaColor.FromRgb(239, 111, 115));
    private static readonly MediaBrush QueuedBrush = new MediaSolidColorBrush(MediaColor.FromRgb(126, 151, 172));

    private string _sessionTitle = string.Empty;
    private string _stateText = string.Empty;
    private string _destinationText = string.Empty;
    private string _destinationPath = string.Empty;
    private double _progress;
    private string _progressText = string.Empty;
    private string _speedText = "Prędkość: —";
    private string _currentFileText = string.Empty;
    private string? _currentFilePath;
    private MediaBrush _stateBrush = ActiveBrush;
    private Visibility _upcomingVisibility = Visibility.Collapsed;
    private Visibility _expandedVisibility = Visibility.Collapsed;
    private Visibility _cancelVisibility = Visibility.Visible;
    private Visibility _removeVisibility = Visibility.Collapsed;
    private bool _isActive;
    private bool _isExpanded;
    private bool _hasUpcoming;
    private string _expandButtonText = "Pokaż listę plików";
    private IReadOnlyList<QueueFileRow> _files = [];
    private int _lastCompletedFiles;
    private long _lastFileStateVersion;
    private string? _lastCurrentFile;
    private TransferSessionState _lastState;
    private long _lastSpeedBytes;
    private long _lastSpeedTimestamp = Stopwatch.GetTimestamp();
    private double _smoothedBytesPerSecond;
    private TransferSessionSnapshot _snapshot;
    private TransferSessionState _state;

    public QueueSessionRow(TransferSessionSnapshot snapshot)
    {
        Id = snapshot.Id;
        _snapshot = snapshot;
        _lastState = snapshot.State;
        _lastSpeedBytes = snapshot.TransferredBytes;
        Update(snapshot);
    }

    public Guid Id { get; }
    public ObservableCollection<string> UpcomingFiles { get; } = [];
    public string SessionTitle { get => _sessionTitle; private set => SetField(ref _sessionTitle, value); }
    public string StateText { get => _stateText; private set => SetField(ref _stateText, value); }
    public string DestinationText { get => _destinationText; private set => SetField(ref _destinationText, value); }
    public string DestinationPath { get => _destinationPath; private set => SetField(ref _destinationPath, value); }
    public double Progress { get => _progress; private set => SetField(ref _progress, value); }
    public string ProgressText { get => _progressText; private set => SetField(ref _progressText, value); }
    public string SpeedText { get => _speedText; private set => SetField(ref _speedText, value); }
    public string CurrentFileText { get => _currentFileText; private set => SetField(ref _currentFileText, value); }
    public string? CurrentFilePath { get => _currentFilePath; private set => SetField(ref _currentFilePath, value); }
    public MediaBrush StateBrush { get => _stateBrush; private set => SetField(ref _stateBrush, value); }
    public Visibility UpcomingVisibility { get => _upcomingVisibility; private set => SetField(ref _upcomingVisibility, value); }
    public Visibility ExpandedVisibility { get => _expandedVisibility; private set => SetField(ref _expandedVisibility, value); }
    public Visibility CancelVisibility { get => _cancelVisibility; private set => SetField(ref _cancelVisibility, value); }
    public Visibility RemoveVisibility { get => _removeVisibility; private set => SetField(ref _removeVisibility, value); }
    public bool IsActive { get => _isActive; private set => SetField(ref _isActive, value); }
    public bool IsExpanded { get => _isExpanded; private set => SetField(ref _isExpanded, value); }
    public string ExpandButtonText { get => _expandButtonText; private set => SetField(ref _expandButtonText, value); }
    public IReadOnlyList<QueueFileRow> Files { get => _files; private set => SetField(ref _files, value); }
    public TransferSessionState State { get => _state; private set => SetField(ref _state, value); }
    public string NextText => Localization.Text("Next");
    public string CancelButtonText => Localization.Text("Cancel");
    public string RemoveButtonText => Localization.Text("Remove");
    public string CancelToolTip => Localization.Text("CancelTip");
    public string RemoveToolTip => Localization.Text("RemoveTip");

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(TransferSessionSnapshot snapshot)
    {
        _snapshot = snapshot;
        UpdateSpeed(snapshot);
        State = snapshot.State;
        IsActive = snapshot.State is TransferSessionState.Preparing or TransferSessionState.Queued or
            TransferSessionState.Running or TransferSessionState.Paused;
        CancelVisibility = IsActive ? Visibility.Visible : Visibility.Collapsed;
        RemoveVisibility = snapshot.State is TransferSessionState.Paused or TransferSessionState.Canceled or TransferSessionState.Failed
            ? Visibility.Visible
            : Visibility.Collapsed;
        SessionTitle = $"{OperationName(snapshot.Operation)} • {FileCount(snapshot.TotalFiles)}";
        StateText = StateName(snapshot.State);
        StateBrush = StateColor(snapshot.State);
        DestinationPath = snapshot.Destination;
        DestinationText = $"{Localization.Text("Destination")}: {snapshot.Destination}";
        Progress = snapshot.Progress;
        ProgressText = BuildProgressText(snapshot);
        CurrentFilePath = snapshot.CurrentFile;
        CurrentFileText = BuildCurrentFileText(snapshot);
        ExpandButtonText = IsExpanded
            ? $"{Localization.Text("HideList")}  ▴"
            : snapshot.TotalFiles > 0 ? $"{Localization.Text("FileList")} ({snapshot.TotalFiles})  ▾" : $"{Localization.Text("FileList")}  ▾";

        var upcoming = snapshot.PendingFiles
            .Where(path => !string.Equals(path, snapshot.CurrentFile, StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .Select(path => $"•  {DisplayName(path)}")
            .ToArray();
        UpcomingFiles.Clear();
        foreach (var file in upcoming)
        {
            UpcomingFiles.Add(file);
        }
        _hasUpcoming = upcoming.Length > 0;
        RefreshSectionVisibility();

        _lastCompletedFiles = snapshot.CompletedFiles;
        _lastFileStateVersion = snapshot.FileStateVersion;
        _lastCurrentFile = snapshot.CurrentFile;
        _lastState = snapshot.State;
    }

    public bool NeedsFileListRefresh(TransferSessionSnapshot snapshot) =>
        IsExpanded && (_lastCompletedFiles != snapshot.CompletedFiles ||
                       _lastFileStateVersion != snapshot.FileStateVersion ||
                       !string.Equals(_lastCurrentFile, snapshot.CurrentFile, StringComparison.OrdinalIgnoreCase) ||
                       _lastState != snapshot.State);

    public void RefreshLocalization()
    {
        Update(_snapshot);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NextText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CancelButtonText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemoveButtonText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CancelToolTip)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemoveToolTip)));
    }

    public void SetExpanded(bool expanded)
    {
        IsExpanded = expanded;
        ExpandButtonText = expanded
            ? $"{Localization.Text("HideList")}  ▴"
            : $"{Localization.Text("FileList")} ({Math.Max(Files.Count, 0)})  ▾";
        RefreshSectionVisibility();
    }

    public void SetFiles(IReadOnlyList<TransferFileSnapshot> files)
    {
        Files = files
            .Select(file => new QueueFileRow(
                Id,
                DisplayName(file.Path),
                file.Path,
                FileStateName(file.State),
                FileStateColor(file.State),
                file.State,
                file.CanControl))
            .ToArray();
        if (!IsExpanded)
        {
            ExpandButtonText = $"{Localization.Text("FileList")} ({Files.Count})  ▾";
        }
    }

    private void RefreshSectionVisibility()
    {
        ExpandedVisibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        UpcomingVisibility = !IsExpanded && _hasUpcoming ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSpeed(TransferSessionSnapshot snapshot)
    {
        var now = Stopwatch.GetTimestamp();
        if (snapshot.IsVerifying)
        {
            SpeedText = $"{Localization.Text("Speed")}: {Localization.Text("Verifying")}";
            _smoothedBytesPerSecond = 0;
        }
        else if (snapshot.State == TransferSessionState.Running)
        {
            if (_lastState != TransferSessionState.Running)
            {
                _smoothedBytesPerSecond = 0;
            }
            else
            {
                var elapsed = (now - _lastSpeedTimestamp) / (double)Stopwatch.Frequency;
                var transferred = snapshot.TransferredBytes - _lastSpeedBytes;
                if (elapsed > 0 && transferred > 0)
                {
                    var instantaneous = transferred / elapsed;
                    _smoothedBytesPerSecond = _smoothedBytesPerSecond <= 0
                        ? instantaneous
                        : _smoothedBytesPerSecond * 0.65 + instantaneous * 0.35;
                }
            }

            SpeedText = _smoothedBytesPerSecond > 0
                ? $"{Localization.Text("Speed")}: {FormatRate(_smoothedBytesPerSecond)}"
                : $"{Localization.Text("Speed")}: {Localization.Text("Calculating")}";
        }
        else
        {
            SpeedText = snapshot.State == TransferSessionState.Paused
                ? $"{Localization.Text("Speed")}: {Localization.Text("Paused")}"
                : $"{Localization.Text("Speed")}: —";
            if (snapshot.State != TransferSessionState.Paused)
            {
                _smoothedBytesPerSecond = 0;
            }
        }

        _lastSpeedTimestamp = now;
        _lastSpeedBytes = snapshot.TransferredBytes;
    }

    private static string BuildCurrentFileText(TransferSessionSnapshot snapshot)
    {
        if (snapshot.State == TransferSessionState.Failed)
        {
            return snapshot.Error ?? Localization.Text("FailedInfo");
        }
        if (snapshot.State == TransferSessionState.Canceled)
        {
            return Localization.Text("CanceledInfo");
        }
        if (snapshot.State == TransferSessionState.Completed)
        {
            return Localization.Text("AllReady");
        }
        if (!string.IsNullOrWhiteSpace(snapshot.CurrentFile))
        {
            return DisplayName(snapshot.CurrentFile);
        }
        return snapshot.State == TransferSessionState.Preparing
            ? Localization.Text("Preparing")
            : Localization.Text("Waiting");
    }

    private static string BuildProgressText(TransferSessionSnapshot snapshot)
    {
        var files = Localization.Format("FilesProgress", snapshot.ProcessedFiles, snapshot.TotalFiles);
        if (snapshot.TotalBytes <= 0)
        {
            return $"{snapshot.Progress:0}%  •  {files}";
        }
        return $"{snapshot.Progress:0}%  •  {files}  •  {FormatBytes(snapshot.TransferredBytes)} / {FormatBytes(snapshot.TotalBytes)}";
    }

    private static string FormatRate(double bytesPerSecond)
    {
        var safeValue = Math.Min(Math.Max(0, bytesPerSecond), long.MaxValue);
        return $"{FormatBytes((long)safeValue)}/s";
    }

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
        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }

    private static string DisplayName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string FileCount(int count) => Localization.Format("Files", count);

    private static string OperationName(TransferOperation operation) =>
        operation == TransferOperation.Copy ? Localization.Text("Copying") : Localization.Text("Moving");

    private static string StateName(TransferSessionState state) => state switch
    {
        TransferSessionState.Preparing => Localization.Text("StatePreparing"),
        TransferSessionState.Queued => Localization.Text("StateQueued"),
        TransferSessionState.Running => Localization.Text("StateRunning"),
        TransferSessionState.Paused => Localization.Text("StatePaused"),
        TransferSessionState.Completed => Localization.Text("StateCompleted"),
        TransferSessionState.Failed => Localization.Text("StateFailed"),
        TransferSessionState.Canceled => Localization.Text("StateCanceled"),
        _ => state.ToString().ToUpperInvariant()
    };

    private static string FileStateName(TransferFileState state) => state switch
    {
        TransferFileState.Queued => Localization.Text("StateWaiting"),
        TransferFileState.Running => Localization.Text("StateRunning"),
        TransferFileState.Paused => Localization.Text("StatePaused"),
        TransferFileState.Completed => Localization.Text("StateCompleted"),
        TransferFileState.Skipped => Localization.Text("StateSkipped"),
        TransferFileState.Canceled => Localization.Text("StateCanceled"),
        TransferFileState.Failed => Localization.Text("StateFailed"),
        _ => state.ToString().ToUpperInvariant()
    };

    private static MediaBrush StateColor(TransferSessionState state) => state switch
    {
        TransferSessionState.Paused => PausedBrush,
        TransferSessionState.Completed => CompletedBrush,
        TransferSessionState.Failed or TransferSessionState.Canceled => FailedBrush,
        _ => ActiveBrush
    };

    private static MediaBrush FileStateColor(TransferFileState state) => state switch
    {
        TransferFileState.Running => ActiveBrush,
        TransferFileState.Paused => PausedBrush,
        TransferFileState.Completed => CompletedBrush,
        TransferFileState.Skipped => QueuedBrush,
        TransferFileState.Failed or TransferFileState.Canceled => FailedBrush,
        _ => QueuedBrush
    };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class QueueFileRow
{
    public QueueFileRow(
        Guid sessionId,
        string displayName,
        string fullPath,
        string stateText,
        MediaBrush stateBrush,
        TransferFileState state,
        bool canControl)
    {
        SessionId = sessionId;
        DisplayName = displayName;
        FullPath = fullPath;
        StateText = stateText;
        StateBrush = stateBrush;
        State = state;
        PauseButtonText = state == TransferFileState.Paused ? Localization.Text("Resume") : Localization.Text("Pause");
        CancelText = Localization.Text("Cancel");
        PauseToolTip = Localization.Text("FilePauseTip");
        CancelToolTip = Localization.Text("FileCancelTip");
        var active = canControl &&
                     (state is TransferFileState.Queued or TransferFileState.Running or TransferFileState.Paused);
        ControlsVisibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    public Guid SessionId { get; }
    public string DisplayName { get; }
    public string FullPath { get; }
    public string StateText { get; }
    public MediaBrush StateBrush { get; }
    public TransferFileState State { get; }
    public string PauseButtonText { get; }
    public string CancelText { get; }
    public string PauseToolTip { get; }
    public string CancelToolTip { get; }
    public Visibility ControlsVisibility { get; }
}
