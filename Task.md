# Task

このタスク一覧は、`DalamudMCP` を 0 -> 100 で完成させるための実行計画です。設計文書一式をもとに、追記が不要になる前提で網羅的に分解しています。

## Rules

- `[]` は未着手
- `[x]` は完了
- メインタスクは上から順に進める
- 詳細タスクは原則として上から依存順に処理する
- 実装前に該当設計が存在しない作業は禁止
- すべての production code に対して automated test を用意する

1 []. Foundation と repository 基盤を構築する
   a [x]. `DalamudMCP/` 直下の最終ディレクトリ構成を確定する
   b [x]. `src/`, `tests/`, `build/`, `.github/workflows/`, `docs/` の役割を固定する
   c [x]. `DalamudMCP.sln` を作成する
   d [x]. `global.json` を追加して SDK version を固定する
   e [x]. `.editorconfig` を追加して code style の単一真実源を作る
   f [x]. `Directory.Build.props` を追加して共通 build 設定を定義する
   g [x]. `Directory.Build.targets` を追加して CI 補助 target を定義する
   h [x]. `packages.lock.json` 運用を solution 全体の前提にする
   i []. ルート README と設計文書のリンク整合を確認する
   j [x]. `Task.md` を以後の唯一の実行タスク一覧として固定する

2 [x]. solution と project shell を Clean Architecture で作成する
   a [x]. `src/DalamudMCP.Domain/` を作成する
   b [x]. `src/DalamudMCP.Application/` を作成する
   c [x]. `src/DalamudMCP.Contracts/` を作成する
   d [x]. `src/DalamudMCP.Infrastructure/` を作成する
   e [x]. `src/DalamudMCP.Plugin/` を作成する
   f [x]. `src/DalamudMCP.Host/` を作成する
   g [x]. `tests/DalamudMCP.Domain.Tests/` を作成する
   h [x]. `tests/DalamudMCP.Application.Tests/` を作成する
   i [x]. `tests/DalamudMCP.Contracts.Tests/` を作成する
   j [x]. `tests/DalamudMCP.Infrastructure.Tests/` を作成する
   k [x]. `tests/DalamudMCP.Host.Tests/` を作成する
   l [x]. `tests/DalamudMCP.Plugin.Tests/` を作成する
   m [x]. `tests/DalamudMCP.ArchitectureTests/` を作成する
   n [x]. 各 project の root namespace と assembly name を統一する
   o [x]. project reference が設計通り外側から内側だけに向くことを確認する

3 []. 共通品質基盤を導入する
   a [x]. `nullable` を全 project で有効化する
   b [x]. warnings as errors を全 project で有効化する
   c [x]. `EnforceCodeStyleInBuild = true` を有効化する
   d [x]. `AnalysisLevel` を固定する
   e [x]. built-in .NET analyzers を全 project で有効化する
   f [x]. xUnit analyzers を test project に導入する
   g [x]. `dotnet format --verify-no-changes` をローカル実行可能にする
   h [x]. code coverage 収集方式を `coverlet.MTP` で統一する
   i [x]. coverage 出力形式を `cobertura` に固定する
   j [x]. coverage threshold を solution / project 単位で設定する
   k [x]. formatter / analyzer / coverage のローカル実行コマンドを `build/` に用意する

4 []. Architecture Tests で依存ルールを強制する
   a [x]. `Plugin -> Domain` の直接依存禁止 test を書く
   b [x]. `Host -> Domain` の直接依存禁止 test を書く
   c [x]. `Application -> Infrastructure` の依存禁止 test を書く
   d [x]. `Domain -> Dalamud` 依存禁止 test を書く
   e [x]. `Domain -> MCP SDK` 依存禁止 test を書く
   f [x]. `Contracts -> Dalamud` 依存禁止 test を書く
   g [x]. `Contracts -> MCP DTO` 混入禁止 test を書く
   h [x]. `Plugin` が composition root と UI 以外の型を持たないことを検査する仕組みを作る
   i [x]. `Host` が transport / registry / handler 以外の責務を持たないことを検査する仕組みを作る
   j [x]. forbidden namespace / assembly reference の失敗メッセージを分かりやすくする

