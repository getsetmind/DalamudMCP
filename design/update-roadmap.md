# DalamudMCP 更新方針

更新日：2026-08-09

状態：方針に基づく実装と自動テストを完了。実装境界は [`capability-kernel.md`](./capability-kernel.md) を参照

## 結論

DalamudMCP は、用途を限定した便利ツールだけでなく、利用者が許可した範囲で AI エージェントがゲーム内能力を組み合わせられる基盤を目指す。

この目的に照らし、次の三機能は採用前提で設計する。

- 汎用的なアクション実行
- 任意の Excel Sheet を対象にした検索と行取得
- Dalamud プラグインの調査、IPC 呼び出し、ライフサイクル管理

危険な操作を実装から除くと、許可した利用者に対しても能力を提供できない。

一方、すべてを一個の無制限な反射呼び出しへ集約すると、エージェントが能力と副作用を事前に判断できず、権限も監査も粗くなる。

そこで、汎用的なプリミティブを型付きのツールとして公開し、その利用可否を権限ポリシーで決める。

## プラグインの責務

このプラグインの責務は、あらかじめ開発者が想定した少数の手順だけを自動化することではない。

ゲーム状態の観測、静的ゲームデータの照会、ゲーム操作、プラグイン連携を組み合わせ、利用者が許可した範囲で新しい手順をエージェントが構成できるようにすることである。

この責務は二層の API で実現する。

- **能力層**：任意の Action ID、任意の Excel Sheet、任意の許可済みプラグインを扱える汎用ツールを提供する。
- **手順層**：テレポート、対象選択、クエスト取得など、頻出処理を扱いやすい個別ツールとして提供する。

個別ツールは汎用ツールを置き換えない。

個別ツールは入力を絞り、結果を意味のある形に整え、モデルが頻出操作を安定して実行するための手順である。

## 調査した実装

### 指定された fork

