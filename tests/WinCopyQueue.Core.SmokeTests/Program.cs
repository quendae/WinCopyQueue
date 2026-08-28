using System.Collections.Concurrent;
using WinCopyQueue.Core;

var testRoot = Path.Combine(Path.GetTempPath(), $"WinCopyQueueSmokeTests-{Guid.NewGuid():N}");
Directory.CreateDirectory(testRoot);

try
{
    var copySource = Path.Combine(testRoot, "copy-source");
    var moveSource = Path.Combine(testRoot, "move-source");
    var destination = Path.Combine(testRoot, "destination");
    Directory.CreateDirectory(copySource);
    Directory.CreateDirectory(moveSource);
    Directory.CreateDirectory(destination);

    var copyBytes = new byte[3 * 1024 * 1024];
    Random.Shared.NextBytes(copyBytes);
    await File.WriteAllBytesAsync(Path.Combine(copySource, "large.bin"), copyBytes);
    await File.WriteAllTextAsync(Path.Combine(copySource, "note.txt"), "kolejka-kopiowania");
    await File.WriteAllTextAsync(Path.Combine(moveSource, "move-me.txt"), "kolejka-przenoszenia");

    await using var queue = new TransferQueueService();
    var events = new ConcurrentQueue<(Guid Id, TransferSessionState State)>();
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var terminalSessions = new ConcurrentDictionary<Guid, TransferSessionState>();
    var verificationObserved = false;

    queue.SessionChanged += (_, snapshot) =>
    {
        events.Enqueue((snapshot.Id, snapshot.State));
        if (snapshot.IsVerifying)
        {
            verificationObserved = true;
        }
        if (snapshot.State is TransferSessionState.Completed or TransferSessionState.Failed or TransferSessionState.Canceled)
        {
            terminalSessions[snapshot.Id] = snapshot.State;
            if (terminalSessions.Count == 2)
            {
                completed.TrySetResult();
            }
        }
    };

    var first = await queue.EnqueueAsync(new TransferRequest(
        TransferOperation.Copy, destination, [copySource], VerifyIntegrity: true));
    var second = await queue.EnqueueAsync(new TransferRequest(
        TransferOperation.Move, destination, [moveSource]));

    await completed.Task.WaitAsync(TimeSpan.FromSeconds(20));

    Assert(terminalSessions[first.Id] == TransferSessionState.Completed, "Sesja kopiowania nie zakończyła się poprawnie.");
    Assert(terminalSessions[second.Id] == TransferSessionState.Completed, "Sesja przenoszenia nie zakończyła się poprawnie.");

    var eventList = events.ToArray();
    var firstCompletedIndex = Array.FindIndex(eventList, item => item.Id == first.Id && item.State == TransferSessionState.Completed);
    var secondRunningIndex = Array.FindIndex(eventList, item => item.Id == second.Id && item.State == TransferSessionState.Running);
    Assert(firstCompletedIndex >= 0 && secondRunningIndex > firstCompletedIndex,
        "Druga sesja ruszyła przed zakończeniem pierwszej.");

    Assert(File.ReadAllBytes(Path.Combine(destination, "copy-source", "large.bin")).SequenceEqual(copyBytes),
        "Skopiowany plik binarny ma inną treść.");
    Assert(await File.ReadAllTextAsync(Path.Combine(destination, "copy-source", "note.txt")) == "kolejka-kopiowania",
        "Skopiowany plik tekstowy ma inną treść.");
    Assert(await File.ReadAllTextAsync(Path.Combine(destination, "move-source", "move-me.txt")) == "kolejka-przenoszenia",
        "Przeniesiony plik ma inną treść.");
    Assert(!Directory.Exists(moveSource), "Źródłowy folder nie został usunięty po przeniesieniu.");
    Assert(verificationObserved, "Włączona weryfikacja SHA-256 nie została wykonana.");

    var completedFiles = queue.GetSessionFiles(first.Id);
    Assert(completedFiles.Count == 2 && completedFiles.All(file => file.State == TransferFileState.Completed),
        "Pełna lista plików nie odzwierciedla zakończonej sesji.");
    Assert(queue.ClearCompleted() == 2 && queue.GetSessions().All(item => item.Id != first.Id && item.Id != second.Id),
        "Czyszczenie poprawnie zakończonych sesji nie usunęło historii.");

    var cancelSource = Path.Combine(testRoot, "cancel-me.bin");
    await File.WriteAllBytesAsync(cancelSource, new byte[1024 * 1024]);
    queue.Pause();
    Assert(queue.IsPaused, "Kolejka nie została wstrzymana.");
    var canceled = await queue.EnqueueAsync(new TransferRequest(
        TransferOperation.Copy, destination, [cancelSource]));
    var queuedFiles = queue.GetSessionFiles(canceled.Id);
    Assert(queuedFiles.Count == 1 && queuedFiles[0].Path == cancelSource,
        "Rozwijana lista nie zwróciła zakolejkowanego pliku.");
    queue.Cancel(canceled.Id);

    var cancelDeadline = DateTime.UtcNow.AddSeconds(5);
    while (DateTime.UtcNow < cancelDeadline &&
           queue.GetSessions().Single(item => item.Id == canceled.Id).State != TransferSessionState.Canceled)
    {
        await Task.Delay(20);
    }

    Assert(queue.GetSessions().Single(item => item.Id == canceled.Id).State == TransferSessionState.Canceled,
        "Wstrzymana sesja nie została anulowana.");
    Assert(queue.GetSessionFiles(canceled.Id).All(file => file.State == TransferFileState.Canceled),
        "Lista plików nie pokazuje stanu anulowania.");
    Assert(queue.Remove(canceled.Id) && queue.GetSessions().All(item => item.Id != canceled.Id),
        "Nie udało się usunąć anulowanej sesji.");
    queue.Resume();
    Assert(!queue.IsPaused, "Kolejka nie została wznowiona.");

    var removePausedSource = Path.Combine(testRoot, "remove-paused.bin");
    await File.WriteAllBytesAsync(removePausedSource, new byte[2 * 1024 * 1024]);
    queue.Pause();
    var removePaused = await queue.EnqueueAsync(new TransferRequest(
        TransferOperation.Copy, destination, [removePausedSource]));
    var pausedDeadline = DateTime.UtcNow.AddSeconds(5);
    while (DateTime.UtcNow < pausedDeadline &&
           queue.GetSessions().Single(item => item.Id == removePaused.Id).State != TransferSessionState.Paused)
    {
        await Task.Delay(20);
    }
    Assert(queue.Remove(removePaused.Id), "Nie udało się zlecić usunięcia wstrzymanej sesji.");
    while (DateTime.UtcNow < pausedDeadline && queue.GetSessions().Any(item => item.Id == removePaused.Id))
    {
        await Task.Delay(20);
    }
    Assert(queue.GetSessions().All(item => item.Id != removePaused.Id),
        "Wstrzymana sesja wróciła na listę po usunięciu.");
    queue.Resume();

    var controlledSource = Path.Combine(testRoot, "controlled");
    Directory.CreateDirectory(controlledSource);
    var firstControlled = Path.Combine(controlledSource, "first.txt");
    var pausedControlled = Path.Combine(controlledSource, "paused.txt");
    var canceledControlled = Path.Combine(controlledSource, "canceled.txt");
    await File.WriteAllTextAsync(firstControlled, "pierwszy");
    await File.WriteAllTextAsync(pausedControlled, "wstrzymany");
    await File.WriteAllTextAsync(canceledControlled, "anulowany");

    queue.Pause();
    var controlled = await queue.EnqueueAsync(new TransferRequest(
        TransferOperation.Copy, destination, [firstControlled, pausedControlled, canceledControlled]));
    Assert(queue.PauseFile(controlled.Id, pausedControlled), "Nie udało się wstrzymać pojedynczego pliku.");
    Assert(queue.CancelFile(controlled.Id, canceledControlled), "Nie udało się anulować pojedynczego pliku.");
    queue.Resume();

    var controlDeadline = DateTime.UtcNow.AddSeconds(5);
    while (DateTime.UtcNow < controlDeadline)
    {
        var states = queue.GetSessionFiles(controlled.Id).ToDictionary(file => file.Path, file => file.State);
        if (states[firstControlled] == TransferFileState.Completed &&
            states[pausedControlled] == TransferFileState.Paused)
        {
            break;
        }
        await Task.Delay(20);
    }
    Assert(queue.ResumeFile(controlled.Id, pausedControlled), "Nie udało się wznowić pojedynczego pliku.");
    await WaitForTerminalAsync(queue, controlled.Id, TimeSpan.FromSeconds(5));
    var controlledFiles = queue.GetSessionFiles(controlled.Id).ToDictionary(file => file.Path, file => file.State);
    Assert(controlledFiles[firstControlled] == TransferFileState.Completed &&
           controlledFiles[pausedControlled] == TransferFileState.Completed &&
           controlledFiles[canceledControlled] == TransferFileState.Canceled,
        "Sterowanie stanem pojedynczych plików nie zachowało oczekiwanych rezultatów.");

    var conflictSource = Path.Combine(testRoot, "conflict-source");
    var conflictDestination = Path.Combine(testRoot, "conflict-destination");
    Directory.CreateDirectory(conflictSource);
    Directory.CreateDirectory(conflictDestination);
    var conflictSourceFile = Path.Combine(conflictSource, "same-name.txt");
    var conflictDestinationFile = Path.Combine(conflictDestination, "same-name.txt");
    await File.WriteAllTextAsync(conflictSourceFile, "nowa-zawartość");
    await File.WriteAllTextAsync(conflictDestinationFile, "stara-zawartość");
    var oldDestinationLength = new FileInfo(conflictDestinationFile).Length;
    FileConflictSnapshot? observedConflict = null;
    await using (var conflictQueue = new TransferQueueService((conflict, _) =>
    {
        observedConflict = conflict;
        return Task.FromResult(ConflictResolution.Replace);
    }))
    {
        var conflictSession = await conflictQueue.EnqueueAsync(new TransferRequest(
            TransferOperation.Copy, conflictDestination, [conflictSourceFile]));
        await WaitForTerminalAsync(conflictQueue, conflictSession.Id, TimeSpan.FromSeconds(5));
    }
    Assert(observedConflict is not null &&
           observedConflict.SourceSize == new FileInfo(conflictSourceFile).Length &&
           observedConflict.DestinationSize == oldDestinationLength,
        "Resolver konfliktu nie otrzymał porównania rozmiarów plików.");
    Assert(await File.ReadAllTextAsync(conflictDestinationFile) == "nowa-zawartość",
        "Decyzja zastąpienia nie nadpisała istniejącego pliku.");

    Console.WriteLine("PASS: kolejność, konflikty, lista plików oraz sterowanie sesją i pojedynczymi plikami działają.");
}
finally
{
    var fullTestRoot = Path.GetFullPath(testRoot);
    var fullTempRoot = Path.GetFullPath(Path.GetTempPath());
    if (fullTestRoot.StartsWith(fullTempRoot, StringComparison.OrdinalIgnoreCase) &&
        Path.GetFileName(fullTestRoot).StartsWith("WinCopyQueueSmokeTests-", StringComparison.Ordinal))
    {
        Directory.Delete(fullTestRoot, recursive: true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static async Task WaitForTerminalAsync(TransferQueueService queue, Guid sessionId, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        var state = queue.GetSessions().Single(item => item.Id == sessionId).State;
        if (state is TransferSessionState.Completed or TransferSessionState.Failed or TransferSessionState.Canceled)
        {
            return;
        }
        await Task.Delay(20);
    }
    throw new TimeoutException($"Sesja {sessionId} nie zakończyła się w wymaganym czasie.");
}
