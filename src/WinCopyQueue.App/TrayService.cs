using System.Collections.Concurrent;
using System.Drawing;
using System.Windows;
using WinCopyQueue.Core;

namespace WinCopyQueue;

public sealed class TrayService : IDisposable
{
    private readonly TransferQueueService _queue;
    private readonly Action _repairIntegration;
    private readonly Action _showQueue;
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu;
    private readonly System.Windows.Forms.ToolStripMenuItem _statusItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _pauseItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _startupItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _showItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _repairItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _exitItem;
    private readonly ConcurrentDictionary<Guid, TransferSessionState> _states = new();
    private bool _shutdownStarted;
    private bool _disposed;

    public TrayService(TransferQueueService queue, Action showQueue, Action exit, Action repairIntegration)
    {
        _queue = queue;
        _showQueue = showQueue;
        _repairIntegration = repairIntegration;

        _menu = new System.Windows.Forms.ContextMenuStrip();
        _statusItem = new System.Windows.Forms.ToolStripMenuItem(Localization.Text("NoTransfers")) { Enabled = false };
        _pauseItem = new System.Windows.Forms.ToolStripMenuItem(Localization.Text("PauseQueue"), null, (_, _) => RunIfActive(TogglePause));
        _startupItem = new System.Windows.Forms.ToolStripMenuItem(Localization.Text("RunAtStartup"), null, (_, _) => RunIfActive(ToggleStartup));
        _showItem = new System.Windows.Forms.ToolStripMenuItem(Localization.Text("ShowQueue"), null, (_, _) => RunIfActive(_showQueue));
        _repairItem = new System.Windows.Forms.ToolStripMenuItem(Localization.Text("RepairExplorer"), null, (_, _) => RunIfActive(_repairIntegration));
        _exitItem = new System.Windows.Forms.ToolStripMenuItem(Localization.Text("Exit"), null, (_, _) =>
        {
            if (_shutdownStarted)
            {
                return;
            }

            BeginShutdown();
            exit();
        });
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add(_showItem);
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(_repairItem);
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add(_exitItem);
        _menu.Opening += (_, e) =>
        {
            if (_shutdownStarted)
            {
                e.Cancel = true;
                return;
            }
            RunIfActive(UpdateStatus);
        };

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = Localization.Text("TrayReady"),
            Icon = LoadApplicationIcon(),
            ContextMenuStrip = _menu,
            Visible = true
        };
        _icon.DoubleClick += IconOnDoubleClick;

        _queue.SessionChanged += QueueOnSessionChanged;
        _queue.SessionRemoved += QueueOnSessionRemoved;
        Localization.LanguageChanged += LocalizationOnLanguageChanged;
    }

    public void ShowInfo(string title, string text) => ShowBalloon(title, text, System.Windows.Forms.ToolTipIcon.Info);
    public void ShowError(string title, string text) => ShowBalloon(title, text, System.Windows.Forms.ToolTipIcon.Error);

    public void BeginShutdown()
    {
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        _queue.SessionChanged -= QueueOnSessionChanged;
        _queue.SessionRemoved -= QueueOnSessionRemoved;
        Localization.LanguageChanged -= LocalizationOnLanguageChanged;
        _icon.DoubleClick -= IconOnDoubleClick;
        _menu.Close();
        _icon.Visible = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        BeginShutdown();
        _disposed = true;
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _menu.Dispose();
    }

    private void IconOnDoubleClick(object? sender, EventArgs e) => RunIfActive(_showQueue);

    private void RunIfActive(Action action)
    {
        if (_shutdownStarted || _disposed)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception exception)
        {
            AppLog.Write("Błąd obsługi ikony w trayu.", exception);
        }
    }

    private void TogglePause()
    {
        if (_queue.IsPaused)
        {
            _queue.Resume();
            _pauseItem.Text = Localization.Text("PauseQueue");
        }
        else
        {
            _queue.Pause();
            _pauseItem.Text = Localization.Text("ResumeQueue");
        }
        UpdateStatus();
    }

    private void ToggleStartup()
    {
        try
        {
            StartupIntegration.SetEnabled(!StartupIntegration.IsEnabled());
            _startupItem.Checked = StartupIntegration.IsEnabled();
        }
        catch (Exception exception)
        {
            ShowError("Nie udało się zmienić autostartu", exception.Message);
        }
    }

    private void QueueOnSessionChanged(object? sender, TransferSessionSnapshot snapshot)
    {
        if (_shutdownStarted || _disposed)
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_shutdownStarted || _disposed)
            {
                return;
            }

            _states.TryGetValue(snapshot.Id, out var previousState);
            _states[snapshot.Id] = snapshot.State;

            if (snapshot.State != previousState)
            {
                if (snapshot.State == TransferSessionState.Queued)
                {
                    ShowInfo(Localization.Text("Added"), $"{OperationName(snapshot.Operation)}: {Localization.Format("Files", snapshot.TotalFiles)}.");
                }
                else if (snapshot.State == TransferSessionState.Completed)
                {
                    ShowInfo(Localization.Text("Completed"), $"{OperationName(snapshot.Operation)}: {snapshot.Destination}");
                }
                else if (snapshot.State == TransferSessionState.Failed)
                {
                    ShowError(Localization.Text("TransferError"), snapshot.Error ?? Localization.Text("FailedInfo"));
                }
            }

            UpdateStatus();
        });
    }

    private void UpdateStatus()
    {
        _pauseItem.Text = _queue.IsPaused ? Localization.Text("ResumeQueue") : Localization.Text("PauseQueue");
        _startupItem.Checked = StartupIntegration.IsEnabled();
        var active = _states.Values.Count(state => state is TransferSessionState.Preparing or
            TransferSessionState.Queued or TransferSessionState.Running or TransferSessionState.Paused);
        _statusItem.Text = active switch
        {
            0 => Localization.Text("SummaryNone"),
            1 => Localization.Text("SummaryOne"),
            _ => Localization.Format("SummaryMany", active)
        };
        _icon.Text = active == 0 ? Localization.Text("TrayReady") : Localization.Format("TrayActive", active);
    }

    private void QueueOnSessionRemoved(object? sender, Guid sessionId)
    {
        _states.TryRemove(sessionId, out _);
        if (!_shutdownStarted && !_disposed)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(UpdateStatus);
        }
    }

    private void LocalizationOnLanguageChanged(object? sender, EventArgs e)
    {
        _showItem.Text = Localization.Text("ShowQueue");
        _startupItem.Text = Localization.Text("RunAtStartup");
        _repairItem.Text = Localization.Text("RepairExplorer");
        _exitItem.Text = Localization.Text("Exit");
        UpdateStatus();
    }

    private void ShowBalloon(string title, string text, System.Windows.Forms.ToolTipIcon icon)
    {
        if (_shutdownStarted || _disposed)
        {
            return;
        }
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text.Length <= 240 ? text : text[..237] + "...";
        _icon.BalloonTipIcon = icon;
        _icon.ShowBalloonTip(3500);
    }

    private static Icon LoadApplicationIcon()
    {
        var path = Environment.ProcessPath;
        var extracted = path is null ? null : Icon.ExtractAssociatedIcon(path);
        return extracted ?? (Icon)SystemIcons.Application.Clone();
    }

    private static string OperationName(TransferOperation operation) =>
        operation == TransferOperation.Copy ? Localization.Text("Copying") : Localization.Text("Moving");
}
