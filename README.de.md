<p align="center">
  <img src="src/WinCopyQueue.App/Assets/logo-full.png" alt="WinCopyQueue" width="420">
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.pl.md">Polski</a> · <strong>Deutsch</strong> · <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a>
</p>

# WinCopyQueue

WinCopyQueue erweitert den Windows-Explorer um eine einfache Warteschlange für Kopier- und Verschiebevorgänge. Statt mehrere Transfers gleichzeitig auszuführen, werden sie nacheinander verarbeitet — eine Sitzung nach der anderen und jeweils eine Datei zur gleichen Zeit.

Die Anwendung läuft im Infobereich und hält kein dauerhaftes Hauptfenster geöffnet. Das kompakte Warteschlangenfenster erscheint erst, wenn ein Transfer hinzugefügt wird, kann jederzeit ausgeblendet werden und die Übertragung läuft im Hintergrund weiter.

## Download

Aktuelle Version: **1.0.0**

- [WinCopyQueue 1.0.0 Installer herunterladen](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue-Setup-1.0.0-x64.exe)
- [Eigenständige WinCopyQueue.exe herunterladen](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue.exe)
- [Release v1.0.0 anzeigen](https://github.com/quendae/WinCopyQueue/releases/tag/v1.0.0)

WinCopyQueue unterstützt **Windows 10 1809 oder neuer**, einschließlich Windows 11. Der Installer wird nur für den aktuellen Benutzer installiert und benötigt keine Administratorrechte.

> Dieses Repository enthält derzeit keine `LICENSE`-Datei.

## Funktionsweise

1. `WinCopyQueue.exe` starten.
2. Im Windows-Explorer Dateien wie gewohnt mit `Ctrl+C` / `Ctrl+X` kopieren oder ausschneiden.
3. Im Zielordner `Ctrl+V` drücken oder im Kontextmenü **Mit WinCopyQueue einfügen** auswählen.

Wenn bereits ein Transfer läuft, wird der nächste einfach am Ende der Warteschlange hinzugefügt. Dadurch konkurrieren mehrere große Vorgänge nicht gleichzeitig um denselben Datenträger.

Unter Windows 11 kann der statische Kontextmenüeintrag unter **Weitere Optionen anzeigen** erscheinen.

<p align="center">
  <img src="docs/images/WinCopyQueue_screenshot.png" alt="WinCopyQueue während eines aktiven Transfers" width="480">
</p>

## Wichtigste Funktionen

- Kopieren und Verschieben einzelner Dateien oder kompletter Ordner,
- mehrere unabhängige Sitzungen in einer gemeinsamen sequentiellen Warteschlange,
- gesamte Warteschlange oder einzelne Dateien pausieren und fortsetzen,
- komplette Sitzung oder einzelne Datei abbrechen,
- Sitzung abbrechen, ohne bereits erfolgreich kopierte Dateien zu entfernen,
- Konfliktbehandlung mit Vergleich von Pfad, Dateigröße und Änderungsdatum,
- Entscheidungen **Ersetzen**, **Überspringen** und **Sitzung abbrechen**, optional für weitere Konflikte derselben Sitzung übernehmen,
- kompaktes Warteschlangenfenster mit aktueller Datei, Fortschritt, Dateianzahl und Übertragungsgeschwindigkeit,
- ausklappbare virtualisierte Liste aller Dateien und Zustände,
- Verlauf abgeschlossener, abgebrochener und fehlgeschlagener Sitzungen,
- Systembenachrichtigungen für hinzugefügte, abgeschlossene und fehlgeschlagene Transfers,
- optionaler Autostart mit Windows,
- acht Oberflächensprachen: Englisch, Polnisch, Deutsch, Französisch, Spanisch, Portugiesisch, Vereinfachtes Chinesisch und Japanisch.

## Sichereres Kopieren und Verschieben

WinCopyQueue schreibt eine unvollständige Datei nicht direkt unter ihrem endgültigen Zielnamen. Die Daten werden zuerst in eine temporäre Datei `*.queue-part-*` geschrieben und erst nach erfolgreichem Abschluss unter dem endgültigen Namen veröffentlicht.

Für normale Kopiervorgänge kann optional eine **SHA-256**-Prüfung aktiviert werden. Dabei wird die Quelldatei während des Kopierens gehasht und die Zieldatei anschließend erneut gelesen und verglichen.

Beim Verschieben zwischen unterschiedlichen Volumes wird die Prüfung automatisch durchgeführt, bevor die Quelldatei gelöscht wird — unabhängig von der UI-Einstellung. Wenn Kopieren, Prüfung oder Finalisierung fehlschlägt, bleibt die Quelle unverändert erhalten.

## Warteschlangenfenster und Tray

Das Warteschlangenfenster öffnet sich automatisch, sobald ein Transfer hinzugefügt wird, und erscheint unten rechts auf dem Bildschirm, ohne dem Explorer den Fokus zu nehmen. Es kann minimiert werden, während die Transfers im Hintergrund weiterlaufen.

Ein Doppelklick auf das Tray-Symbol oder **Warteschlange anzeigen** öffnet das Fenster erneut. Über das Tray-Menü kann außerdem die gesamte Warteschlange pausiert oder fortgesetzt, Autostart umgeschaltet, die Explorer-Integration repariert und die Anwendung beendet werden.

## Dateikonflikte

Wenn am Ziel bereits eine Datei mit demselben Namen existiert, zeigt WinCopyQueue beide Dateien inklusive Größe und Änderungsdatum an. Drei Aktionen stehen zur Verfügung:

- **Ersetzen**,
- **Überspringen**,
- **Sitzung abbrechen**.

Ersetzen oder Überspringen kann auch auf alle späteren Konflikte derselben Sitzung angewendet werden.

## Einstellungen und Diagnose

Benutzereinstellungen werden gespeichert unter:

```text
%LOCALAPPDATA%\WinCopyQueue\settings.json
```

Das Diagnoseprotokoll befindet sich unter:

```text
%LOCALAPPDATA%\WinCopyQueue\WinCopyQueue.log
```

Die gewählte Sprache und die SHA-256-Prüfeinstellung bleiben zwischen den Starts erhalten.

## Kommandozeile

WinCopyQueue kann Transfers auch direkt über die Kommandozeile annehmen:

```powershell
WinCopyQueue.exe --copy "D:\Ziel" "D:\Datei.txt" "D:\Ordner"
WinCopyQueue.exe --move "D:\Ziel" "D:\Datei.txt"
WinCopyQueue.exe --paste "D:\Ziel"
```

Ein erneuter Programmstart erzeugt keine zweite Warteschlange. Befehle werden über eine Named Pipe an den Hauptprozess weitergeleitet.

## Projekt bauen

.NET 10 SDK wird benötigt.

```powershell
dotnet restore WinCopyQueue.slnx --configfile NuGet.Config
dotnet build WinCopyQueue.slnx --no-restore -c Release
```

Anwendung direkt aus dem Repository starten:

```powershell
dotnet run --project src\WinCopyQueue.App\WinCopyQueue.App.csproj --no-restore
```

### Tests

```powershell
dotnet run --project tests\WinCopyQueue.Core.SmokeTests\WinCopyQueue.Core.SmokeTests.csproj --no-build -c Release
dotnet run --project tests\WinCopyQueue.App.SmokeTests\WinCopyQueue.App.SmokeTests.csproj --no-build -c Release
```

Die Core-Smoke-Tests führen echte Dateioperationen in isolierten temporären Verzeichnissen aus und prüfen unter anderem Sitzungsreihenfolge, Konflikte, SHA-256, Pause/Fortsetzen, Abbruch, Verlaufsbereinigung und Steuerung einzelner Dateien. Die Anwendungstests prüfen WPF, Lokalisierung, Konfliktdialog und Shutdown-Szenarien.

### Installer

Installer bauen mit:

```powershell
.\installer\Build-Installer.ps1
```

Das Skript veröffentlicht einen eigenständigen `win-x64`-Build und erstellt mit Inno Setup 7 einen Installer. Fertige Binärdateien werden unter [Releases](https://github.com/quendae/WinCopyQueue/releases) veröffentlicht und nicht im Repository gespeichert.

## Projektstruktur

```text
src/WinCopyQueue.Core/       Warteschlangenlogik und Dateioperationen
src/WinCopyQueue.App/        WPF-App, Tray und Explorer-Integration
tests/                       Smoke-Tests für Core und Anwendung
installer/                   Inno-Setup-Definition und Build-Skript
```