5 []. Domain 層の capability と policy モデルを実装する
   a [x]. `CapabilityId` を実装する
   b [x]. `CapabilityCategory` を実装する
   c [x]. `SensitivityLevel` を実装する
   d [x]. `ProfileType` を実装する
   e [x]. `CapabilityDefinition` を実装する
   f [x]. `ExposurePolicy` を実装する
   g [x]. `AuditEvent` を実装する
   h [x]. `SessionState` に相当する domain model が必要かを判断し、必要なら実装する
   i [x]. `Denied capability` の不変条件を domain で表現する
   j [x]. `DefaultEnabled` と `Denied` の矛盾を禁止する不変条件を実装する
   k [x]. `CapabilityDefinition` の一意性制約を表現する
   l [x]. Domain model から transport / persistence 都合の属性を排除する
   m [x]. Domain test で全不変条件を検証する

6 []. Domain 層の snapshot モデルを実装する
   a [x]. `PlayerContextSnapshot` を実装する
   b [x]. `DutyContextSnapshot` を実装する
   c [x]. `InventorySummarySnapshot` を実装する
   d [x]. `AddonSummary` を実装する
   e [x]. `AddonTreeSnapshot` を実装する
   f [x]. `NodeSnapshot` を実装する
   g [x]. `StringTableSnapshot` を実装する
   h [x]. `StringTableEntry` を実装する
   i [x]. `PositionSnapshot` を coarse precision 前提で実装する
   j [x]. snapshot の `capturedAt` / freshness に必要な最小フィールドを固定する
   k [x]. nullability と unavailable 戦略を domain 側の型で表現する
   l [x]. snapshot 正規化ルールを Domain test で固定する

7 []. Capability Registry と hard deny list を実装する
   a [x]. `CapabilityRegistry` の論理モデルを code-defined で実装する
   b [x]. `ToolBinding` を実装する
   c [x]. `ResourceBinding` を実装する
   d [x]. `AddonMetadata` を実装する
   e [x]. hard deny list を registry レベルで表現する
   f [x]. blocked addon を metadata で表現する
   g [x]. registry の一意性検証ロジックを実装する
   h [x]. `ToolName` 一意性検証を実装する
   i [x]. handler / schema / capability の欠落検出を実装する
   j [x]. registry self-validation test を作成する
   k []. blocked capability が settings UI で有効化不能であることを test で保証する
   l [x]. registry が Host と Plugin UI の両方から再利用できる形に整理する

8 []. Application 層の port と use case 基盤を実装する
   a [x]. `IClock` を定義する
   b [x]. `IPlayerContextReader` を定義する
   c [x]. `IDutyContextReader` を定義する
   d [x]. `IInventorySummaryReader` を定義する
   e [x]. `IAddonCatalogReader` を定義する
   f [x]. `IAddonTreeReader` を定義する
   g [x]. `IStringTableReader` を定義する
   h [x]. `ISettingsRepository` を定義する
   i [x]. `IAuditLogWriter` を定義する
   j []. `IBridgeServer` / `IBridgeClient` 抽象が必要なら定義する
   k [x]. `ExposurePolicyEvaluator` を実装する
   l [x]. `SnapshotFreshnessPolicy` を実装する
   m [x]. policy と port を use case からしか使わない構成に整理する

9 []. BaselineProfile の read-only use case を実装する
   a [x]. `GetPlayerContextUseCase` を実装する
   b [x]. `GetDutyContextUseCase` を実装する
   c [x]. `GetInventorySummaryUseCase` を実装する
   d [x]. `GetAddonListUseCase` を実装する
   e [x]. `GetAddonTreeUseCase` を実装する
   f [x]. `GetAddonStringsUseCase` を実装する
   g [x]. `ListExposedToolsUseCase` を実装する
   h [x]. `ListExposedResourcesUseCase` を実装する
   i [x]. `ListInspectableAddonsUseCase` を実装する
   j [x]. unavailable response 生成ルールを use case 層で統一する
   k [x]. `Denied` / `Disabled` / `NotReady` を明確に区別する
   l [x]. Application test で各 use case の正常系 / 異常系 / policy 拒否系を網羅する

10 []. 設定更新と監査の use case を実装する
   a [x]. `UpdateExposurePolicyUseCase` を実装する
   b [x]. `EnableToolUseCase` / `DisableToolUseCase` 相当の操作を実装する
   c [x]. `EnableResourceUseCase` / `DisableResourceUseCase` 相当の操作を実装する
   d [x]. `EnableAddonUseCase` / `DisableAddonUseCase` 相当の操作を実装する
   e [x]. `ApplyPresetUseCase` を実装する
   f [x]. `GetCurrentSettingsUseCase` を実装する
   g [x]. `RecordAuditEventUseCase` を実装する
   h []. high sensitivity 項目変更時の追加確認フラグを扱えるようにする
   i [x]. blocked 項目変更要求を拒否する
   j [x]. 設定変更に対する audit event 生成を test で保証する

