param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$InnoCompiler
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repositoryRoot 'artifacts\publish'
$installerDirectory = Join-Path $repositoryRoot 'artifacts\installer'

if (-not $InnoCompiler) {
    $candidates = @(
        (Join-Path $repositoryRoot '.tools\InnoSetup\ISCC.exe'),
        'C:\Program Files\Inno Setup 7\ISCC.exe',
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    )
    $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $InnoCompiler -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    throw 'Nie znaleziono ISCC.exe. Zainstaluj Inno Setup 7 lub podaj -InnoCompiler.'
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null

$appProject = Join-Path $repositoryRoot 'src\WinCopyQueue.App\WinCopyQueue.App.csproj'
dotnet restore $appProject `
    --runtime $Runtime `
    --configfile (Join-Path $repositoryRoot 'NuGet.Config')

if ($LASTEXITCODE -ne 0) {
    throw 'Przywracanie zależności publikacji nie powiodło się.'
}

dotnet publish $appProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --no-restore `
    --output $publishDirectory `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None

if ($LASTEXITCODE -ne 0) {
    throw 'Publikacja WinCopyQueue nie powiodła się.'
}

& $InnoCompiler (Join-Path $PSScriptRoot 'WinCopyQueue.iss')
if ($LASTEXITCODE -ne 0) {
    throw 'Budowanie instalatora nie powiodło się.'
}

Get-ChildItem -LiteralPath $installerDirectory -Filter 'WinCopyQueue-Setup-*.exe'
