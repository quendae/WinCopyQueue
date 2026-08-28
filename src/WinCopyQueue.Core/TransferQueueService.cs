using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;

namespace WinCopyQueue.Core;

public sealed class TransferQueueService : IAsyncDisposable
{
    private const int BufferSize = 1024 * 1024;
    private readonly ConcurrentQueue<TransferSession> _pending = new();
    private readonly ConcurrentDictionary<Guid, TransferSession> _sessions = new();
    private readonly ConcurrentBag<TransferSession> _retiredSessions = [];
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly AsyncPauseGate _pauseGate = new();
    private readonly Func<FileConflictSnapshot, CancellationToken, Task<ConflictResolution>> _conflictResolver;
    private readonly Task _worker;

    public TransferQueueService(
        Func<FileConflictSnapshot, CancellationToken, Task<ConflictResolution>>? conflictResolver = null)
    {
        _conflictResolver = conflictResolver ?? ((_, _) => Task.FromResult(ConflictResolution.Skip));
        _worker = Task.Run(ProcessQueueAsync);
    }

    public event EventHandler<TransferSessionSnapshot>? SessionChanged;
    public event EventHandler<Guid>? SessionRemoved;

    public bool IsPaused { get; private set; }

    public async Task<TransferSessionSnapshot> EnqueueAsync(
        TransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = new TransferSession(request);
        _sessions[session.Id] = session;
        Publish(session);

        try
        {
            using var planningCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                session.Cancellation.Token,
                _shutdown.Token);
            await Task.Run(
                () => BuildPlan(session, planningCancellation.Token),
                planningCancellation.Token);
            session.IsPlanComplete = true;
            session.State = TransferSessionState.Queued;
            Publish(session);
            _pending.Enqueue(session);
            _queueSignal.Release();
        }
        catch (OperationCanceledException)
        {
            session.State = TransferSessionState.Canceled;
            Publish(session);
        }
        catch (Exception exception)
        {
            session.State = TransferSessionState.Failed;
            session.Error = exception.Message;
            Publish(session);
        }

