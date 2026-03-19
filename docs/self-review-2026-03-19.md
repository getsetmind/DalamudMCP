# Self Review 2026-03-19

## 1. 対象

このセルフレビューは、2026-03-19 時点の `DalamudMCP` 設計文書一式に対して行う。

## 2. 結論

全体として、`DalamudMCP` は「FFXIV 用 read-only MCP context bridge」としては破綻しにくい設計になっている。

ただし、次の 3 つは将来的な主要リスク。

1. social / chat 系の高感度データ境界
2. 特殊コンテンツ固有 UI の増加による addon 保守負荷
3. experimental operation profile が baseline を侵食するリスク

## 3. 良い点

### 3.1 責務分離

- Plugin を thin にする方針
- Host を MCP transport に閉じる方針
- Domain / Application を中心に置く方針

これは長期保守で有効。

### 3.2 Tool 拡張性

- `CapabilityDefinition`
- `UseCase`
- `ToolHandler`
- 設定 UI metadata

のセット追加に整理したので、単発追加では崩れにくい。

### 3.3 FF14 向け適合性

- player context
- duty context
- inventory summary
- addon tree / string table

といった read-only needs に強い。

### 3.4 多クライアント対応の見通し

- local `stdio`
- 将来 `Streamable HTTP`

で transport を分けているため、MCP ecosystem 側の拡張にも追従しやすい。

## 4. 懸念点

### 4.1 Social データ

`UI introspection` を広げすぎると、chat、tell、friend、FC、event recruitment のような高感度領域へ簡単に踏み込める。

評価:
高リスク。

必要対策:

- hard deny list
- profile 分離
- 高感度 category の別 consent

### 4.2 特殊コンテンツ増加

FFXIV は拡張とパッチで独自 UI を持つコンテンツが増える。addon 名と UI 構造の差異を plugin 側が吸収し続けるのは保守負荷が高い。

評価:
中リスク。

必要対策:

- generic tree reader を基本にする
- feature specific reader を最小化する
- addon allowlist metadata を外だししやすくする

### 4.3 Experimental profile

`ExperimentalPrivateProfile` を導入すると、設計上は分離していても運用で境界が曖昧になりやすい。

評価:
高リスク。

必要対策:

- assembly か package を分ける案を残す
- baseline build と experimental build を分ける
- settings UI でも別タブではなく別モードにする

### 4.4 Tool 定義の散在

将来 tool 数が増えると、schema、registry、settings、tests の更新漏れが起こる。

評価:
中リスク。

必要対策:

- tool manifest 的な registry source を持つ
- test で `CapabilityDefinition` と handler の整合を検査する

## 5. FF14 ユースケース網羅の観点

### 十分対応できる

- 基本プレイ
- inventory
- duty / map / zone
- crafting / gathering の read-only 一部
- UI 説明 / UI 解析

### 設計追加で対応可能

- gearset
- recipe context
- gathering context
- market UI context
- special content specific UI

### 対象外で正しい

- 自動移動
- 自動ターゲット
- 自動クラフト
- 自動購入
- 戦闘最適化 telemetry

## 6. 将来的に破綻しにくいか

### 判定

`BaselineProfile` に限れば、かなり破綻しにくい。

理由:

- read-only に限定
- capability と settings を分離
- MCP と Dalamud を分離
- tests / CI / architecture gate を前提にしている

### ただし条件付き

次の条件が守られる必要がある。

1. baseline と experimental を混ぜない
2. social / chat 系を既定対象にしない
3. `Tool` 追加時のテンプレートを守る
4. generic reader を優先し、feature-specific reader を増やしすぎない

## 7. 推奨追加事項

将来の破綻防止のため、まだ文書化しておくとよいもの:

1. hard deny list spec
2. capability registry schema
3. addon metadata schema
4. baseline / experimental build separation plan

## 8. 最終判断

現設計は、FFXIV の read-only MCP bridge としては十分に筋が良い。

最大の破綻要因は技術不足ではなく、境界の緩みである。特に social data と experimental actions が baseline へ滲むと壊れやすい。

逆にそこを守る限り、将来の tool 追加や UI introspection の拡張には耐えやすい。
