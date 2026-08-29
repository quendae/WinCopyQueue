<p align="center">
  <img src="src/WinCopyQueue.App/Assets/logo-full.png" alt="WinCopyQueue" width="420">
</p>

<p align="center">
  <strong>English</strong> · <a href="README.pl.md">Polski</a> · <a href="README.de.md">Deutsch</a> · <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a>
</p>

# WinCopyQueue

WinCopyQueue adds a simple copy and move queue to Windows Explorer. Instead of running several transfers at the same time, it processes them sequentially — one session after another and one file at a time.

The app lives in the system tray and does not keep a permanent main window on screen. The compact queue panel appears only when a transfer is added, can be hidden at any time, and transfers continue in the background.

## Download

Current version: **1.0.0**

- [Download WinCopyQueue 1.0.0 installer](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue-Setup-1.0.0-x64.exe)
- [Download standalone WinCopyQueue.exe](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue.exe)
- [View release v1.0.0](https://github.com/quendae/WinCopyQueue/releases/tag/v1.0.0)

WinCopyQueue supports **Windows 10 1809 or newer**, including Windows 11. The installer is per-user and does not require administrator privileges.

> This repository currently does not include a `LICENSE` file.

## How it works

1. Start `WinCopyQueue.exe`.
2. In Windows Explorer, copy or cut files normally with `Ctrl+C` / `Ctrl+X`.
3. In the destination folder, press `Ctrl+V` or choose **Paste with WinCopyQueue** from the context menu.

If another transfer is already running, the new one is simply added to the end of the queue. This prevents several large operations from competing for the same disk at once.

On Windows 11, the static context-menu entry may appear under **Show more options**.

<p align="center">
  <img src="docs/images/WinCopyQueue_screenshot.png" alt="WinCopyQueue during an active transfer" width="480">
</p>

## Main features

- copy and move individual files or complete folders,
- multiple independent sessions in one sequential queue,
- pause and resume the whole queue or individual files,
- cancel an entire session or a selected file,
- cancel a session without removing files that were already copied successfully,
- conflict handling with path, size, and modification-date comparison,
- **Replace**, **Skip**, and **Cancel session** decisions, with the option to apply a choice to later conflicts,
- compact queue panel with current file, progress, file count, and transfer speed,
- expandable virtualized list of all files and their states,
- history of completed, canceled, and failed sessions,
- system notifications for added, completed, and failed transfers,
- optional startup with Windows,
- eight interface languages: English, Polish, German, French, Spanish, Portuguese, Simplified Chinese, and Japanese.

## Safer copy and move operations

WinCopyQueue does not write an incomplete file directly under its final destination name. Data is first written to a temporary `*.queue-part-*` file and published under the final name only after the transfer completes successfully.

For normal copy operations, optional **SHA-256** verification can be enabled. WinCopyQueue hashes the source while copying and then reads the destination again to compare the result.

When moving files between different volumes, verification is performed automatically before the source is deleted, regardless of the UI setting. If copying, verification, or finalization fails, the source remains intact.

## Queue panel and tray

The queue panel opens automatically when a transfer is added and appears near the bottom-right corner of the screen without stealing focus from Explorer. It can be minimized while transfers continue in the background.

Double-clicking the tray icon or choosing **Show queue** opens the panel again. The tray menu can also pause or resume the whole queue, toggle startup, repair Explorer integration, and exit the application.

## File conflicts

If a file with the same name already exists at the destination, WinCopyQueue shows both files together with their sizes and modification dates. Three actions are available:

- **Replace**,
- **Skip**,
- **Cancel session**.

Replace or Skip can also be applied to all later conflicts in the same session.

## Settings and diagnostics

User settings are stored in:

```text
%LOCALAPPDATA%\WinCopyQueue\settings.json
```

The diagnostic log is stored in:

```text
%LOCALAPPDATA%\WinCopyQueue\WinCopyQueue.log
```

The selected language and SHA-256 verification preference are remembered between runs.

## Command line

WinCopyQueue can also receive transfer requests directly from the command line:

```powershell
WinCopyQueue.exe --copy "D:\Destination" "D:\File.txt" "D:\Folder"
WinCopyQueue.exe --move "D:\Destination" "D:\File.txt"
WinCopyQueue.exe --paste "D:\Destination"
```

Launching the application again does not create another queue. Commands are forwarded to the primary process through a named pipe.

## Building the project

.NET 10 SDK is required.

```powershell
dotnet restore WinCopyQueue.slnx --configfile NuGet.Config
dotnet build WinCopyQueue.slnx --no-restore -c Release
```

Run the application from the repository:

```powershell
dotnet run --project src\WinCopyQueue.App\WinCopyQueue.App.csproj --no-restore
```

### Tests

```powershell
dotnet run --project tests\WinCopyQueue.Core.SmokeTests\WinCopyQueue.Core.SmokeTests.csproj --no-build -c Release
dotnet run --project tests\WinCopyQueue.App.SmokeTests\WinCopyQueue.App.SmokeTests.csproj --no-build -c Release
```

Core smoke tests perform real operations on isolated temporary files and cover session ordering, conflicts, SHA-256, pause/resume, cancellation, history cleanup, and per-file controls. Application smoke tests cover WPF, localization, the conflict dialog, and shutdown scenarios.

### Installer

Build the installer with:

```powershell
.\installer\Build-Installer.ps1
```

The script publishes a self-contained `win-x64` build and creates an installer with Inno Setup 7. Release binaries are published under [Releases](https://github.com/quendae/WinCopyQueue/releases) rather than stored in the repository.

## Project structure

```text
src/WinCopyQueue.Core/       queue logic and file operations
src/WinCopyQueue.App/        WPF app, tray, and Explorer integration
tests/                       core and application smoke tests
installer/                   Inno Setup definition and build script
```
