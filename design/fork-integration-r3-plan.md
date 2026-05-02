# Fork 全機能取り込み設計案（R3基盤）

## 結論

fork の全機能は取り込む。  
ただし `plugin.data.*` はそのまま移植せず、**Cysharp の NuGet パッケージ `R3` を直接参照する R3 基盤 + poll互換**で再設計して統合する。

## ゴール

1. fork の機能差分（API15対応、UI多言語、ChatLog、IPC強化、reload、slash、safe invoke、data relay）を全部取り込む
2. 既存クライアント互換を壊さない（`plugin.data.poll` 維持）
3. docsノイズ（`.planning/`）は取り込まない
4. CIを安定通過させる

## 取り込み対象（機能）

1. Dalamud API 15 対応
2. UIローカライズ（`IUiLocalization`、`en/zh`、設定反映）
3. `chat.read`（ChatLogBufferService + operation）
4. IPCインフラ抽象化（Gateway/Subscriber/DI）
5. `plugin.reload`
6. `command.slash`
7. `plugin.ipc.safe_invoke`
8. `plugin.data.subscribe / poll / unsubscribe`（R3基盤で実装）

## R3ベース再設計（Data Relay）

### 1. NuGet参照

- `src/DalamudMCP.Plugin/DalamudMCP.Plugin.csproj` に `PackageReference Include="R3" Version="1.3.0"` を追加する
- Data Relay 実装では `using R3;` を使い、`System.Reactive` 互換ライブラリではなく Cysharp/R3 の型を直接使う
- R3 はイベント処理の本流に使い、バックプレッシャーと poll 互換バッファは `System.Threading.Channels.Channel<T>` に担当させる

### 2. 内部モデル

- `RelayStream`（1チャンネル単位）
- `R3.Subject<string>`: 受信イベント本流
- `Channel<string>`: poll互換バッファ（容量・DropOldest）
- `IDisposable`: R3購読解除ハンドル
- `ICallGateProvider`: IPC Provider登録情報

### 3. データフロー

1. plugin から IPC 受信
2. `R3.Subject<string>.OnNext(json)`
3. `Subject.Subscribe(...)` 側で `Channel.Writer.TryWrite(json)`
4. `poll` は Channel から drain（現行契約維持）

### 4. API互換

- `plugin.data.subscribe`: チャンネル作成 + IPC登録
- `plugin.data.poll`: 既存の戻り形式を維持
- `plugin.data.unsubscribe`: IPC解除 + R3 dispose + Channel完了

### 5. pollの契約

- `poll` は R3 の機能ではなく、MCPクライアント向けの互換APIとして残す
- `poll` は `Channel<string>` に溜まっているデータを非同期通知なしで取りに行く操作
- `max-items` が未指定なら、その時点で取り出せるデータを全件返す
- `max-items` が指定された場合は、最大件数だけ取り出し、残りは `Channel<string>` に残す
- `max-items` を超えた分を読み捨ててはいけない
- データがない既存チャンネルは `Success=true`、`Status=no_data`、`Items=[]` を返す
- 未購読または解除済みチャンネルは `Success=true`、`Status=channel_not_found`、`Items=[]` を返す
- 内部例外時だけ `Success=false`、`Status=poll_failed` を返す

### 6. 追加メリット

- 将来 `stream` API（SSE/WebSocket）を追加しやすい
- R3演算子でフィルタ/間引き/集約が簡単

## 実装PR分割（推奨）

1. PR-A: API15 + 基盤差分
   - csproj/SDK更新
   - IPC抽象、DI配線、既存テスト更新
2. PR-B: UI/ChatLog
   - localization 一式
   - `chat.read` と関連テスト
3. PR-C: 操作拡張
   - `plugin.reload`
   - `command.slash`
   - `safe invoke`
   - exposure policy更新 + テスト
4. PR-D: Data Relay(R3)
   - `IPluginDataRelayService` 再定義
   - `PluginDataRelayService` を R3 + Channel で実装
   - `subscribe/poll/unsubscribe` 実装
   - テスト一式（通常系、上限、再購読、dispose、並行アクセス）
5. PR-E: ドキュメント整理
   - `.planning/` は除外
   - 必要最小限を `README.md` または追跡対象の設計文書に統合

## 非機能要件

1. スレッド安全: `ConcurrentDictionary` + 明確な dispose順序
2. メモリ制御: チャンネル上限、DropOldest 明示
3. エラー方針: operation は `status` 返却を統一
4. セキュリティ: unsafe操作は exposure policy に明示

## 受け入れ条件

1. `build/restore.ps1` `build/build.ps1` `build/test.ps1` 全通過
2. 既存テスト + 新規テスト成功
3. `plugin.data.poll` の互換性維持（既存クライアント無改修で動作）
4. 不要ファイル（`.planning` 等）非混入
5. PR本文に「互換性」「危険操作」「移行手順」を明記
6. `plugin.data.poll --max-items N` で N 件だけ返し、残りを次回 poll で取得できる

## セルフレビュー

### 良い点

1. R3を抽象概念ではなく、`NuGet: R3` と `using R3;` を前提にした具体設計にできている
2. `poll` 互換を残すため、既存MCPクライアントの呼び出し契約を壊さない
3. R3にバックプレッシャーまで背負わせず、バッファ制御は `Channel<T>` に分けているため責務が明確
4. forkの大量docsをそのまま取り込まない方針なので、PRのノイズを抑えられる

### 懸念点

1. R3の導入範囲が `src/DalamudMCP.Plugin` に閉じている前提なので、将来Framework側へ広げるなら依存境界を再検討する必要がある
2. `Subject<string>` に流すデータがJSON文字列のままだと、型安全性は弱い。まず互換性を優先し、必要なら後続で typed payload を追加する
3. `poll` 用 `Channel<string>` と R3購読の二重構造になるため、dispose順序のテストが重要
4. `plugin.data.stream` を将来足す場合は、MCP transport 側の対応可否を別途確認する必要がある
5. forkの実装に近い「全件drainしてから `max-items` で切る」方式だと、返さなかった分が消えるため採用しない

### 修正すべき実装時ルール

1. `R3` の `PackageReference` は Data Relay を実装するPRに含める
2. `PluginDataRelayService` は `Subject<string>`、`Channel<string>`、IPC Provider の3つを必ず同じライフサイクルで破棄する
3. tests では `subscribe -> push -> poll -> unsubscribe -> poll` の順に、互換動作と破棄後動作を確認する
4. PR本文には「R3はイベント本流、Channelはpoll互換バッファ」と明記する
5. tests では `max-items` 超過分が次回 poll に残ることを確認する

## 補足

この方針で進めると、fork の全機能を取り込みつつ、Data Relay だけは将来拡張しやすい形にできる。
