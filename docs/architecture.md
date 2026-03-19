# Architecture

## 1. 問題設定

作りたいのは「MCP クライアントが FFXIV の現在文脈を参照できる仕組み」です。ただし、MCP 互換性だけを優先して Dalamud プラグイン内に何でも詰め込むと、責務が肥大化し、デバッグ性と安全性が崩れます。

本設計では、`Dalamud` プラグインをゲーム内観測点として扱い、MCP サーバー責務は別プロセスへ分離する前提を採用します。

## 2. 前提制約

### MCP 側

- MCP は JSON-RPC ベース
- 標準 transport は `stdio` と `Streamable HTTP`
- `stdio` では「クライアントが MCP サーバーを subprocess として起動する」
- `Streamable HTTP` では「サーバーは独立した process として複数接続を扱える」

このため、ゲームプロセス内でロードされる Dalamud プラグインは、MCP が想定する標準的なサーバー実行形態と噛み合いにくい。

### Dalamud 側

- 2026-03-19 時点の公式ドキュメントでは `14.x (API 14)` が current
- プラグイン DLL には `IDalamudPlugin` エントリポイントが 1 つ必要
- 同一 solution 内に他プロジェクトを置くこと自体は可能
- 公式 repo へ出す場合はオープンソース前提
- Plugin Restrictions により、自動化、戦闘介入、PvP 優位、過度な解析、他人の識別情報収集などは強く制限される

## 3. ゴール

- ローカル PC 上で、MCP クライアントから FFXIV の現在文脈を参照できる
- Dalamud ガイドラインに極力沿う
- トラブル時に「プラグイン側の問題」か「MCP 側の問題」か切り分けやすい
- 将来ツール数が増えても壊れにくい
- 将来、麻雀のような上位 automation を外部 client 側で組めるだけの汎用 observation/action primitive を持てる

## 4. 非ゴール

- 自動操作
- 戦闘補助
- パケット送信やサーバー仕様外操作
- 無人実行の周回やクラフト
- PvP で優位を与える情報提示
- 他人の恒久識別子の蓄積

## 5. 候補アーキテクチャ

### A. プラグイン内蔵 MCP サーバー

概要:
Dalamud プラグイン自身が MCP サーバー機能を持ち、`localhost HTTP` などでクライアント接続を受ける。

利点:

- プロセス数が少ない
- 実装対象が 1 つに見える

欠点:

- ゲームプロセスにネットワーク待受や MCP セッション管理を持ち込む
- クラッシュ時の切り分けが悪い
- `stdio` 型クライアントとの相性が悪い
- 再接続、複数 client、ログ、権限制御をプラグイン内で抱える

評価:
不採用。

### B. プラグイン + ローカル MCP ホスト分離

概要:
Dalamud プラグインはゲーム状態の取得とローカル IPC 提供のみ担当し、別プロセスの `DalamudMCP.Host` が MCP サーバーとして `stdio` または `Streamable HTTP` を提供する。

利点:

- MCP 標準 transport と整合しやすい
- ゲーム内コードを小さく保てる
- クラッシュ分離しやすい
- プラグインを read-only 観測エージェントに限定しやすい
- Claude Desktop などの `stdio` 前提クライアントに合わせやすい

欠点:

- プロセスが 2 つになる
- プラグイン内 IPC と MCP 側 transport の二層設計が必要

評価:
採用。

### C. 外部プロセスのみでゲーム情報取得

概要:
Dalamud を使わず、外部プロセスが直接ゲームクライアントや外部 API を読む。

利点:

- MCP としては単純

欠点:

- Dalamud サービス群を利用できない
- 設計が別物になる
- ゲーム内 UI や状態の正当な観測点として扱いにくい

評価:
今回の目標から外れるため不採用。

## 6. 採用アーキテクチャ

### 構成要素

1. `DalamudMCP.Plugin`
   - ゲーム内状態を取得する
   - 外部へ出してよい情報だけを正規化する
   - ローカル IPC endpoint を提供する
   - ユーザー同意、可視化、監査ログを担当する
   - tool / resource / addon allowlist の設定 UI を担当する