        return session.Snapshot();
    }

    public void Pause()
    {
        if (IsPaused)
        {
            return;
        }

        IsPaused = true;
        _pauseGate.Pause();
        foreach (var session in _sessions.Values.Where(item => item.State == TransferSessionState.Running))
        {
            session.State = TransferSessionState.Paused;
            Publish(session);
        }
    }

    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
        foreach (var session in _sessions.Values.Where(item => item.State == TransferSessionState.Paused))
        {
            if (!IsCurrentFileIndividuallyPaused(session))
            {
                session.State = TransferSessionState.Running;
            }
            Publish(session);
        }

        _pauseGate.Resume();
    }

    public void Cancel(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) || IsTerminal(session.State))
        {
            return;
        }

        session.Cancellation.Cancel();
        if (session.State is TransferSessionState.Queued or TransferSessionState.Preparing)
        {
            session.State = TransferSessionState.Canceled;
            Publish(session);
        }
    }

    public int ClearCompleted()
    {
        var completed = _sessions.Values
            .Where(session => session.State == TransferSessionState.Completed)
            .Select(session => session.Id)
            .ToArray();
        foreach (var sessionId in completed)
        {
            RemoveSessionCore(sessionId);
        }
        return completed.Length;
    }

    public bool Remove(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) ||
            session.State is not (TransferSessionState.Paused or TransferSessionState.Canceled or TransferSessionState.Failed))
        {
            return false;
        }

        if (session.State == TransferSessionState.Paused)
        {
            session.RemoveWhenTerminal = true;
            session.Cancellation.Cancel();
            session.FileStateSignal.Release();
            foreach (var file in session.Files)
            {
                file.PauseGate.Resume();
            }
            return true;
        }

        return RemoveSessionCore(sessionId);
    }

    public bool PauseFile(Guid sessionId, string sourcePath) =>
        ChangeFileState(sessionId, sourcePath, pause: true);

    public bool ResumeFile(Guid sessionId, string sourcePath) =>
        ChangeFileState(sessionId, sourcePath, pause: false);

    public bool CancelFile(Guid sessionId, string sourcePath)
    {
        if (!TryGetControllableFile(sessionId, sourcePath, out var session, out var file))
        {
            return false;
        }

        lock (file.Sync)
        {
            if (IsTerminal(file.State))
            {
                return false;
            }

            var wasRunning = (file.State is TransferFileState.Running or TransferFileState.Paused) &&
                string.Equals(session.CurrentFile, file.Source, StringComparison.OrdinalIgnoreCase);
            file.State = TransferFileState.Canceled;
            file.PauseGate.Resume();
            if (wasRunning)
            {
                file.Cancellation.Cancel();
            }
            else
            {
                ExcludeFileBytes(session, file);
            }
        }

        session.FileStateSignal.Release();
        Interlocked.Increment(ref session.FileStateVersion);
        Publish(session);
        return true;
    }

    public IReadOnlyList<TransferSessionSnapshot> GetSessions() =>
        _sessions.Values
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.Snapshot())
            .ToArray();

    public IReadOnlyList<TransferFileSnapshot> GetSessionFiles(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return [];
        }

        // Podczas budowania planu lista Files jest jeszcze modyfikowana w tle.
        // Do chwili publikacji stanu Queued pokazujemy więc bezpiecznie źródła główne.
        if (!session.IsPlanComplete)
        {
            var sourceState = session.State switch
            {
                TransferSessionState.Canceled => TransferFileState.Canceled,
                TransferSessionState.Failed => TransferFileState.Failed,
                _ => TransferFileState.Queued
            };
            return session.Sources
                .Select(path => new TransferFileSnapshot(path, sourceState, CanControl: false))
                .ToArray();
        }

        return session.Files
            .Select(file =>
            {
                lock (file.Sync)
                {
                    return new TransferFileSnapshot(file.Source, file.State);
                }
            })
            .ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _pauseGate.Resume();
        _queueSignal.Release();

        try
        {
            await _worker;
        }
        catch (OperationCanceledException)
        {
        }

        foreach (var session in _sessions.Values.Concat(_retiredSessions))
        {
            DisposeSession(session);
        }

        _queueSignal.Dispose();
        _shutdown.Dispose();
    }

    private bool ChangeFileState(Guid sessionId, string sourcePath, bool pause)
    {
        if (!TryGetControllableFile(sessionId, sourcePath, out var session, out var file))
        {
            return false;
        }

        lock (file.Sync)
        {
            var isCurrent = string.Equals(session.CurrentFile, file.Source, StringComparison.OrdinalIgnoreCase);
            if (pause)
            {
                if (file.State is not (TransferFileState.Queued or TransferFileState.Running))
                {
                    return false;
                }
                file.State = TransferFileState.Paused;
                file.PauseGate.Pause();
                if (isCurrent)
                {
                    session.State = TransferSessionState.Paused;
                }
            }
            else
            {
                if (file.State != TransferFileState.Paused)
                {
                    return false;
                }
                file.State = isCurrent
                    ? TransferFileState.Running
                    : TransferFileState.Queued;
                file.PauseGate.Resume();
                if (isCurrent)
                {
                    session.State = IsPaused ? TransferSessionState.Paused : TransferSessionState.Running;
                }
            }
        }

        session.FileStateSignal.Release();
        Interlocked.Increment(ref session.FileStateVersion);
        Publish(session);
        return true;
    }

    private static bool IsCurrentFileIndividuallyPaused(TransferSession session)
    {
        var current = session.CurrentFile;
        if (current is null)
        {
            return false;
        }
        var file = session.Files.FirstOrDefault(item =>
            string.Equals(item.Source, current, StringComparison.OrdinalIgnoreCase));
        return file is not null && GetFileState(file) == TransferFileState.Paused;
    }

    private bool TryGetControllableFile(
        Guid sessionId,
        string sourcePath,
        out TransferSession session,
        out PlannedFile file)
    {
        file = null!;
        if (!_sessions.TryGetValue(sessionId, out session!) || !session.IsPlanComplete || IsTerminal(session.State))
        {
            return false;
        }

        file = session.Files.FirstOrDefault(item =>
            string.Equals(item.Source, sourcePath, StringComparison.OrdinalIgnoreCase))!;
        return file is not null;
    }

    private static void ExcludeFileBytes(TransferSession session, PlannedFile file)
    {
        lock (session.Sync)
        {
            if (file.BytesExcluded)
            {
                return;
            }
            session.TotalBytes = Math.Max(0, session.TotalBytes - file.Length);
            session.TransferredBytes = Math.Max(0, session.TransferredBytes - file.TransferredBytes);
            file.TransferredBytes = 0;
            file.BytesExcluded = true;
        }
    }

    private static void MarkNonTerminalFiles(TransferSession session, TransferFileState state)
    {
        var changed = false;
        foreach (var file in session.Files)
        {
            lock (file.Sync)
            {
                if (IsTerminal(file.State))
                {
                    continue;
                }
                file.State = state;
                changed = true;
                file.PauseGate.Resume();
                if (state == TransferFileState.Canceled)
                {
                    file.Cancellation.Cancel();
                }
            }
        }
        if (changed)
        {
            Interlocked.Increment(ref session.FileStateVersion);
        }
    }

    private async Task ProcessQueueAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            await _queueSignal.WaitAsync(_shutdown.Token);

            while (_pending.TryDequeue(out var session))
            {
                if (session.State == TransferSessionState.Canceled)
                {
                    continue;
                }

                await ProcessSessionAsync(session);
            }
        }
    }

    private async Task ProcessSessionAsync(TransferSession session)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _shutdown.Token,
            session.Cancellation.Token);
        var cancellationToken = linkedCancellation.Token;

        try
        {
            session.State = IsPaused ? TransferSessionState.Paused : TransferSessionState.Running;
            Publish(session);

            foreach (var directory in session.Directories.OrderBy(item => item.Target.Length))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _pauseGate.WaitAsync(cancellationToken);
                Directory.CreateDirectory(directory.Target);
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _pauseGate.WaitAsync(cancellationToken);

                var file = session.Files.FirstOrDefault(item => GetFileState(item) == TransferFileState.Queued);
                if (file is null)
                {
                    if (session.Files.All(item => IsTerminal(GetFileState(item))))
                    {
                        break;
                    }

                    await session.FileStateSignal.WaitAsync(cancellationToken);
                    continue;
                }

                await ProcessFileAsync(session, file, cancellationToken);
            }

            if (session.Operation == TransferOperation.Move)
            {
                DeleteEmptySourceDirectories(session);
            }

            session.State = session.Files.Count > 0 &&
                            session.Files.All(file => GetFileState(file) == TransferFileState.Canceled)
                ? TransferSessionState.Canceled
                : TransferSessionState.Completed;
            if (session.State == TransferSessionState.Completed)
            {
                lock (session.Sync)
                {
                    session.TransferredBytes = session.TotalBytes;
                }
            }
            Publish(session);
            RemoveIfRequested(session);
        }
        catch (OperationCanceledException)
        {
            MarkNonTerminalFiles(session, TransferFileState.Canceled);
            session.State = TransferSessionState.Canceled;
            session.CurrentFile = null;
            Publish(session);
            RemoveIfRequested(session);
        }
        catch (Exception exception)
        {
            MarkNonTerminalFiles(session, TransferFileState.Failed);
            session.State = TransferSessionState.Failed;
            session.Error = exception.Message;
            session.CurrentFile = null;
            Publish(session);
            RemoveIfRequested(session);
        }
    }

    private async Task ProcessFileAsync(
        TransferSession session,
        PlannedFile file,
        CancellationToken cancellationToken)
    {
        lock (file.Sync)
        {
            if (file.State != TransferFileState.Queued)
            {
                return;
            }
            file.State = TransferFileState.Running;
        }
        Interlocked.Increment(ref session.FileStateVersion);

        lock (session.Sync)
        {
            session.CurrentFile = file.Source;
        }
        Publish(session);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            file.Cancellation.Token);

        try
        {
            await file.PauseGate.WaitAsync(linkedCancellation.Token);
            var outcome = await CopyOneFileAsync(session, file, linkedCancellation.Token);
            if (outcome == FileCopyOutcome.Skipped)
            {
                lock (file.Sync)
                {
                    file.State = TransferFileState.Skipped;
                }
                Interlocked.Increment(ref session.FileStateVersion);
                ExcludeFileBytes(session, file);
                return;
            }

            if (session.Operation == TransferOperation.Move)
            {
                File.Delete(file.Source);
            }

            lock (file.Sync)
            {
                file.State = TransferFileState.Completed;
            }
            Interlocked.Increment(ref session.FileStateVersion);
            lock (session.Sync)
            {
                session.CompletedFiles++;
            }
        }
        catch (OperationCanceledException) when (file.Cancellation.IsCancellationRequested &&
                                                  !cancellationToken.IsCancellationRequested)
        {
            lock (file.Sync)
            {
                file.State = TransferFileState.Canceled;
            }
            Interlocked.Increment(ref session.FileStateVersion);
            ExcludeFileBytes(session, file);
        }
        finally
        {
            lock (session.Sync)
            {
                session.CurrentFile = null;
            }
            if (session.State == TransferSessionState.Paused && !IsPaused)
            {
                session.State = TransferSessionState.Running;
            }
            Publish(session);
        }
    }

    private async Task<FileCopyOutcome> CopyOneFileAsync(
        TransferSession session,
        PlannedFile file,
        CancellationToken cancellationToken)
    {
        var targetDirectory = Path.GetDirectoryName(file.Target)
            ?? throw new InvalidOperationException("Nie można ustalić katalogu docelowego.");
        Directory.CreateDirectory(targetDirectory);

        var finalTarget = file.Target;
        if (Directory.Exists(finalTarget))
        {
            throw new IOException($"Plik docelowy koliduje z istniejącym katalogiem: {finalTarget}");
        }

        var replaceExisting = false;
        if (File.Exists(finalTarget))
        {
            if (string.Equals(Path.GetFullPath(file.Source), Path.GetFullPath(finalTarget),
                    StringComparison.OrdinalIgnoreCase))
            {
                return FileCopyOutcome.Skipped;
            }

            var resolution = session.ConflictPolicy ?? await ResolveConflictAsync(session, file, finalTarget, cancellationToken);
            if (resolution is ConflictResolution.ReplaceAll or ConflictResolution.SkipAll)
            {
                session.ConflictPolicy = resolution;
            }

            if (resolution == ConflictResolution.CancelSession)
            {
                session.Cancellation.Cancel();
                throw new OperationCanceledException(cancellationToken);
            }
            if (resolution is ConflictResolution.Skip or ConflictResolution.SkipAll)
            {
                return FileCopyOutcome.Skipped;
            }
            replaceExisting = true;
        }

        var temporaryTarget = finalTarget + $".queue-part-{session.Id:N}";
        var verifyIntegrity = session.VerifyIntegrity ||
                              (session.Operation == TransferOperation.Move && !IsSameVolume(file.Source, finalTarget));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            byte[]? expectedHash = null;
            {
                await using var source = new FileStream(
                    file.Source,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var target = new FileStream(
                    temporaryTarget,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                using var sourceHash = verifyIntegrity
                    ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
                    : null;
                var buffer = new byte[BufferSize];
                var lastPublished = TimeSpan.Zero;
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await _pauseGate.WaitAsync(cancellationToken);
                    await file.PauseGate.WaitAsync(cancellationToken);
                    await target.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    sourceHash?.AppendData(buffer, 0, bytesRead);
                    lock (session.Sync)
                    {
                        session.TransferredBytes += bytesRead;
                        file.TransferredBytes += bytesRead;
                    }

                    if (stopwatch.Elapsed - lastPublished >= TimeSpan.FromMilliseconds(120))
                    {
                        lastPublished = stopwatch.Elapsed;
                        Publish(session);
                    }
                }

                await target.FlushAsync(cancellationToken);
                target.Flush(flushToDisk: true);
                expectedHash = sourceHash?.GetHashAndReset();
            }

            if (verifyIntegrity)
            {
                session.IsVerifying = true;
                Publish(session);
                var actualHash = await ComputeHashAsync(temporaryTarget, file, cancellationToken);
                session.IsVerifying = false;
                if (!CryptographicOperations.FixedTimeEquals(expectedHash!, actualHash))
                {
                    throw new IOException($"Weryfikacja integralności nie powiodła się: {file.Source}");
                }
            }

            File.Move(temporaryTarget, finalTarget, replaceExisting);
            File.SetLastWriteTimeUtc(finalTarget, File.GetLastWriteTimeUtc(file.Source));
            return FileCopyOutcome.Completed;
        }
        finally
        {
            session.IsVerifying = false;
            if (File.Exists(temporaryTarget))
            {
                File.Delete(temporaryTarget);
            }
        }
    }

    private static void BuildPlan(TransferSession session, CancellationToken cancellationToken)
    {
        var destination = Path.GetFullPath(session.Destination);
        if (File.Exists(destination))
        {
            throw new IOException("Miejsce docelowe wskazuje plik, a nie katalog.");
        }

        var reservedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawSource in session.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.GetFullPath(rawSource);

            if (File.Exists(source))
            {
                var target = ReservePath(
                    Path.Combine(destination, Path.GetFileName(source)),
                    reservedRoots,
                    isDirectory: false);
                var length = new FileInfo(source).Length;
                session.Files.Add(new PlannedFile(source, target, length));
                session.TotalBytes += length;
                continue;
            }

            if (!Directory.Exists(source))
            {
                throw new FileNotFoundException($"Nie znaleziono źródła: {source}");
            }

            var directoryInfo = new DirectoryInfo(source);
            if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException($"Dowiązania katalogów nie są obsługiwane: {source}");
            }

            var rootTarget = ReservePath(
                Path.Combine(destination, directoryInfo.Name),
                reservedRoots,
                isDirectory: true);
            session.SourceDirectoryRoots.Add(source);
            session.Directories.Add(new PlannedDirectory(source, rootTarget));
            AddDirectoryTree(session, source, rootTarget, cancellationToken);
        }

        session.TotalFiles = session.Files.Count;
    }

    private static void AddDirectoryTree(
        TransferSession session,
        string sourceRoot,
        string targetRoot,
        CancellationToken cancellationToken)
    {
        var directories = new Stack<string>();
        directories.Push(sourceRoot);

        while (directories.TryPop(out var sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeDirectory = Path.GetRelativePath(sourceRoot, sourceDirectory);
            var targetDirectory = relativeDirectory == "."
                ? targetRoot
                : Path.Combine(targetRoot, relativeDirectory);

            if (!string.Equals(sourceDirectory, sourceRoot, StringComparison.OrdinalIgnoreCase))
            {
                session.Directories.Add(new PlannedDirectory(sourceDirectory, targetDirectory));
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(sourceDirectory))
            {
                var info = new DirectoryInfo(childDirectory);
                if (!info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    directories.Push(childDirectory);
                }
            }

            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory))
            {
                var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
                var length = new FileInfo(sourceFile).Length;
                session.Files.Add(new PlannedFile(sourceFile, targetFile, length));
                session.TotalBytes += length;
            }
        }
    }

    private static string ReservePath(
        string desiredPath,
        HashSet<string> reservedPaths,
        bool isDirectory)
    {
        var candidate = desiredPath;
        var suffix = 2;

        while (reservedPaths.Contains(candidate))
        {
            var directory = Path.GetDirectoryName(desiredPath)!;
            var name = isDirectory
                ? Path.GetFileName(desiredPath)
                : Path.GetFileNameWithoutExtension(desiredPath);
            var extension = isDirectory ? string.Empty : Path.GetExtension(desiredPath);
            candidate = Path.Combine(directory, $"{name} ({suffix++}){extension}");
        }

        reservedPaths.Add(candidate);
        return candidate;
    }

    private static void DeleteEmptySourceDirectories(TransferSession session)
    {
        foreach (var directory in session.Directories
                     .Select(item => item.Source)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(item => item.Length))
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }

    private static bool IsTerminal(TransferSessionState state) =>
        state is TransferSessionState.Completed or TransferSessionState.Failed or TransferSessionState.Canceled;

    private static bool IsTerminal(TransferFileState state) =>
        state is TransferFileState.Completed or TransferFileState.Skipped or
            TransferFileState.Canceled or TransferFileState.Failed;

    private static TransferFileState GetFileState(PlannedFile file)
    {
        lock (file.Sync)
        {
            return file.State;
        }
    }

    private async Task<ConflictResolution> ResolveConflictAsync(
        TransferSession session,
        PlannedFile file,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var source = new FileInfo(file.Source);
        var destination = new FileInfo(destinationPath);
        var conflict = new FileConflictSnapshot(
            session.Id,
            session.Operation,
            file.Source,
            destinationPath,
            source.Length,
            destination.Length,
            source.LastWriteTimeUtc,
            destination.LastWriteTimeUtc);
        return await _conflictResolver(conflict, cancellationToken).ConfigureAwait(false);
    }

    private void Publish(TransferSession session) =>
        SessionChanged?.Invoke(this, session.Snapshot());

    private async Task<byte[]> ComputeHashAsync(
        string path,
        PlannedFile file,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[BufferSize];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await _pauseGate.WaitAsync(cancellationToken);
            await file.PauseGate.WaitAsync(cancellationToken);
            hash.AppendData(buffer, 0, bytesRead);
        }
        return hash.GetHashAndReset();
    }

    private static bool IsSameVolume(string source, string destination) =>
        string.Equals(
            Path.GetPathRoot(Path.GetFullPath(source)),
            Path.GetPathRoot(Path.GetFullPath(destination)),
            StringComparison.OrdinalIgnoreCase);

    private void RemoveIfRequested(TransferSession session)
    {
        if (session.RemoveWhenTerminal && IsTerminal(session.State))
        {
            RemoveSessionCore(session.Id);
        }
    }

    private bool RemoveSessionCore(Guid sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var removed))
        {
            return false;
        }
        _retiredSessions.Add(removed);
        SessionRemoved?.Invoke(this, sessionId);
        return true;
    }

    private static void DisposeSession(TransferSession session)
    {
        session.Cancellation.Dispose();
        session.FileStateSignal.Dispose();
        foreach (var file in session.Files)
        {
            file.Cancellation.Dispose();
        }
    }

    private sealed class TransferSession
    {
        public TransferSession(TransferRequest request)
        {
            Id = Guid.NewGuid();
            Operation = request.Operation;
            Destination = Path.GetFullPath(request.Destination);
            Sources = request.Sources.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            VerifyIntegrity = request.VerifyIntegrity;
        }

        public Guid Id { get; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public TransferOperation Operation { get; }
        public string Destination { get; }
        public IReadOnlyList<string> Sources { get; }
        public bool VerifyIntegrity { get; }
        public TransferSessionState State { get; set; } = TransferSessionState.Preparing;
        public List<PlannedFile> Files { get; } = [];
        public volatile bool IsPlanComplete;
        public List<PlannedDirectory> Directories { get; } = [];
        public List<string> SourceDirectoryRoots { get; } = [];
        public CancellationTokenSource Cancellation { get; } = new();
        public SemaphoreSlim FileStateSignal { get; } = new(0);
        public object Sync { get; } = new();
        public int CompletedFiles { get; set; }
        public long FileStateVersion;
        public int TotalFiles { get; set; }
        public long TransferredBytes { get; set; }
        public long TotalBytes { get; set; }
        public string? CurrentFile { get; set; }
        public string? Error { get; set; }
        public ConflictResolution? ConflictPolicy { get; set; }
        public bool IsVerifying { get; set; }
        public bool RemoveWhenTerminal { get; set; }

        public TransferSessionSnapshot Snapshot()
        {
            var fileStates = IsPlanComplete
                ? Files.Select(file => (file.Source, State: GetFileState(file))).ToArray()
                : [];
            lock (Sync)
            {
                return new TransferSessionSnapshot(
                    Id,
                    Operation,
                    Destination,
                    Sources,
                    State,
                    CompletedFiles,
                    fileStates.Count(item => IsTerminal(item.State)),
                    Interlocked.Read(ref FileStateVersion),
                    TotalFiles,
                    TransferredBytes,
                    TotalBytes,
                    CurrentFile,
                    fileStates
                        .Where(item => item.State is TransferFileState.Queued or TransferFileState.Running or TransferFileState.Paused)
                        .Take(5)
                        .Select(item => item.Source)
                        .ToArray(),
                    Error,
                    IsVerifying,
                    VerifyIntegrity || Operation == TransferOperation.Move);
            }
        }
    }

    private sealed class PlannedFile(string source, string target, long length)
    {
        public string Source { get; } = source;
        public string Target { get; } = target;
        public long Length { get; } = length;
        public object Sync { get; } = new();
        public AsyncPauseGate PauseGate { get; } = new();
        public CancellationTokenSource Cancellation { get; } = new();
        public TransferFileState State { get; set; } = TransferFileState.Queued;
        public long TransferredBytes { get; set; }
        public bool BytesExcluded { get; set; }
    }
    private sealed record PlannedDirectory(string Source, string Target);

    private enum FileCopyOutcome
    {
        Completed,
        Skipped
    }

    private sealed class AsyncPauseGate
    {
        private volatile TaskCompletionSource _resumeSource = CompletedSource();

        public Task WaitAsync(CancellationToken cancellationToken) =>
            _resumeSource.Task.WaitAsync(cancellationToken);

        public void Pause()
        {
            if (_resumeSource.Task.IsCompleted)
            {
                _resumeSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public void Resume() => _resumeSource.TrySetResult();

        private static TaskCompletionSource CompletedSource()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            source.SetResult();
            return source;
        }
    }
}
