# Capability kernel の実装

更新日：2026-08-09

状態：実装済み

## 責務の分割

DalamudMCP は、許可されたゲーム内能力をエージェントが組み合わせるための基盤を提供する。

この目的のため、API を二層に分けている。

- **能力層**：Action、Excel、イベント、プラグイン管理などの汎用操作を公開する。
- **手順層**：テレポート、対象選択、Duty Action などの頻出手順を、入力を絞った個別ツールとして公開する。

能力層は手順層を置き換えない。

手順層はモデルが頻出操作を安定して扱うために残し、能力層は開発者が事前に想定していない手順を構成するために使う。

## MCP と Manifold

Manifold 1.0.0 は、Operation 属性、CLI 引数、操作ディスパッチを担当する。

MCP の wire protocol と Streamable HTTP transport は `ModelContextProtocol` 2.1.0 が担当する。

したがって、MCP の更新を理由に Manifold を fork または置換していない。

DalamudMCP の protocol descriptor には、次の transport 非依存メタデータを追加した。

- effect
- permission scope
- idempotent
- open world
- dry-run 対応
- framework thread 要件
- 入出力 schema

CLI 側は、このメタデータを MCP の `ToolAnnotations`、`outputSchema`、tool `_meta` へ変換する。

ドメイン結果の `success` または `succeeded` が `false` の場合、MCP の `isError` も `true` になる。

HTTP は公式 ASP.NET Core transport を使い、2025-03-26 の initialize 方式と 2026-07-28 の `server/discover` 方式を統合テストしている。

## Capability policy

設定画面では、permission scope ごとに次のアクセス方式を選べる。

- **deny**：tool 一覧から除外し、Operation ID を直接指定した呼び出しも拒否する。
- **confirm once**：正確な引数を画面へ表示し、一回限りの承認後に同じ要求だけを実行する。
- **allow**：追加確認なしで実行する。

変更系 capability の初期値は deny である。

従来の `EnableActionOperations` と `EnableUnsafeOperations` は、個別ポリシーが存在しない場合の互換設定として残している。

認可は CLI 側ではなく、ゲームプロセス内の dispatcher が呼び出しごとに実行する。

クライアントが送る permission scope や tool annotation は、認可判定に使わない。

### 一回限りの承認

confirm once は Operation ID、permission scope、引数全体の JSON から SHA-256 fingerprint を作る。

承認は fingerprint が一致する一回の再試行で消費されるため、対象 ID や引数を変えた要求へ流用できない。

承認待ちは最大 32 件、保持時間は 5 分である。

利用者はプラグイン設定画面で引数を確認し、許可または拒否できる。

### 制約とレート制限

各ポリシーには次の制約を指定できる。

- プラグイン InternalName
- Action type と Action ID
- ClassJob ID と territory ID
- Excel Sheet 名
- IPC callgate 名
- 対象 object ID
- 最大返却件数
- scope と対象ごとの毎分呼び出し数

制約集合が空の場合は、その項目を制限しない。

文字列制約には `*` を指定できる。

## 汎用 Action

`game_action_resolve` は Action ID または Action Sheet 上の完全一致名を解決し、実行可能状態と対象 revision を返す。

部分一致は候補として返すだけであり、曖昧な名前を自動実行しない。

`game_action_execute` は解決時の `expected_revision`、ClassJob、territory、対象 object ID を再検証してから `UseAction` を呼ぶ。

処理は framework thread 上で実行する。

`dry-run` は解決結果と現在の status code を返し、`UseAction` を呼ばない。

実行結果には correlation ID を付け、同じ ID で `action.accepted` または `action.rejected` イベントを発行する。

ここでの accepted は、ゲームが要求を受け付けたことだけを表す。

ゲーム内効果の完了は、イベントや型付き観測ツールで別に確認する必要がある。

## Excel 検索

次のツールが任意の Lumina Excel Sheet を扱う。

- `game_data_list_sheets`
- `game_data_describe_sheet`
- `game_data_get_row`
- `game_data_search`

生成済み row 型がある Sheet は、公開プロパティを反射して射影する。

生成済み型がない Sheet は `RawExcelSheet` へフォールバックする。

`RowRef` は参照先を再帰展開せず、Sheet 名と row ID を返す。

