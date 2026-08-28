# WinCopyQueue — LLM Project Context

## Purpose

WinCopyQueue is a Windows tray utility integrated with Windows Explorer. It turns copy and move operations into a persistent sequential queue: sessions run in submission order, one session at a time and one file at a time. Users can enqueue another destination while a transfer is already running.

The application deliberately has no conventional main window. A compact queue panel appears after a transfer is added, can be hidden without stopping work, and can be reopened from the notification-area icon.

## Product identity

- Product and executable name: `WinCopyQueue` / `WinCopyQueue.exe`
- Current intentionally fixed version: `1.0.0`
- Platform: Windows 10 1809 or newer, including Windows 11
- Distribution: self-contained .NET 10 x64 single-file executable and per-user Inno Setup installer
- UI: WPF plus Windows Forms `NotifyIcon`
- License: no license file is currently present; do not assume an open-source license

## Complete feature set

### Explorer integration

- Registers a per-user `Paste with WinCopyQueue` command for directory backgrounds and directory objects.
- Intercepts `Ctrl+V` only when Windows Explorer is foreground and the clipboard contains `CF_HDROP` file-system entries.
- Detects copy versus cut/move semantics from the Explorer clipboard.
- Accepts files and complete directory trees.
- Requires no administrator privileges.
- Provides a tray command to repair registry integration after relocation or corruption.
- On Windows 11 the static command may appear under `Show more options`.

### Sequential queue

- Accepts multiple independent copy or move sessions.
- Preserves session submission order.
- Executes exactly one session and one file at a time.
- Allows new Explorer operations to be queued during an active transfer.
- Builds directory plans in the background.
- Does not follow directory reparse points while walking trees.
- Creates target directories parent-first.
- Deletes empty source directories after a successful move.
- Uses one primary process; later invocations forward length-prefixed commands over a named pipe.

### Controls and history

- Pause/resume the entire queue.
- Cancel a session while retaining already completed destination files.
- Expand a session to inspect every file and state.
- Pause/resume an individual queued or active file.
- Cancel an individual file without canceling its session.
- Clear every successfully completed session with one button.
- Remove paused, canceled, or failed sessions using a separate confirmed action.
- Removing a paused session first cancels all remaining work.
- Canceled, failed, and paused sessions are never automatically removed.

### Progress UI

- Opens automatically when a session is added.
- Shows current file, upcoming files, target, file counts, byte counts, percentage, and smoothed transfer speed.
- Shows session and per-file states using distinct colors.
- Shows an explicit SHA-256 verification phase.
- Retains session history until explicit cleanup.
- Auto-hides a few seconds after no active work remains unless explicitly opened.
- Hiding/minimizing the panel never stops transfers.
- Displays the supplied WinCopyQueue logo and version in the header.

### Conflict handling

- Detects an existing target file before replacement.
- Shows a modal comparison with both paths, sizes, and local modification dates.
- Offers Replace, Skip, and Cancel session.
- Replace or Skip can be applied to every later conflict in that session.
- A directory occupying a file target is an error.
- A source resolving to the same path as its target is skipped.

### Integrity and safe file handling

- Writes to a unique temporary `*.queue-part-*` path.
- Flushes the temporary target to disk before finalization.
- Publishes the final name only after a successful copy.
- Preserves source last-write timestamps.
- Offers persistent optional SHA-256 verification for copies.
- Hashes source bytes incrementally while copying, then re-reads and hashes the temporary target.
- Compares hashes before renaming the temporary target.
- Automatically verifies moves across different volumes even when optional verification is off.
- Deletes a moved source only after copy, required verification, and finalization succeed.
- On checksum mismatch, fails the session, removes the temporary target, and keeps the source.
- Verification detects transfer mismatch; it cannot guarantee future storage-media health.

### Localization

- On first run selects a supported language from the Windows UI locale.
- Saves explicit language choice and verification preference in `%LOCALAPPDATA%\WinCopyQueue\settings.json`.
- Supports English, Polish, German, French, Spanish, Portuguese, Simplified Chinese, and Japanese.
- Localizes the queue panel, conflict dialog, tray menu, and Explorer paste label.
- Allows language changes at runtime.

### Tray, startup, and shutdown

- Tray balloons report additions, completion, and errors.
- Double-click opens the queue panel.
- Tray menu controls visibility, global pause/resume, autostart, Explorer repair, and exit.
- Optional per-user autostart uses `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Shutdown disables and detaches tray callbacks before WPF windows are disposed.
- Late tray events are guarded so they cannot call `Show()` on an already closed window or produce a .NET JIT crash.

### Installer

- Installs per-user under `%LOCALAPPDATA%\Programs\WinCopyQueue` by default.
- Does not request elevation.
- Can add a desktop shortcut and enable autostart.
- Registers Explorer commands and removes them during uninstall.
- Removes the legacy `WinCopyQueue.App.exe` filename during upgrades.

## CLI

```text
WinCopyQueue.exe --copy <destination> <source...>
WinCopyQueue.exe --move <destination> <source...>
WinCopyQueue.exe --paste <destination>
```

`--paste` obtains source paths and copy/cut semantics from the Windows clipboard. Secondary instances send parsed commands to the primary instance and exit.

## State model

Session states:

```text
Preparing -> Queued -> Running <-> Paused -> Completed
                           |          |
                           +--------> Canceled
                           +--------> Failed