11 []. Contracts 層の internal bridge 契約を実装する
   a [x]. request / response envelope を定義する
   b [x]. `GetPlayerContext` request/response contract を定義する
   c [x]. `GetDutyContext` request/response contract を定義する
   d [x]. `GetInventorySummary` request/response contract を定義する
   e [x]. `GetAddonList` request/response contract を定義する
   f [x]. `GetAddonTree` request/response contract を定義する
   g [x]. `GetAddonStrings` request/response contract を定義する
   h [x]. `GetCapabilityState` request/response contract を定義する
   i [x]. `contractVersion` 戦略を固定する
   j [x]. serialization / deserialization test を書く
   k [x]. backward incompatible change の検知 test を書く

12 []. Infrastructure 層の settings / audit / utility を実装する
   a [x]. `SystemClock` を実装する
   b [x]. `JsonSettingsRepository` を実装する
   c [x]. settings file path 戦略を決めて実装する
   d [x]. settings migration 戦略を実装する
   e [x]. `FileAuditLogWriter` を実装する
   f [x]. audit log rotation 方針が必要か判断し、必要なら実装する
   g [x]. broken settings file の復旧戦略を実装する
   h [x]. repository の同時書込み対策を実装する
   i [x]. Infrastructure test で persistence / migration / concurrency を検証する

13 []. Infrastructure 層の Dalamud 読取り adapter を実装する
   a [x]. `DalamudPlayerContextReader` を実装する
   b []. `DalamudDutyContextReader` を実装する
   c []. `DalamudInventorySummaryReader` を実装する
   d []. `DalamudAddonCatalogReader` を実装する
   e []. `DalamudAddonTreeReader` を実装する
   f []. `DalamudStringTableReader` を実装する
   g []. `ISeStringEvaluator` を使うかどうかを判断し、使うなら adapter を実装する
   h []. string table の raw / decoded 戦略を実装する
   i []. shallow tree depth を設定可能にする
   j []. high sensitivity addon を adapter レベルで取得対象から除外できるようにする
   k []. adapter test 用 fake / snapshot fixture を整備する
   l []. adapter の null / missing / destroyed UI 状態を網羅的に test する

14 []. Infrastructure 層の Named Pipe bridge を実装する
   a [x]. `NamedPipeBridgeServer` を実装する
   b [x]. `NamedPipeBridgeClient` を実装する
   c [x]. request routing を実装する
   d [x]. timeout 戦略を実装する
   e [x]. version mismatch 検出を実装する
   f [x]. capability state 問い合わせを実装する
   g [x]. unavailable / disabled / denied の応答を橋渡しできるようにする
   h [x]. bridge serialization test を書く
   i [x]. bridge integration test を書く
   j []. server / client 再接続 test を書く

15 []. Host 層の MCP server 基盤を実装する
   a [x]. `McpServerHost` を実装する
   b [x]. `StdioTransportHost` を実装する
   c [x]. `McpToolRegistry` を実装する
   d [x]. `McpResourceRegistry` を実装する
   e [x]. `initialize` ハンドシェイクを実装する
   f [x]. server metadata を実装する
   g []. capability negotiation を実装する
   h [x]. protocol version negotiation を実装する
   i [x]. tool listing を実装する
   j [x]. resource listing を実装する
   k [x]. `JSON Schema 2020-12` を出す仕組みを実装する
   l [x]. Host test で initialize / list / schema を網羅する

16 []. BaselineProfile の MCP tool surface を実装する
   a []. `get_player_context` input schema を実装する
   b []. `get_player_context` output schema を実装する
   c [x]. `GetPlayerContextToolHandler` を実装する
   d []. `get_duty_context` input/output schema を実装する
   e [x]. `GetDutyContextToolHandler` を実装する
   f []. `get_inventory_summary` input/output schema を実装する
   g [x]. `GetInventorySummaryToolHandler` を実装する
   h []. `get_addon_list` input/output schema を実装する
   i [x]. `GetAddonListToolHandler` を実装する
   j []. `get_addon_tree` input/output schema を実装する
   k [x]. `GetAddonTreeToolHandler` を実装する
   l []. `get_addon_strings` input/output schema を実装する
   m [x]. `GetAddonStringsToolHandler` を実装する
   n []. tool handler 共通ベースが必要なら実装する
   o []. handler が use case 以外のロジックを持たないことを test で保証する

