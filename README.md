# WinCopyQueue

WinCopyQueue to działająca w trayu integracja z Explorerem Windows. Wykonuje transfery sekwencyjnie: jedna sesja po drugiej i jeden plik naraz. Nie ma stałego głównego okna — kompaktowy podgląd kolejki pojawia się dopiero po dodaniu transferu.

Kompletny opis projektu przeznaczony dla modeli językowych i narzędzi programistycznych znajduje się w pliku [LLMS.md](LLMS.md).

## Aktualny zakres MVP

- kopiowanie i przenoszenie plików oraz całych folderów,
- wiele niezależnych sesji we wspólnej kolejce,
- powiadomienia systemowe o dodaniu i zakończeniu sesji,
- globalna pauza i wznowienie z menu ikony w trayu,
- dialog konfliktu z porównaniem rozmiaru i daty modyfikacji; dostępne decyzje to zastąpienie, pominięcie lub anulowanie sesji,
- zapis przez plik tymczasowy, a następnie atomowa zmiana nazwy,
- opcjonalna weryfikacja SHA-256 po skopiowaniu; przy przenoszeniu między woluminami weryfikacja jest wymuszana przed usunięciem źródła,
- pojedyncza instancja aplikacji; następne wywołania przekazują zlecenia przez ramkowany named pipe,
- kompaktowy panel z bieżącym plikiem, szybkością, następnymi pozycjami i postępem; po zakończeniu kolejki chowa się automatycznie,
- rozwijana, wirtualizowana lista wszystkich plików i ich stanów,
- pauza i wznowienie całej kolejki, anulowanie sesji oraz pauza, wznowienie i anulowanie pojedynczych plików,
- opcjonalny autostart ustawiany w instalatorze lub później z menu traya,
- interfejs w ośmiu językach (polski, angielski, niemiecki, francuski, hiszpański, portugalski, chiński uproszczony i japoński), domyślnie dobrany z ustawień Windows,
- czyszczenie historii ukończonych sesji oraz potwierdzane usuwanie sesji anulowanych, błędnych i wstrzymanych,
- automatyczna instalacja menu kontekstowego Explorera dla bieżącego użytkownika, bez uprawnień administratora,
- przechwytywanie `Ctrl+V` tylko w Explorerze i tylko dla schowka zawierającego pliki (`CF_HDROP`),
- dziennik diagnostyczny w `%LOCALAPPDATA%\WinCopyQueue\WinCopyQueue.log`.

## Użycie z Eksploratorem

1. Uruchom `WinCopyQueue.exe`. Program pojawi się w trayu i automatycznie zarejestruje integrację.
2. Zaznacz pliki lub foldery i użyj zwykłego `Ctrl+C` albo `Ctrl+X`.
3. W folderze docelowym użyj `Ctrl+V` albo kliknij prawym przyciskiem i wybierz **Wklej z WinCopyQueue**.

Po dodaniu transferu panel kolejki otworzy się przy prawym dolnym rogu ekranu, nie odbierając fokusu Explorerowi. Można go ukryć przyciskiem minimalizacji `—`; kopiowanie będzie działać dalej. Dwuklik ikony w trayu lub polecenie **Pokaż kolejkę** otwiera go ponownie.

Przycisk **Wstrzymaj/Wznów** steruje całą sekwencyjną kolejką. **Anuluj** dotyczy wybranej sesji; pliki ukończone przed anulowaniem pozostają w miejscu docelowym.

Po rozwinięciu **Listy plików** każda oczekująca lub aktywna pozycja ma własne przyciski **Pauza/Wznów** i **Anuluj**. Jeśli w miejscu docelowym istnieje plik o tej samej nazwie, WinCopyQueue pokazuje oba rozmiary i daty modyfikacji. Decyzję zastąpienia lub pominięcia można zastosować również do wszystkich następnych konfliktów w danej sesji.

Opcję **Weryfikuj pliki po skopiowaniu (SHA-256)** można przełączyć w dolnym pasku panelu. Weryfikacja odczytuje plik docelowy ponownie, dlatego zwiększa czas transferu. Przy przenoszeniu między różnymi woluminami jest wykonywana niezależnie od ustawienia, zanim źródło zostanie usunięte. Obok znajduje się wybór języka; wybrana wartość jest zapamiętywana.

Na Windows 11 statyczny wpis może być widoczny w menu **Pokaż więcej opcji**. Jeśli rejestracja została przeniesiona wraz z plikiem wykonywalnym, wybierz z menu ikony w trayu **Napraw integrację z Explorerem**.

## Instalator

Gotowy instalator per-user znajduje się w `artifacts\installer\WinCopyQueue-Setup-1.0.0-x64.exe`. Nie wymaga uprawnień administratora. Kreator pozwala włączyć uruchamianie wraz z systemem i opcjonalny skrót na pulpicie; autostart można później przełączyć również w menu ikony w trayu.

Ponowne zbudowanie pakietu po zmianach:

```powershell
.\installer\Build-Installer.ps1
```

Skrypt publikuje samowystarczalną wersję `win-x64` i kompiluje pojedynczy instalator EXE za pomocą Inno Setup 7.

## Uruchomienie deweloperskie

```powershell
dotnet restore WinCopyQueue.slnx --configfile NuGet.Config
dotnet run --project src\WinCopyQueue.App\WinCopyQueue.App.csproj --no-restore
```

Można również dodać sesję z linii poleceń:

```powershell
WinCopyQueue.exe --copy "D:\Cel" "D:\Plik.txt" "D:\Folder"
WinCopyQueue.exe --move "D:\Cel" "D:\Plik.txt"
WinCopyQueue.exe --paste "D:\Cel"
```

## Weryfikacja

```powershell
dotnet build WinCopyQueue.slnx --no-restore
dotnet run --project tests\WinCopyQueue.Core.SmokeTests\WinCopyQueue.Core.SmokeTests.csproj --no-restore
dotnet run --project tests\WinCopyQueue.App.SmokeTests\WinCopyQueue.App.SmokeTests.csproj --no-restore
```

Test rdzenia tworzy odizolowane dane w katalogu tymczasowym i sprawdza kolejność, konflikty oraz sterowanie sesją i pojedynczymi plikami. Test WPF weryfikuje ładowanie dialogu konfliktu i prezentację metadanych.

## Grafiki

- `src\WinCopyQueue.App\Assets\WinCopyQueue.ico` — wielorozmiarowa ikona EXE i traya,
- `src\WinCopyQueue.App\Assets\tray-icon.png` — przezroczyste źródło ikony,
- `src\WinCopyQueue.App\Assets\logo-full.png` — pełne logo dostarczone do projektu.