```

File states:

```text
Queued -> Running <-> Paused -> Completed
   |          |
   +----------+----------------------> Canceled
   +---------------------------------> Skipped
   +---------------------------------> Failed
```

Completed, skipped, canceled, and failed are terminal file states. Completed, canceled, and failed are terminal session states.

## Architecture and repository map

```text
src/WinCopyQueue.Core/
  TransferModels.cs          Requests, snapshots, states, conflict models
  TransferQueueService.cs    Planning, sequential worker, controls, copy/move, hashing

src/WinCopyQueue.App/
  App.xaml.cs                Startup, composition, single instance, shutdown
  QueueWindow.xaml(.cs)      Queue panel and row view models
  ConflictWindow.xaml(.cs)   Existing-file comparison dialog
  TrayService.cs             NotifyIcon and tray menu
  ExplorerPasteHook.cs       Explorer-only Ctrl+V and target resolution
  ClipboardTransferReader.cs File clipboard and preferred-operation reader
  Ipc.cs                     CLI parser and named-pipe IPC
  ShellIntegration.cs        Explorer registry integration
  StartupIntegration.cs      Autostart registry integration
  AppSettings.cs             JSON user settings
  Localization.cs            Runtime translations and locale selection
  Assets/                    Logo and application/tray icons

tests/
  WinCopyQueue.Core.SmokeTests/  Real file operations and queue-state tests
  WinCopyQueue.App.SmokeTests/   WPF, localization, conflict, shutdown tests

installer/
  Build-Installer.ps1        Restore, publish, package
  WinCopyQueue.iss           Inno Setup definition
```

## Critical invariants

1. Never execute more than one file or session concurrently.
2. Never delete a move source before target finalization and any required verification succeed.
3. Never overwrite an existing target without an explicit or session-wide conflict decision.
4. Never follow directory reparse points during recursive planning.
5. Never discard paused, canceled, or failed history without user confirmation.
6. Never leave a partial file under the final target name.
7. Keep the source intact when copy, verification, or finalization fails.
8. Keep Explorer interception scoped to Explorer and file-list clipboard contents.
9. Disable tray callbacks before closing the WPF queue window.
10. Never attempt to show a WPF window after it has closed during shutdown.

## Build and tests

```powershell
dotnet restore WinCopyQueue.slnx --configfile NuGet.Config
dotnet build WinCopyQueue.slnx --no-restore -c Release
dotnet run --project tests\WinCopyQueue.Core.SmokeTests\WinCopyQueue.Core.SmokeTests.csproj --no-build -c Release
dotnet run --project tests\WinCopyQueue.App.SmokeTests\WinCopyQueue.App.SmokeTests.csproj --no-build -c Release
.\installer\Build-Installer.ps1
```

The core smoke test uses isolated temporary files and checks byte-correct copying, moves, session ordering, conflicts, SHA-256 execution, pause/resume, cancellation/removal, history clearing, and per-file controls.

The WPF smoke test checks conflict metadata, embedded branding, eight languages, runtime language switching, and the delayed-tray-callback shutdown regression.

## Persistent locations

- Settings: `%LOCALAPPDATA%\WinCopyQueue\settings.json`
- Log: `%LOCALAPPDATA%\WinCopyQueue\WinCopyQueue.log`
- Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\WinCopyQueue`
- Explorer verbs: `HKCU\Software\Classes\Directory\...\WinCopyQueue.Paste`
- Mutex: `Local\WinCopyQueue.1070D10E-85FD-42A6-B82B-0C29FD823683`
- Named pipe: `WinCopyQueue.Commands.v1`

## Distribution

- Repository: https://github.com/quendae/WinCopyQueue
- Release: https://github.com/quendae/WinCopyQueue/releases/tag/v1.0.0
- Standalone asset: `WinCopyQueue.exe`
- Installer asset: `WinCopyQueue-Setup-1.0.0-x64.exe`

## Guidance for future LLM work

- Preserve all invariants before optimizing performance or changing UX.
- Extend smoke tests whenever transfer state, deletion timing, conflict policy, shutdown, or removal changes.
- Keep visible branding exactly `WinCopyQueue`, without an `.App` suffix.
- Keep version `1.0.0` until the owner explicitly authorizes a significant version change.
- Add localization keys for new visible strings, with at least English fallback and Polish translation.
- Do not commit `artifacts/`, `.tools/`, `bin/`, or `obj/`; binaries belong in GitHub Releases.
- Treat the WPF/WinForms shutdown boundary as race-prone.
