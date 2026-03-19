# Hard Deny List Spec

## 1. 目的

この文書は、`DalamudMCP` で「設定 UI からも有効化できない対象」を明文化する。

allowlist や profile だけでは不十分な領域を、設計上の hard deny list として固定する。

## 2. 原則

hard deny list に入った対象は:

- baseline で有効化不可
- experimental でも原則有効化不可
- 設定 UI から選択不可
- MCP tool / resource として公開不可
- 内部 adapter 実装の対象外として扱ってよい

## 3. deny の軸

### A. 自動操作に直結するもの

- 自移動
- 自動ターゲット変更
- 自動スキル使用
- 自動インタラクト
- 自動クラフト
- 自動採集
- 自動ダイアログ送り
- 自動戦利品ロット

### B. 高感度コミュニケーション

- tell の本文
- free company chat の本文
- linkshell / cross-world linkshell の本文
- novice network の本文
- party chat の本文
- direct message 的 UI の本文

### C. 他者追跡 / 恒久識別

- 他プレイヤーの恒久識別子
- account を推測可能な識別情報
- 他者の location history の蓄積
- social graph の永続保存

### D. 経済自動化

- 自動購入
- 自動出品
- 自動価格更新
- 自動 retainer 操作

### E. 戦闘最適化 telemetry

- GCD 詳細タイミング
- cooldown の高頻度完全ダンプ
- action queue / weaving 状態
- 敵味方の高頻度詳細戦闘 state

### F. 露骨に高感度な UI

- chat log full text
- tell history
- support / GM 通知本文
- account / service 情報画面
- payment / optional item 購入関連 UI

## 4. hard deny の具体例

### tools

- `move_to_position`
- `target_actor`
- `click_visible_button`
- `auto_select_menu_entry`
- `send_chat_message`
- `buy_market_item`
- `craft_recipe`

### resources

- `ffxiv://chat/*`
- `ffxiv://social/tells/*`
- `ffxiv://social/friends/history`
- `ffxiv://combat/raw/*`

### addons

カテゴリ deny として扱う候補:

- Chat
- Tell
- Social private messaging
- GM / support
- Cash shop / payment

## 5. deny の表現

hard deny は settings ではなく registry 側で持つ。

つまり:

- `CapabilityDefinition` に `Denied = true`
- `AddonMetadata` に `Sensitivity = blocked`

のように、機能定義の段階でブロックされる。

## 6. settings UI の扱い

- blocked 項目は一覧に表示しても有効化できない
- 必要なら「存在は見えるが disabled」の状態にする
- 理由文を出す

例:

- `Blocked: chat message content is not exposable`
- `Blocked: gameplay automation is outside project scope`

## 7. audit の扱い

blocked 項目へのアクセス要求は監査対象にする。

記録内容:

- timestamp
- requested capability
- source
- rejection reason

## 8. 例外運用

原則として例外は作らない。

どうしても必要なら:

- baseline ではなく別 product
- 別 assembly
- 別 package
- 別 repository

まで分ける前提にする。

## 9. 結論

`DalamudMCP` の安全性は allowlist より hard deny list に依存する。

特に social、automation、economy automation、combat telemetry は最初から境界外に置くべき。