17 []. BaselineProfile の MCP resource surface を実装する
   a [x]. `ffxiv://player/context` resource provider を実装する
   b [x]. `ffxiv://duty/context` resource provider を実装する
   c [x]. `ffxiv://inventory/summary` resource provider を実装する
   d [x]. `ffxiv://ui/addons` resource provider を実装する
   e [x]. `ffxiv://ui/addon/{name}/tree` resource provider を実装する
   f [x]. `ffxiv://ui/addon/{name}/strings` resource provider を実装する
   g [x]. resource URI validation を実装する
   h [x]. blocked / disabled / denied addon への resource access 拒否を実装する
   i [x]. resource MIME type 戦略を固定する
   j [x]. Resource test で URI validation / exposure policy / payload を検証する

18 []. Plugin 層の composition root を実装する
   a [x]. `PluginEntryPoint` を実装する
   b [x]. `PluginCompositionRoot` を実装する
   c [x]. Dalamud service の DI wiring を実装する
   d [x]. Application / Infrastructure / Contracts の解決を実装する
   e [x]. disposal と lifecycle cleanup を実装する
   f []. log category / plugin name の扱いを統一する
   g [x]. Plugin smoke test を作る
   h []. `Plugin.cs` 相当が thin のままであることを review / test で保証する

19 []. Plugin 層の設定 UI を実装する
   a []. `SettingsWindowVM` を実装する
   b []. Overview セクションを実装する
   c []. Tools セクションを実装する
   d []. Resources セクションを実装する
   e []. UI Addons セクションを実装する
   f []. Privacy & Audit セクションを実装する
   g []. Experimental セクションを実装する
   h []. 検索フィルタを実装する
   i []. sensitivity badge 表示を実装する
   j []. blocked 項目の disabled 表示を実装する
   k []. preset 適用 UI を実装する
   l []. `Apply` ベースの staged changes UI を実装する
   m []. high sensitivity 項目変更時の警告 UI を実装する
   n []. current exposure 状態の表示を実装する
   o []. Settings UI test を作成する

20 []. Plugin 層の audit / status UI を実装する
   a []. `AuditWindowVM` を実装する
   b []. recent audit event 一覧を表示する
   c []. blocked access request を表示する
   d []. active connection 状態を表示する
   e []. current profile 状態を表示する
   f []. panic stop UI を実装する
   g []. host 接続有無の可視表示を実装する
   h []. Audit UI test を作成する

21 []. allowlist / deny list / preset を end-to-end で実装する
   a []. `Recommended` preset を実装する
   b []. `UI Explorer` preset を実装する
   c []. `Locked Down` preset を実装する
   d []. preset 適用時に blocked 項目が混入しないことを保証する
   e []. tool / resource / addon の三系統を同一 policy で制御する
   f [x]. denied capability へのアクセス要求が audit に残るようにする
   g []. disabled capability へのアクセス要求の挙動を統一する
   h [x]. allowlist 変更が Host 公開面に反映されることを integration test で保証する

22 []. UI introspection を安全に実装する
   a []. addon allowlist だけを対象に列挙する仕組みを実装する
   b []. blocked addon を列挙対象から除外する
   c []. `summary`, `tree`, `strings`, `numbers` の mode 制御を実装する
   d []. deep tree opt-in を実装する
   e []. shallow tree の既定深さを実装する
   f []. addon name typo / missing addon に対する unavailable 応答を実装する
   g []. destroyed addon / hidden addon / not ready addon の扱いを統一する
   h []. sensitive UI の mode を制限する仕組みを実装する
   i []. UI introspection integration test を作成する

23 []. `get_player_context` を仕様通りに完成させる
   a []. empty input schema を実装する
   b []. output envelope を仕様通りに実装する
   c []. `characterName`, `homeWorld`, `currentWorld` を実装する
   d []. job 情報を実装する
   e []. territory / map 情報を実装する
   f []. coarse position を実装する
   g []. `inCombat`, `inDuty`, `isCrafting`, `isGathering`, `isMounted`, `isMoving` を実装する
   h []. `summaryText` 生成を実装する
   i []. unavailable / not logged in / disabled を実装する
   j []. schema compatibility test を実装する
   k []. snapshot freshness test を実装する

