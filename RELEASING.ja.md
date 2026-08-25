# DalamudMCP リリース手順

この手順では、Dalamud プラグイン、NuGet の MCP Server パッケージ、GitHub Release、公式 MCP Registry の版をそろえて公開します。

## 1. バージョンを更新する

次の値を同じバージョンに変更します。

- `Directory.Build.props` の `Version`
- `src/DalamudMCP.Plugin/DalamudMCP.json` の `AssemblyVersion`
- `.mcp/server.json` の `version`
- `.mcp/server.json` にある NuGet パッケージの `version`
- README と日本語セットアップガイドにある `dnx` コマンドのバージョン

`AssemblyVersion` は末尾に `.0` を付けます。
たとえばリリースが `1.1.0` なら、プラグインの値は `1.1.0.0` です。

## 2. 品質チェックを実行する

```powershell
.\build\restore.ps1 -Solution .\DalamudMCP.CI.slnx
.\build\quality.ps1 -Solution .\DalamudMCP.CI.slnx
```

プラグインを含む Release ビルドも実行します。

```powershell
.\.dotnet\dotnet.exe build .\src\DalamudMCP.Plugin\DalamudMCP.Plugin.csproj -c Release
```

Dalamud が標準の開発パス以外にある場合は、`DALAMUD_HOME` を指定します。

## 3. NuGet パッケージを検査する

```powershell
.\.dotnet\dotnet.exe pack .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj `
  -c Release `
  --no-restore `
  -o .\artifacts\nuget
```

作成した `.nupkg` に次の項目が含まれることを確認します。

- package type の `DotnetTool` と `McpServer`
- `README.md`
- `.mcp/server.json`
- `DalamudMCP.Cli.dll`
- `DalamudMCP.Protocol.dll`

ローカルパッケージを `dnx` で起動し、`serve mcp` が CLI へ渡ることも確認します。

```powershell
.\.dotnet\dotnet.exe dnx DalamudMCP.Cli@1.1.0 `
  --source .\artifacts\nuget `
  --yes `
  serve mcp
```

プラグインが停止している場合は接続エラーになりますが、CLI が起動して引数を解釈できていればパッケージの起動確認は完了です。

## 4. PR をマージする

PR の CI とマージ可能状態を確認してから `main` へマージします。
NuGet と公式 MCP Registry は、マージ後のコミットから作った成果物を公開します。

## 5. NuGet.org へ公開する

公開直前に、同じパッケージ ID とバージョンが未使用であることを確認します。
NuGet.org で `Garume` 所有者の短期 API キーを作り、対象を `DalamudMCP.Cli` に限定します。
そのキーをリポジトリの Actions secret `NUGET_API_KEY` に登録します。

GitHub Actions の `publish-nuget` workflow を手動実行し、公開する annotated tag を指定します。
workflow はタグと `Directory.Build.props` のバージョンが一致することを確認し、そのタグのソースからパッケージを作成して公開します。

ローカルから公開する必要がある場合だけ、API キーを現在の PowerShell セッションの環境変数へ設定して次のコマンドを実行します。

```powershell
.\.dotnet\dotnet.exe nuget push .\artifacts\nuget\DalamudMCP.Cli.1.1.0.nupkg `
  --api-key $env:DALAMUDMCP_NUGET_API_KEY `
  --source https://api.nuget.org/v3/index.json
```

公開後は NuGet の flat container API からパッケージと README を取得できるまで待ちます。

## 6. GitHub Release を作成する

プラグイン成果物は次の場所に作成されます。

```text
src/DalamudMCP.Plugin/bin/Release/DalamudMCP/latest.zip
```

`v<version>` の annotated tag を `main` のマージコミットへ作り、`latest.zip` とチェックサムを GitHub Release へ添付します。
タグと Release を作る前に、NuGet へ公開した版と一致していることをもう一度確認します。

## 7. 公式 MCP Registry へ公開する

公式の `mcp-publisher` を取得し、GitHub アカウントでログインします。

```powershell
.\mcp-publisher.exe login github
.\mcp-publisher.exe publish .\.mcp\server.json
```

公開後は `io.github.getsetmind/dalamudmcp` を Registry API で検索し、NuGet パッケージの版と起動引数を確認します。

## 8. 掲載先を確認する

awesome-mcp-servers や XIV Dev Wiki への掲載 PR は、各リポジトリの規約に従って作成します。
外部リポジトリの PR は管理者がマージするため、提出後はリンクと審査状況を記録します。
PulseMCP が公式 MCP Registry を取り込んでいる場合は、個別登録ではなく自動反映を確認します。