2. `DalamudMCP.Host`
   - MCP サーバー本体
   - `stdio` を第一候補として提供する
   - 必要なら将来 `Streamable HTTP` を追加する
   - プラグインに対して IPC で問い合わせる

3. MCP Client
   - Claude Desktop など
   - `DalamudMCP.Host` を起動または接続する

### capability profile

- `ObservationProfile`
  - read-only
  - tools + resources
- `ActionProfile`
  - side-effecting
  - tools only
  - private experimental

### 内部設計原則

- Clean Architecture を前提とする
- `Plugin` と `Host` は delivery mechanism に限定する
- `Domain` と `Application` を中心に置く
- architecture tests で依存方向を強制する

詳細は [clean-architecture.md](C:\Users\user\Documents\GitHub\DalamudMCP\docs\clean-architecture.md) を参照。

### データフロー

1. ゲーム内イベントまたは明示要求でプラグインが状態を収集
2. プラグインがサニタイズ済み snapshot を内部モデルへ反映
3. 外部ホストが IPC 経由で snapshot を要求
4. 外部ホストが MCP `tool` / `resource` として整形して返す

## 7. IPC 方針

候補:

- Windows Named Pipe
- localhost HTTP
- localhost WebSocket

推奨:
`Windows Named Pipe`

理由:

- 対象 OS が事実上 Windows 中心
- ローカル閉域で扱いやすい
- ファイアウォールやポート競合を避けやすい
- 「外部公開サーバーではない」ことを設計上明確にできる

非推奨:

- `localhost HTTP`
  - デバッグはしやすいが、待受ポート管理と誤設定リスクが増える
- `WebSocket`
  - 双方向性はあるが、この段階では過剰

## 8. 能力モデル

初期版は read-only のみ。

### 想定ツール群

- `get_player_context`
  - 自キャラの現在ジョブ、レベル、座標概略、ゾーン、コンテンツ参加状態
- `get_party_context`
  - 自パーティの概況
- `get_target_context`
  - 現在ターゲットの基本情報
- `get_inventory_summary`
  - 所持品の集約要約
- `get_duty_context`
  - 現在の duty / instance の文脈
- `get_ui_context`
  - 開いている主要 UI 状態の要約

### 除外するもの

- ボタン押下や選択肢選択の代理実行
- マクロ相当の自動操作
- 戦闘回しの提案に直結する高頻度詳細 telemetry
- 他者識別子の永続保存

## 9. 同意と安全性

### 基本方針

- デフォルト無効
- 初回起動時に明示 opt-in
- 接続中であることをゲーム内 UI で常時表示可能にする
- どの capability が有効かを一覧化する
- すべての外部要求を監査ログに残せる設計にする

### 制御単位

- capability 単位で ON/OFF
- tool 単位で公開制御
- resource 単位で公開制御
- addon 単位で UI 観測許可を制御
- 一時停止スイッチ
- セッション強制切断

### 設定 UI の原則

- allowlist はコード固定ではなくユーザー設定で持つ
- ただし deny list は設定で上書きできない
- `Recommended` プリセットを用意する
- baseline と experimental profile は UI 上でも分けて表示する

## 10. 更新モデル

プラグインは push 型で大量送信しない。基本は request/response とし、必要なら短寿命 cache を持つ。

理由:

- ゲームプロセス側の負荷を抑える
- 不要な高頻度ストリームを避ける
- 監査と再現性を高める

## 11. 配布モデル

初期段階では、公式 repo 前提で設計しない。

理由:

- MCP という性質上、審査観点が通常の QoL プラグインより厳しくなる可能性が高い
- まずはローカル開発と custom repository 前提で境界を固めるべき
- Dalamud 公式ドキュメントでも custom repository は可能だが支援は限定的

## 12. 実装前に確定すべき事項

- read-only 公開対象の厳密なデータ辞書
- capability ごとの deny list
- snapshot 更新頻度
- ログの保存先と保持期間
- host 側で `stdio` のみ対応するか、`Streamable HTTP` まで持つか
