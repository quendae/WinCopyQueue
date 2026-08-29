<p align="center">
  <img src="src/WinCopyQueue.App/Assets/logo-full.png" alt="WinCopyQueue" width="420">
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.pl.md">Polski</a> · <a href="README.de.md">Deutsch</a> · <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.zh-CN.md">简体中文</a> · <strong>日本語</strong>
</p>

# WinCopyQueue

WinCopyQueue は、Windows エクスプローラーにシンプルなコピー／移動キューを追加するアプリです。複数の転送を同時に実行するのではなく、セッションを順番に処理し、常に 1 ファイルずつ転送します。

アプリはシステムトレイに常駐し、メインウィンドウを常に表示し続けることはありません。転送を追加したときだけコンパクトなキューパネルが表示され、いつでも非表示にできます。非表示にしても転送はバックグラウンドで継続します。

## ダウンロード

現在のバージョン：**1.0.0**

- [WinCopyQueue 1.0.0 インストーラーをダウンロード](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue-Setup-1.0.0-x64.exe)
- [単体版 WinCopyQueue.exe をダウンロード](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue.exe)
- [v1.0.0 リリースを見る](https://github.com/quendae/WinCopyQueue/releases/tag/v1.0.0)

WinCopyQueue は **Windows 10 1809 以降**に対応しており、Windows 11 でも動作します。インストーラーはユーザー単位で動作し、管理者権限は必要ありません。

> このリポジトリには現在 `LICENSE` ファイルがありません。

## 使い方

1. `WinCopyQueue.exe` を起動します。
2. Windows エクスプローラーで、通常どおり `Ctrl+C` / `Ctrl+X` でファイルをコピーまたは切り取ります。
3. 移動先フォルダーで `Ctrl+V` を押すか、コンテキストメニューから **WinCopyQueue で貼り付け** を選択します。

すでに転送が実行中の場合、新しい転送はキューの末尾に追加されます。これにより、複数の大きな処理が同じディスクを同時に奪い合うことを防げます。

Windows 11 では、静的なコンテキストメニュー項目が **その他のオプションを表示** の中に表示される場合があります。

<p align="center">
  <img src="docs/images/WinCopyQueue_screenshot.png" alt="転送中の WinCopyQueue" width="480">
</p>

## 主な機能

- 個別ファイルおよびフォルダー全体のコピー／移動、
- 複数の独立したセッションを 1 つの順次キューで処理、
- キュー全体または個別ファイルの一時停止／再開、
- セッション全体または選択したファイルのキャンセル、
- すでに正常にコピーされたファイルを残したままセッションをキャンセル、
- パス、サイズ、更新日時を比較する競合処理、
- **置換**、**スキップ**、**セッションをキャンセル** を選択し、以降の競合にも同じ選択を適用可能、
- 現在のファイル、進捗、ファイル数、転送速度を表示するコンパクトなキューパネル、
- すべてのファイルと状態を確認できる展開可能な仮想化リスト、
- 完了、キャンセル、失敗したセッションの履歴、
- 転送の追加、完了、失敗を知らせるシステム通知、
- Windows 起動時の自動起動を任意で有効化、
- 8 言語の UI：英語、ポーランド語、ドイツ語、フランス語、スペイン語、ポルトガル語、簡体字中国語、日本語。

## より安全なコピーと移動

WinCopyQueue は、不完全なファイルを最終ファイル名で直接保存しません。まず一時ファイル `*.queue-part-*` に書き込み、転送が正常に完了した後でのみ最終ファイル名として公開します。

通常のコピーでは、任意で **SHA-256** 検証を有効にできます。コピー中にソースのハッシュを計算し、その後に宛先ファイルを再度読み込んで結果を比較します。

異なるボリューム間でファイルを移動する場合は、UI の設定に関係なく、ソースを削除する前に自動で検証が行われます。コピー、検証、または最終処理に失敗した場合、ソースファイルはそのまま残ります。

## キューパネルとシステムトレイ

転送を追加するとキューパネルが自動的に開き、エクスプローラーからフォーカスを奪わずに画面右下付近へ表示されます。最小化しても転送はバックグラウンドで継続します。

トレイアイコンをダブルクリックするか、**キューを表示** を選ぶとパネルを再表示できます。トレイメニューからは、キュー全体の一時停止／再開、自動起動の切り替え、エクスプローラー連携の修復、アプリの終了も行えます。

## ファイル競合

宛先に同名ファイルがすでに存在する場合、WinCopyQueue は両方のファイルについてサイズと更新日時を表示します。選択できる操作は次の 3 つです。

- **置換**、
- **スキップ**、
- **セッションをキャンセル**。

「置換」または「スキップ」は、同じセッション内の以降の競合すべてに適用することもできます。

## 設定と診断

ユーザー設定は次の場所に保存されます：

```text
%LOCALAPPDATA%\WinCopyQueue\settings.json
```

診断ログは次の場所に保存されます：

```text
%LOCALAPPDATA%\WinCopyQueue\WinCopyQueue.log
```

選択した言語と SHA-256 検証設定は次回起動時にも保持されます。

## コマンドライン

WinCopyQueue はコマンドラインから直接転送要求を受け取ることもできます：

```powershell
WinCopyQueue.exe --copy "D:\Destination" "D:\File.txt" "D:\Folder"
WinCopyQueue.exe --move "D:\Destination" "D:\File.txt"
WinCopyQueue.exe --paste "D:\Destination"
```

アプリを再度起動しても別のキューは作成されません。コマンドは named pipe を通じてメインプロセスへ転送されます。

## プロジェクトのビルド

.NET 10 SDK が必要です。

```powershell
dotnet restore WinCopyQueue.slnx --configfile NuGet.Config
dotnet build WinCopyQueue.slnx --no-restore -c Release
```

リポジトリからアプリを実行：

```powershell
dotnet run --project src\WinCopyQueue.App\WinCopyQueue.App.csproj --no-restore
```

### テスト

```powershell
dotnet run --project tests\WinCopyQueue.Core.SmokeTests\WinCopyQueue.Core.SmokeTests.csproj --no-build -c Release
dotnet run --project tests\WinCopyQueue.App.SmokeTests\WinCopyQueue.App.SmokeTests.csproj --no-build -c Release
```

Core の smoke test は、分離された一時ファイルに対して実際のファイル操作を行い、セッション順序、競合、SHA-256、一時停止／再開、キャンセル、履歴の削除、ファイル単位の制御などを確認します。アプリ側のテストでは WPF、ローカライズ、競合ダイアログ、終了処理を確認します。

### インストーラー

次のスクリプトでインストーラーをビルドします：

```powershell
.\installer\Build-Installer.ps1
```

スクリプトは自己完結型の `win-x64` ビルドを公開し、Inno Setup 7 を使ってインストーラーを生成します。配布用バイナリはリポジトリには保存せず、[Releases](https://github.com/quendae/WinCopyQueue/releases) で公開します。

## プロジェクト構成

```text
src/WinCopyQueue.Core/       キューのロジックとファイル操作
src/WinCopyQueue.App/        WPF アプリ、トレイ、Explorer 連携
tests/                       Core とアプリの smoke test
installer/                   Inno Setup 定義とビルドスクリプト
```
