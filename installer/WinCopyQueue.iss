#define AppName "WinCopyQueue"
#define AppVersion "1.0.0"
#define AppExeName "WinCopyQueue.exe"

[Setup]
AppId={{D6A28C8D-64B7-47D5-9C42-046894155A2A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=WinCopyQueue
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=WinCopyQueue-Setup-{#AppVersion}-x64
SetupIconFile=..\src\WinCopyQueue.App\Assets\WinCopyQueue.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.17763

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "startup"; Description: "Uruchamiaj WinCopyQueue wraz ze startem systemu"; GroupDescription: "Autostart:"; Flags: unchecked
Name: "desktopicon"; Description: "Utwórz skrót na pulpicie"; GroupDescription: "Skróty:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\WinCopyQueue.App.exe"

[Icons]
Name: "{autoprograms}\WinCopyQueue"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{autodesktop}\WinCopyQueue"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WinCopyQueue"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WinCopyQueue.Paste"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Wklej z WinCopyQueue"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WinCopyQueue.Paste"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WinCopyQueue.Paste"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WinCopyQueue.Paste\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" --paste ""%V"""
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WinCopyQueue.Paste"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Wklej z WinCopyQueue"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WinCopyQueue.Paste"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WinCopyQueue.Paste"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WinCopyQueue.Paste\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" --paste ""%1"""

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Uruchom WinCopyQueue"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not WizardIsTaskSelected('startup')) then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WinCopyQueue');
end;