24 []. inventory / duty / addon 系 read-only tool を仕様通りに完成させる
   a []. `get_duty_context` の schema と handler を完成させる
   b []. `get_inventory_summary` の schema と handler を完成させる
   c []. `get_addon_list` の schema と handler を完成させる
   d []. `get_addon_tree` の schema と handler を完成させる
   e []. `get_addon_strings` の schema と handler を完成させる
   f []. payload サイズ制御を実装する
   g []. string table raw / decoded 出力を制御する
   h []. large addon tree の切り詰め戦略を実装する
   i []. all handlers の contract test を作成する

25 []. Host と Plugin の end-to-end baseline flow を完成させる
   a [x]. Plugin 起動時に BridgeServer が立ち上がることを実装する
   b [x]. Host 起動時に BridgeClient が接続することを実装する
   c [x]. MCP client から `tools/list` が見えることを確認する
   d [x]. disabled tool が list から除外されるか方針を決めて実装する
   e [x]. settings UI 変更で MCP surface が更新されることを実装する
   f []. Host 未接続時の Plugin 状態表示を実装する
   g [x]. Plugin 未起動時の Host エラー応答を実装する
   h [x]. end-to-end integration test を作成する

26 []. ExperimentalPrivateProfile の境界だけを実装する
   a []. baseline と experimental の profile model を明確に分離する
   b []. experimental capability は既定で全無効にする
   c []. experimental tab / mode 表示を baseline から分離する
   d []. experimental capability registry を baseline と別管理できる構造にする
   e []. action tool は実装しなくても placeholder / blocked 状態を表現できるようにする
   f []. baseline build から experimental capability を完全除外できる構造を整える
   g []. profile 混入防止 test を書く

27 []. hard deny list を end-to-end で強制する
   a []. blocked capability を settings UI で有効化不能にする
   b []. blocked addon を settings UI で有効化不能にする
   c []. blocked tool name / resource URI への要求を Host で拒否する
   d [x]. blocked request を audit へ記録する
   e []. social/chat/economy/combat automation 系が registry に入っていないことを検証する test を書く
   f []. hard deny list の docs と実装の整合 test を書く

28 []. CI workflow を完成させる
   a [x]. `.github/workflows/ci.yml` を作成する
   b [x]. checkout を実装する
   c [x]. `actions/setup-dotnet` を実装する
   d [x]. NuGet cache を有効化する
   e [x]. restore step を実装する
   f [x]. build step を実装する
   g [x]. `dotnet format --verify-no-changes` step を実装する
   h [x]. analyzer gate step を実装する
   i [x]. unit / integration / architecture tests step を実装する
   j [x]. coverage collection step を実装する
   k [x]. coverage threshold fail step を実装する
   l [x]. artifact upload step を実装する
   m [x]. check name を branch protection 用に安定化する
   n []. CI の失敗メッセージが十分読めることを確認する

29 []. ローカル開発用 build / test / quality スクリプトを整備する
   a [x]. restore 用 script を用意する
   b [x]. build 用 script を用意する
   c [x]. format 用 script を用意する
   d [x]. test 用 script を用意する
   e [x]. coverage 用 script を用意する
   f [x]. architecture test 用 script を用意する
   g [x]. all-in-one 検証 script を用意する
   h [x]. スクリプトの使い方を docs に記載する

30 []. ドキュメントと実装の整合を完成させる
   a []. README の現状説明を実装状態に合わせる
   b []. architecture docs と実コードの差分を埋める
   c []. tool-extension guideline と registry 実装の差分を埋める
   d []. hard deny list spec と実装の差分を埋める
   e []. settings model と UI 実装の差分を埋める
   f []. player context spec と actual schema の差分を埋める
   g []. use case matrix と実装済み capability を照合する
   h []. known limitations を README に記載する

31 []. 検証と最終品質ゲートを完了する
   a [x]. solution 全体 build を成功させる
   b [x]. formatter を clean にする
   c [x]. analyzer warning を 0 にする
   d []. Domain coverage 95/90 を達成する
   e []. Application coverage 95/90 を達成する
   f []. solution coverage 90/85 を達成する
   g []. end-to-end baseline flow を手動確認する
   h []. blocked capability が実際に公開されないことを手動確認する
   i []. UI introspection allowlist が settings から効くことを手動確認する
   j []. self-review 文書を実装版に更新する
   k []. release-ready 判定を行う

