# DalamudMCP 日本語セットアップガイド

DalamudMCP は、Final Fantasy XIV（FFXIV / FF14）の状態を MCP クライアントやコマンドラインから扱うための、Windows 向けローカルサーバーと Dalamud プラグインです。
ゲーム内の処理は Dalamud プラグインが担当し、CLI と MCP サーバーは同じ Windows ユーザーだけが接続できる名前付きパイプを通して通信します。

[English README](./README.md)

## 必要なもの

- Windows
- XIVLauncher と Dalamud を使って起動した Final Fantasy XIV
- .NET 10 SDK
- プラグインをビルドするための Dalamud 開発環境

## ビルド

PowerShell でリポジトリのルートを開き、依存関係の復元とビルドを実行します。

```powershell
.\build\restore.ps1
.\build\build.ps1 -NoRestore
```

Dalamud が標準の開発パス以外にある場合は、`DALAMUD_HOME` を指定します。

```powershell
$env:DALAMUD_HOME = 'C:\path\to\Hooks\dev'
.\build\build.ps1 -NoRestore -DalamudHome $env:DALAMUD_HOME
```

## プラグインの読み込み

Dalamud の開発用プラグインとして、次の DLL を読み込みます。

```text
src/DalamudMCP.Plugin/bin/Debug/DalamudMCP.dll
```

読み込み後、DalamudMCP の設定画面を開き、接続状態と許可する機能を確認します。

## CLI の動作確認

プラグインを読み込んだ状態で、プレイヤー情報やセッション状態を取得します。

```powershell
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- player context
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- session status --json
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- inventory summary
```

通常は起動中のプラグインを自動検出するため、パイプ名の指定は不要です。

## MCP サーバーの起動

標準入出力で MCP サーバーを起動する場合は、次のコマンドを使います。

```powershell
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve mcp
```

バージョン 1.1.0 が NuGet.org へ公開された後は、CLI を常設せずに起動できます。

```powershell
dnx DalamudMCP.Cli@1.1.0 --yes serve mcp
```

NuGet パッケージに含まれるのは CLI と MCP サーバーです。
Dalamud プラグインは別途ビルドして読み込み、FFXIV とともに起動しておく必要があります。

ローカル HTTP で起動する場合は、Bearer トークンを設定します。

```powershell
$env:DALAMUD_MCP_HTTP_TOKEN = '<token>'
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve http --port 38473
```

既定のエンドポイントは `http://127.0.0.1:38473/mcp` です。
HTTP クライアントは `Authorization: Bearer <token>` ヘッダーを送信する必要があります。
プラグインの設定画面から HTTP サーバーを起動した場合は、エンドポイントと自動生成されたトークンを個別にコピーできます。

## 安全設定

状態を読む機能は初期状態で有効です。
ゲーム内操作を変更する機能は初期状態で無効になっており、プラグインの設定画面で明示的に許可する必要があります。
危険性の高い連携機能には別の開発者向け設定があり、許可されていない機能は MCP のツール一覧にも表示されません。
許可範囲の詳しい設定方法は、[Capability Kernel](./design/capability-kernel.md) を参照してください。

## うまく接続できない場合

1. FFXIV と DalamudMCP プラグインが起動していることを確認します。
2. プラグインの設定画面でランタイムの状態を確認します。
3. CLI と FFXIV が同じ Windows ユーザーで動いていることを確認します。
4. HTTP 接続では、エンドポイントと Bearer トークンが一致していることを確認します。
5. ビルドに失敗する場合は、`DALAMUD_HOME` が Dalamud の `Hooks\dev` を指していることを確認します。

利用できるコマンドとツールの一覧は、[README の Current Tool Surface](./README.md#current-tool-surface) にあります。