[`denghaoxuan991876906/DalamudMCP`](https://github.com/denghaoxuan991876906/DalamudMCP) の API 15 対応、UI 多言語化、ChatLog、IPC、プラグイン再読み込み、slash command、data relay は、現在の作業ブランチにすでに取り込まれている。

したがって、この fork を再度取り込む作業は不要である。

### MCPforDalamud

[`denghaoxuan991876906/MCPforDalamud`](https://github.com/denghaoxuan991876906/MCPforDalamud) は、汎用 `execute_action`、Excel Sheet の列挙と反射ベース検索、広いゲーム状態取得を実装している。

これらの機能範囲は DalamudMCP にも必要である。

ただし、中国向け SDK、独自 MCP 処理、直接的な FFXIVClientStructs 呼び出しに依存しているため、コードをそのまま移植せず、ツール契約と利用場面を取り込む。

### MelkyWay の MCP 群

[`MelkyWay/mcp-ffxiv-dalamud`](https://github.com/MelkyWay/mcp-ffxiv-dalamud) は、キャッシュ更新、バージョン検出、型情報の遅延取得を備えた Dalamud API ドキュメントサーバーである。

[`MelkyWay/mcp-ffxiv-clientstructs`](https://github.com/MelkyWay/mcp-ffxiv-clientstructs) は、FFXIVClientStructs の検索索引を Git SHA と parser version で無効化する。

[`MelkyWay/mcp-ffxiv-lumina`](https://github.com/MelkyWay/mcp-ffxiv-lumina) は、任意 Sheet の検索、ローカライズ、ページング、結果上限、構造化エラー、ゲームバージョン不一致の検出を実装している。

DalamudMCP には、これらの更新検出、検索上限、カーソル、構造化エラーを取り込む。

### そのほかの比較対象

[`FranFkntastic/DalamudAgentBridge`](https://github.com/FranFkntastic/DalamudAgentBridge) からは、能力マニフェスト、カーソル付きログ、待機ツール、操作対象の revision 検証、スクリーンショットのプライバシー設計を参考にする。

[`drptbl/universalis-mcp-server`](https://github.com/drptbl/universalis-mcp-server) からは、tool annotations、`outputSchema`、ページング、レート制限、出力形式の選択を参考にする。

## 採用した構成

各ライブラリの責務は次のように分けた。

| 要素 | 現在の責務 |
| --- | --- |
| `Manifold` | Operation 属性、引数記述、CLI、操作ディスパッチ |
| `DalamudMCP.Protocol` | プラグインと CLI プロセス間の契約 |
| `ModelContextProtocol` | MCP メッセージ、stdio サーバー、Streamable HTTP の部品 |
| `DalamudMCP.Cli` | 内部 Operation を MCP tool へ変換するアダプター |

パッケージ参照は `Manifold` 1.0.0、`Manifold.Cli` 1.0.0、`ModelContextProtocol` 2.1.0 である。

HTTP 実装は公式 ASP.NET Core transport を使い、2025 系の initialize と 2026-07-28 の `server/discover` を SDK に交渉させる。

Operation のプロトコル記述子は、副作用、必要権限、dry-run、thread 要件、`outputSchema`、MCP annotations の変換元を持つ。

権限は permission scope ごとの deny、confirm once、allow と、対象別の制約で管理する。

## MCP と Manifold の更新方針

### 判断

MCP の更新を理由に Manifold を置き換える必要はない。

MCP の wire protocol と HTTP transport を担当しているのは `ModelContextProtocol` であり、Manifold ではないからである。

`ModelContextProtocol` を 2.1.0 へ更新し、公式の ASP.NET Core 統合へ HTTP 実装を移行した。

公式 C# SDK 2.0.0 は MCP 2026-07-28 に対応し、旧プロトコルとの下位互換接続を備える。

2.1.0 は transport のフォールバック改善と `subscriptions/listen` のサーバー API を追加している。

固定した `CurrentProtocolVersion` は削除し、SDK にバージョン交渉と標準ヘッダー検証を担当させた。

### Manifold に追加したい情報

Manifold には MCP のバージョン番号ではなく、transport に依存しない操作メタデータが不足している。

各 Operation から次の情報を取得できるようにする。

- **effect**：`read`、`write`、`destructive` のいずれか。
- **permissionScope**：実行に必要な権限名。
- **idempotent**：同じ入力を繰り返してよいか。
- **supportsDryRun**：実行せず検証結果だけを返せるか。
- **requiresFrameworkThread**：Dalamud の framework thread が必要か。
- **resultContract**：成功、拒否、実行失敗を区別する結果契約。

MCP 固有の `ToolAnnotations`、`outputSchema`、image content への変換は `DalamudMCP.Cli` のアダプターで付与する。

汎用メタデータを Manifold 本体へ追加できない場合は、まず `DalamudMCP.Protocol` の記述子を拡張する。

Manifold の置き換えや fork は、次のいずれかが確認された時点で判断する。

- 必要なメタデータを生成処理から取得できない。
- Operation 契約の拡張が既存の公開 API によって継続的に妨げられる。
- 上流で必要な修正を保守できず、DalamudMCP 側の互換コードが増え続ける。

現時点の推奨は、Manifold 1.0.0 を維持したまま MCP SDK と内部記述子を先に更新することである。

## 汎用アクション実行

### 必要性

固定された Action ID だけを実行できる設計では、パッチやジョブによって増えるアクションを利用できず、エージェントの手順構成能力が開発者の事前実装に制限される。

したがって、任意の許可済み Action ID を実行できる `game_action_execute` を追加する。

既存の `use_duty_action` や `teleport_to_aetheryte` は、意味の明確な手順層として残す。

### ツール契約

実行前の解決と実行を分け、モデルが Action ID、対象、実行可能状態を確認できるようにする。

```text
game_action_resolve
  action_type
  action_id | action_name
  target_object_id?

game_action_execute
  action_type
  action_id | action_name
  target_object_id?
  expected_revision?
  dry_run?
```

`action_type` は SDK が扱える列挙値に限定し、文字列から任意の CLR 型やメソッドを指定させない。

名前指定は Excel の Action Sheet から ID へ解決し、曖昧な一致では候補を返して実行しない。

対象 ID は直前の ObjectTable snapshot と照合し、`expected_revision` が古い場合は実行を拒否する。

Dalamud または FFXIVClientStructs の呼び出しは framework thread 上で行う。

結果は少なくとも `resolvedAction`、`accepted`、`executable`、`reason`、`target` を返す。

`UseAction` の戻り値は呼び出し受付の結果であり、ゲーム内効果の完了保証として扱わない。

### 権限

固定 Action ID の許可リストを唯一の安全策にはしない。

権限ポリシーは、利用者が次の範囲を選べるようにする。

- `game.action.execute` を拒否する。
- Action type、Action ID、ジョブ、コンテンツ種別ごとの条件に一致する場合だけ許可する。
- 確認付きで任意の Action ID を許可する。
- 任意の Action ID を確認なしで許可する。

これにより、初期設定は保守的にしながら、利用者が完全な操作権限をエージェントへ渡せる。

## 汎用 Excel 検索

### 必要性

用途別の型付きツールだけでは、未実装の Sheet やパッチで追加されたデータを検索できない。

任意 Sheet を探索できる API は、エージェントが既知のツールを超えて調査するための基盤になる。

### ツール契約

```text
game_data_list_sheets
  query?
  cursor?
  limit?

game_data_describe_sheet
  sheet
  language?

game_data_get_row
  sheet
  row_id
  fields?
  language?

game_data_search
  sheet
  query?
  fields?
  where?
  language?
  cursor?
  limit?
```

反射は、`Lumina.Excel.Sheets` の型発見、`GetExcelSheet<T>` の構築、公開プロパティの射影に利用する。

ただし、取得したオブジェクトを再帰的にそのまま JSON 化しない。

`RowRef` は既定で参照先を展開せず、Sheet 名と Row ID の参照として返す。

バイト列、ポインター、delegate、循環参照を含む値は出力対象から除外する。

利用可能なら `RawExcelSheet` と `RawRow` を併用し、生成済み型が存在しない Sheet も扱えるようにする。

型付き反射と raw reader は競合する方式ではない。

前者は名前付きフィールドを提供し、後者は未知の Sheet と列レイアウトへのフォールバックを提供する。

検索には件数上限、カーソル、実行時間上限、キャンセルを設ける。

`limit` の上限は初期値 100 とし、結果の切り捨て時には `nextCursor` と `truncated` を返す。

反射情報のキャッシュキーにはゲームバージョン、Dalamud API level、Lumina assembly version を含める。

## 生のプラグイン管理

### 必要性

現在の再読み込みと IPC 呼び出しだけでは、エージェントが連携先の有無、状態、能力、停止理由を調べて回復するところまで到達できない。

プラグイン管理も、利用者が権限を与えた範囲で公開する。

### ツールの分割

「プラグイン管理」という一個の万能ツールにはまとめず、副作用と必要権限が異なる単位へ分ける。

- `plugin_list`：インストール済みプラグイン、状態、バージョンを列挙する。
- `plugin_describe`：対象プラグインの状態、依存関係、公開 IPC、直近エラーを取得する。
- `plugin_lifecycle`：`load`、`unload`、`enable`、`disable`、`reload` を実行する。
- `plugin_package`：`install`、`update`、`uninstall` を実行する。
- `plugin_ipc_invoke`：許可された callgate を型付き JSON 引数で呼び出す。
- `plugin_data_subscribe`：許可されたイベントを購読する。

Dalamud の公開 API で実装できる処理は公開 API を使う。

内部 API への反射が必要な処理は、バージョン判定を持つ `IPluginManagerAdapter` の内部へ隔離する。

公開 MCP 契約に CLR 型名、`MethodInfo`、任意の private method 名を露出させない。

任意 private method の実行を公開すると、`plugin.lifecycle` と `plugin.package` に分けた権限を迂回できるためである。

自分自身の unload や更新は、同じプラグインプロセスだけでは完了結果を返せない。

この処理まで自動化する場合は、CLI 側に supervisor を置き、プラグイン停止後も処理と監査を継続させる。

### 権限

プラグイン権限は操作種別と対象プラグインの両方で判定する。

```text
plugin.inspect
plugin.ipc.invoke
plugin.lifecycle
plugin.package.install
plugin.package.update
plugin.package.uninstall
plugin.self.manage
```

各権限はプラグイン InternalName の許可集合または wildcard を持てるようにする。

`plugin.package.*` には配布元、manifest、hash、対象バージョンを監査記録へ残す。

## 権限モデル

現在の `EnableActionOperations` と `EnableUnsafeOperations` は、異なる危険度の能力を一括で開閉するため粗すぎる。

各 capability のポリシーを次の三状態で保持する。

- **deny**：tool 一覧から除外し、直接呼び出されても拒否する。
- **confirm**：引数と副作用を提示し、一回限りの承認後に実行する。
- **allow**：追加確認なしで実行する。

ポリシーには対象 ID、対象プラグイン、Action type、最大件数、時間当たり呼び出し数などの条件を追加できるようにする。

変更系 capability の初期値は `deny` とする。

利用者が wildcard と `allow` を選べば、エージェントは実装済み能力を制限なしで組み合わせられる。

MCP の tool annotations はモデルへ副作用を伝えるヒントであり、認可判定の代わりには使わない。

認可はプラグイン側の dispatcher で毎回行い、CLI やクライアントから渡された自己申告を信用しない。

すべての変更操作について、時刻、tool、引数の要約、権限判定、結果、所要時間をローカル監査ログへ残す。

## MCP tool 契約

各 tool は次の情報を持つ。

- `title`
- `description`
- `inputSchema`
- `outputSchema`
- `annotations.readOnlyHint`
- `annotations.destructiveHint`
- `annotations.idempotentHint`
- `annotations.openWorldHint`
- DalamudMCP 固有の `permissionScope`

入力 schema には `additionalProperties: false`、文字列長、数値範囲、列挙値、配列上限を設定する。

Operation のドメイン結果が失敗を表す場合は、MCP の `CallToolResult.isError` も `true` にする。

成功と失敗の双方で機械処理用の `structuredContent` を返し、人間向けテキストだけに情報を閉じ込めない。

スクリーンショットは永続 BMP のファイルパスではなく、既定では PNG の image content として返す。

ファイル保存は別権限とし、保存した場合は TTL と削除処理を持たせる。

## transport とローカル境界

Streamable HTTP は公式 C# SDK の ASP.NET Core transport を利用する。

HTTP は引き続き `127.0.0.1` のみに bind し、Origin allowlist と bearer token を追加する。

named pipe には `PipeOptions.CurrentUserOnly` と最大 frame size を設定する。

不正な長さ、未知の message kind、timeout、キャンセルを明示的なプロトコルエラーとして処理する。

## イベントと観測 API

エージェントが操作結果を確認するには、操作 API だけでなく差分観測が必要である。

次の API を追加する。

- `events_query`：カーソル以降のイベントを取得する。
- `events_wait`：条件に一致するイベントまたは timeout まで待機する。
- `events_configure`：イベント種別、対象、バッファ上限を設定する。

ChatLog も時刻だけでなく単調増加 cursor、`nextCursor`、`droppedCount` を返す。

対象変更、territory 変更、condition 変更、party 変更、inventory 変更、cast、action 受付結果を初期イベント候補とする。

## 実装結果

### PR 1：文書と基準点の整理（完了）

- README と AGENTS.md のプロジェクト一覧を実際の solution に合わせる。
- `getsetmind/main` から新しい作業ブランチを作る。
- tool 一覧を実装から生成し、README の手動一覧との差分をなくす。

### PR 2：MCP SDK と transport（完了）

- `ModelContextProtocol` 2.1 系へ更新する。
- HTTP を公式 ASP.NET Core transport へ置き換える。
- 2026-07-28 と旧クライアントの互換テストを追加する。
- Origin、token、named pipe の境界を強化する。

### PR 3：Operation metadata と権限（完了）

- effect、permission scope、result contract を記述子へ追加する。
- 二つの bool 設定を capability policy へ移行する。
- tool annotations、`outputSchema`、`isError` を実装する。
- 監査ログを追加する。

### PR 4：汎用アクション実行（完了）

- `game_action_resolve` と `game_action_execute` を追加する。
- Action type、対象 revision、framework thread、実行結果を検証する。
- allowlist、confirm、unrestricted の各権限テストを追加する。

### PR 5：汎用 Excel 検索（完了）

- Sheet 列挙、schema 取得、行取得、検索を追加する。
- 反射 projection と raw reader のフォールバックを実装する。
- ページング、上限、循環参照、RowRef、バージョン別キャッシュを検証する。

### PR 6：プラグイン管理（完了）

- inspect、lifecycle、package、IPC の権限を分ける。
- 公開 API と反射 adapter の境界を作る。
- 対象プラグイン制約、self-management、監査を検証する。

### PR 7：イベントと結果確認（完了）

- カーソル付きイベントリングを追加する。
- `events_wait` と ChatLog cursor を追加する。
- アクション実行前後の状態を関連付ける。

### PR 8：観測機能の拡充（完了）

- target、party、condition、inventory items、equipment、currencies を追加する。
- territory 名と duty 名の placeholder を Excel データで解決する。
- screenshot を image content として返す。

## 受け入れ条件

- 利用者が全 capability を `allow` に設定した場合、実装済みの読み取り、操作、Excel 照会、プラグイン連携をエージェントが組み合わせられる。
- `deny` の capability は tool 一覧に現れず、直接呼び出しても plugin dispatcher が拒否する。
- `confirm` の capability は承認対象の引数と副作用を表示し、承認範囲を超えた再利用を拒否する。
- すべての tool が入力 schema と出力 schema を持つ。
- ドメイン失敗が MCP 上の成功として返らない。
- 旧 MCP クライアントとの互換範囲を自動テストで固定する。
- Excel の大規模検索が件数上限、時間上限、キャンセルを越えて実行されない。
- プラグイン操作が対象プラグイン制約を迂回できない。
- 変更系操作を監査ログから追跡できる。

## 決定事項と残る境界

- `confirm` は Dalamud UI の一回限り承認として実装し、MCP client の elicitation 対応へ依存させない。
- package 操作は Dalamud に構成済みの repository だけを対象とし、取得元 URL と配置後 DLL の SHA-256 を証跡にする。これは配布者署名の検証を意味しない。
- `game_action_execute` は SDK の `ActionType` 列挙値を受け付け、permission policy で type と ID を狭める。
- 生成済み型がない raw Excel row へ、根拠のない列名を付けない。名前付きフィールドが必要な Sheet は生成済み Lumina 型を使う。
- MCP 2026-07-28 は公式 SDK の handshake-free 経路へ任せ、旧 initialize 経路と両方を統合テストする。

## 参照資料

- [MCP 2026-07-28 specification](https://modelcontextprotocol.io/specification/2026-07-28)
- [MCP Streamable HTTP transport](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports)
- [MCP tools](https://modelcontextprotocol.io/specification/2026-07-28/server/tools)
- [MCP C# SDK 2.0.0](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.0.0)
- [MCP C# SDK 2.1.0](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.1.0)
