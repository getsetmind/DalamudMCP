# Addon Metadata Schema

## 1. 目的

この文書は、UI introspection 対象 addon の metadata をどの粒度で持つかを定義する。

## 2. 役割

addon metadata は次に使う。

- settings UI 表示
- allowlist 候補
- deny 制御
- introspection mode 制御
- 説明文表示

## 3. 論理モデル

```text
AddonMetadata
├─ AddonName: string
├─ DisplayName: string
├─ Category: enum
├─ Sensitivity: enum
├─ DefaultEnabled: bool
├─ Denied: bool
├─ Recommended: bool
├─ Notes: string
├─ IntrospectionModes: string[]
└─ ProfileAvailability: string[]
```

## 4. category 候補

- `Character`
- `Inventory`
- `Crafting`
- `Gathering`
- `Duty`
- `Map`
- `Market`
- `Housing`
- `Social`
- `Chat`
- `System`
- `Experimental`

## 5. mode 候補

- `summary`
- `tree`
- `strings`
- `numbers`

## 6. 重要ルール

- `Denied = true` の addon は設定 UI で有効化不可
- `Sensitivity = blocked` と `Denied = true` は整合する
- `Recommended = true` でも `Sensitivity = high` にはしない
- `Chat` / `Social` は既定で deny か少なくとも default off

## 7. v1 運用

v1 は code-defined を推奨。

理由:

- addon 名 typo を減らせる
- enum と整合を取りやすい
- blocked / recommended の監査がしやすい

## 8. 将来

特殊コンテンツ UI が増えたら、metadata のみ data-driven に移す余地がある。

ただし deny ロジックはコード側に残す。

## 9. 結論

addon metadata は capability registry の補助ではなく、UI introspection 安全性の中核データとして扱う。