一回の検索には次の上限がある。

- 返却件数：100
- 走査件数：10,000
- 実行時間：500 ms
- 一つの row から射影するプロパティ：96
- enumerable の要素：32

`where` は `{field, operator, value}` の JSON だけを受け付ける。

演算子は `eq`、`ne`、`contains`、`startsWith`、`endsWith`、`gt`、`gte`、`lt`、`lte` に固定し、任意式や CLR メソッドを実行しない。

カーソルはゲームデータ版、Dalamud 版、Lumina 版、検索条件へ結び付ける。

ゲーム更新後の古いカーソルや、別の検索条件から得たカーソルは `invalid_cursor` になる。

## イベントと観測

ゲームイベントは単調増加 cursor を持つ bounded ring buffer に保存する。

- `events_query`：cursor 以降のイベントを返す。
- `events_wait`：一致するイベントまたは最大 30 秒の timeout まで待つ。
- `events_configure`：有効なイベント種別と最大 10,000 件の buffer capacity を設定する。

初期イベントは target、cast、territory、condition、party、inventory、Action 受付結果である。

ChatLog も cursor、`nextCursor`、`oldestCursor`、`droppedCount`、`truncated` を返す。

現在状態は target、party、condition、inventory items、equipment、currencies の型付きツールから取得できる。

player と duty の territory 名は Lumina の Excel データから解決する。

## スクリーンショット

`capture_game_screenshot` は BMP を公開せず、既定では PNG の MCP image content を返す。

画像は最大 1920 × 1080 に縮小し、named pipe の 16 MiB frame 上限内で扱う。

既定ではファイルを保存しない。

`save=true` は別 scope の `screenshot.save` を要求し、保存ファイルへ 60 秒から 86,400 秒の TTL を設定する。

期限切れファイルとサービス終了時の登録済みファイルは削除する。

## プラグイン管理

調査ツールは Dalamud の公開 `IExposedPlugin` 契約から、インストール状態、版、manifest、UI capability を返す。

Dalamud の公開契約が依存関係、公開 IPC 一覧、直近の load error を提供しないため、それらを推測して返さない。

lifecycle と package 操作は公開 API だけでは足りないため、Dalamud API 15 専用 adapter に内部 API 呼び出しを隔離した。

公開 MCP 引数には CLR 型名、private method 名、任意 URL を含めない。

install と update は、Dalamud が構成済み repository から取得した manifest だけを対象にする。

結果と監査ログには、取得元 URL と実際に配置された DLL の SHA-256 を残す。

通常の呼び出しでは DalamudMCP 自身の lifecycle と package 操作を拒否する。

自己 update と uninstall は外部 CLI supervisor だけが行える。

```powershell
dalamudmcp supervise self-package --action update --timeout-seconds 90
dalamudmcp supervise self-package --action uninstall --timeout-seconds 90
```

supervisor は別プロセスに残り、update 後の discovery record と operation catalog、または uninstall 後の停止を確認する。

この経路には独立した `plugin.self.manage` scope が必要である。

## ローカル通信

HTTP サーバーは `127.0.0.1` だけに bind する。

非 loopback の `Origin` を拒否し、必須の bearer token を固定時間比較で検証する。

プラグインが起動する子 CLI には 256-bit token を環境変数で渡し、コマンドラインへ含めない。

named pipe は `CurrentUserOnly` で作成し、request と response の最大 frame 長を 16 MiB に制限する。

## 監査ログ

変更操作と拒否された操作は `audit/operations.jsonl` に記録する。

記録項目には時刻、request ID、approval ID、Operation ID、permission scope、認可結果、引数の限定要約、結果、所要時間、package source、版、SHA-256 が含まれる。

bearer token と IPC 引数本文は記録しない。

## 意図的に残した境界

次の機能は、能力不足ではなく公開契約と安全境界を維持するために実装していない。

- 任意 CLR 型、private method、download URL を受け取る無制限な反射 executor
- 曖昧な Action 名の自動実行
- 再帰的な `RowRef` 展開
- supervisor を伴わない自己更新
- Dalamud の公開 API が提供しない依存関係、IPC 所有者、過去の load error の推測

比較調査と採用判断は [`update-roadmap.md`](./update-roadmap.md) に保存している。
