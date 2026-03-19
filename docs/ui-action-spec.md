# UI Action Spec

## 1. 目的

この文書は、`Atk` / addon に対する汎用 UI action capability を定義する。

## 2. 基本方針

- UI action は action profile に限定
- generic に「何でも送れる」設計は避ける
- addon allowlist と node allowlist の両方を持つ
- high sensitivity UI は hard deny

## 3. capability 一覧

### `send_addon_event`

対象 addon に対して predefined event を送る。

入力:

- addonName
- eventKind
- nodeId
- optional parameters

### `select_addon_entry`

一覧 UI に対して index または stable selector で項目選択を行う。

入力:

- addonName
- selector

### `press_addon_button`

ボタン系 node を押す。

入力:

- addonName
- nodeId または selector

## 4. generic event send を制限する理由

完全に汎用な event dispatch は、ダイアログ送りや hidden workflow 自動化に直結する。

したがって v1 では:

- addon allowlist
- node allowlist
- event kind allowlist

の 3 段階制御にする。

## 5. internal ports

- `IAddonActionExecutor`
- `IAddonStateReader`
- `IAddonActionPolicyEvaluator`

## 6. selector 戦略

v1 では selector を狭くする。

候補:

- exact node id
- exact row index
- exact button semantic id

自然言語 selector や fuzzy selector は採用しない。

## 7. precondition

- addon visible
- addon ready
- addon allowed
- node allowed
- event allowed
- no blocking modal mismatch

## 8. denied 対象

- chat / tell / support / payment 系 addon
- generic text submit
- arbitrary string input

## 9. 監査

最低でも次を残す。

- addonName
- node selector summary
- event kind
- precondition result
- dispatch result

## 10. 結論

UI action は「何でも押せる汎用リモコン」ではなく、「allowlist 化された安全な one-shot action」としてのみ扱う。