32 []. 将来拡張の準備を完了する
   a []. `get_gearset_summary` の追加余地を確認する
   b []. `get_recipe_context` の追加余地を確認する
   c []. `get_gathering_context` の追加余地を確認する
   d []. `get_market_ui_context` の追加余地を確認する
   e []. `Streamable HTTP` host 追加時の影響面を整理する
   f []. baseline / experimental build 分離の実装計画を確定する
   g []. next capability を追加するための template を `build/` か `docs/` に用意する
   h []. `Tool` 追加時に必要な test checklist をテンプレート化する
   i []. long-term maintenance 上のリスク一覧を更新する

33 []. ActionProfile の domain / registry モデルを実装する
   a []. `ObservationProfile` と `ActionProfile` の分離を domain model に反映する
   b []. action capability 用 category / tags を registry に追加する
   c []. action capability の default OFF を不変条件として表現する
   d []. action capability の tool-only 制約を registry で表現する
   e []. action capability の requiresConsent / auditRequired を metadata に追加する
   f []. action capability が baseline preset に混入しないことを test で保証する

34 []. ActionProfile の Application port と guard を実装する
   a []. `IPlayerMovementController` を定義する
   b []. `IInteractionController` を定義する
   c []. `ITargetResolver` を定義する
   d []. `IWorldStateReader` を定義する
   e []. `IAddonActionExecutor` を定義する
   f []. `IAddonActionPolicyEvaluator` を定義する
   g []. `ActionPreconditionEvaluator` を実装する
   h []. `ActionRateLimiter` を実装する
   i []. `ActionConflictGuard` を実装する
   j []. `ActionAuditPolicy` を実装する
   k []. guard 群の unit test を作成する

35 []. World Action use case と contract を実装する
   a []. `MoveToWorldPointUseCase` を実装する
   b []. `MoveToEntityUseCase` を実装する
   c []. `StopMovementUseCase` を実装する
   d []. `InteractWithTargetUseCase` を実装する
   e []. `InteractWithNearestUseCase` を実装する
   f []. action result envelope を contracts に定義する
   g []. world action request/response contract を定義する
   h []. accepted/completed/rejected/timed_out の状態遷移を固定する
   i []. wrong territory / blocked_by_ui / conflicting_action の失敗分類を実装する
   j []. world action use case test を作成する

36 []. UI Action use case と contract を実装する
   a []. `SendAddonEventUseCase` を実装する
   b []. `SelectAddonEntryUseCase` を実装する
   c []. `PressAddonButtonUseCase` を実装する
   d []. addon allowlist と node allowlist の評価を実装する
   e []. event kind allowlist の評価を実装する
   f []. generic text submit を hard deny にする
   g []. UI action request/response contract を定義する
   h []. UI action use case test を作成する

37 []. ActionProfile の Infrastructure adapter を実装する
   a []. `DalamudPlayerMovementController` を実装する
   b []. `DalamudInteractionController` を実装する
   c []. `DalamudTargetResolver` を実装する
   d []. `DalamudWorldStateReader` を実装する
   e []. `DalamudAddonActionExecutor` を実装する
   f []. movement / interaction / UI action の adapter test fixture を整備する
   g []. modal blocking UI / loading / not controllable の検出を実装する
   h []. action adapter integration test を作成する

38 []. ActionProfile の MCP tool surface と settings UI を実装する
   a []. `move_to_world_point` schema と handler を実装する
   b []. `move_to_entity` schema と handler を実装する
   c []. `stop_movement` schema と handler を実装する
   d []. `interact_with_target` schema と handler を実装する
   e []. `interact_with_nearest` schema と handler を実装する
   f []. `send_addon_event` schema と handler を実装する
   g []. `select_addon_entry` schema と handler を実装する
   h []. `press_addon_button` schema と handler を実装する
   i []. Action タブを settings UI に追加する
   j []. action capability の warning / consent UI を実装する
   k []. action capability が resources に出ないことを test で保証する

39 []. ActionProfile の end-to-end と build 分離を完成させる
   a []. action capability の audit を end-to-end で確認する
   b []. action capability の rate limit / conflict guard を end-to-end で確認する
   c []. blocked action request の拒否と audit を end-to-end で確認する
   d []. baseline host surface に action tool が出ない構成を実装する
   e []. experimental host surface にのみ action tool が出る構成を実装する
   f []. baseline build と experimental build の分離計画を実装に落とす
   g []. action profile の manual smoke test protocol を作成する
