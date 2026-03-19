# Tool Extension Guideline

## 1. 目的

この文書は、将来的に `Tool` が増えても設計が破綻しないように、追加手順とルールを固定する。

## 2. 結論

今の設計は拡張しやすい。ただし、`Tool` を自由に足すのではなく、必ず同じ増やし方をする必要がある。

`Tool` は「MCP に見せる名前付き関数」ではなく、`Application` の use case を公開するための thin adapter とみなす。

## 3. 追加時の必須セット

新しい `Tool` を 1 つ増やすときは、最低でも次の 8 点をセットで追加する。

1. `CapabilityDefinition`
2. `UseCase`
3. `InputSchema`
4. `OutputSchema`
5. `ToolHandler`
6. `ExposurePolicy` 登録
7. 設定 UI 用メタデータ
8. テスト一式

この 8 点のどれかが欠ける追加は禁止。

## 4. 依存方向

```text
ToolHandler -> UseCase -> Port -> Adapter
```

逆方向は禁止。

禁止例:

- `UseCase` が MCP request DTO を受け取る
- `ToolHandler` が Dalamud service を直接触る
- `ToolHandler` が allowlist 判定を独自に持つ

## 5. CapabilityDefinition を中心にする

新しい `Tool` は必ず `CapabilityDefinition` から始める。

`CapabilityDefinition` に持たせるべき項目:

- `Id`
- `DisplayName`
- `Description`
- `Category`
- `Sensitivity`
- `Profile`
- `DefaultEnabled`
- `RequiresConsent`
- `SupportsTool`
- `SupportsResource`

これを single source of truth として使う。

## 6. メタデータの一元供給

同じ metadata を以下で再利用する。

- MCP `tools/list`
- 設定 UI の tool 一覧
- 監査ログの capability 名
- ドキュメント生成
- profile / sensitivity の判定

つまり、`Host` 用 metadata と `Plugin UI` 用 metadata を別管理しない。

## 7. ToolHandler の責務

`ToolHandler` は次だけを行う。

1. request の schema validation
2. capability exposure 判定
3. use case 呼び出し
4. response mapping

`ToolHandler` に書いてはいけないもの:

- business rule
- data acquisition logic
- retry policy
- file IO
- native UI traversal

## 8. UseCase の責務

`UseCase` は tool 非依存であるべき。

例:

- `GetPlayerContextUseCase`
- `GetAddonTreeUseCase`
- `GetInventorySummaryUseCase`

同じ use case が将来:

- MCP tool
- MCP resource
- plugin UI preview
- local debug console

から再利用できる状態を保つ。

## 9. Tool の分類

`Tool` は追加前に必ず次のどれかに分類する。

### A. Snapshot Tool

現在状態を読む。

例:

- `get_player_context`
- `get_duty_context`

### B. Collection Tool

一覧や複数要素を返す。

例:

- `get_addon_list`
- `get_inventory_summary`

### C. Introspection Tool

構造を掘る。

例:

- `get_addon_tree`
- `get_addon_strings`

### D. Experimental Action Tool

private experimental profile に属する one-shot 補助操作。

baseline では禁止。

この分類で、追加時の sensitivity と profile が決めやすくなる。

## 10. schema 設計ルール

- すべて `JSON Schema 2020-12`
- input はなるべく狭くする
- output は envelope を持つ
- unavailable は protocol error より通常レスポンスを優先
- breaking change は schema version を上げる

## 11. ツール追加テンプレート

```text
Domain
  CapabilityDefinition
Application
  XxxUseCase
  IXxxReader / IXxxService
Infrastructure
  XxxReader
Host
  XxxToolHandler
  XxxInputSchema
  XxxOutputSchema
Plugin
  Settings metadata
Tests
  Domain/Application/Host/Integration
```

## 12. 追加時チェックリスト

- sensitivity は妥当か
- baseline / experimental のどちらか
- tool だけでなく resource も必要か
- settings UI に表示されるか
- audit log に記録されるか
- deny list に触れていないか
- other-player data を含まないか
- schema に optional/null 戦略があるか
- tests が揃っているか

## 13. 将来の破綻ポイント

### 危険 1

`ToolHandler` が肥大化する。

対策:

- handler は 1 tool 1 class
- use case への委譲を強制

### 危険 2

capability metadata が分散する。

対策:

- registry を単一化

### 危険 3

settings UI と host の公開面がずれる。

対策:

- 同じ registry を両方で参照

### 危険 4

FF14 固有の read model が tool ごとに乱立する。

対策:

- `Reader` を capability 単位ではなくドメイン文脈単位に切る

## 14. 結論

今の設計は、`CapabilityDefinition -> UseCase -> ToolHandler` という追加手順を守る限り、`Tool` は比較的安全に増やせる。
