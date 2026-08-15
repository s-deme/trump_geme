# Skat監査記録

資料は[ISPA Skat Order](https://www.ispaworld.info/images/ispa-world/canada/ISkO%20Revisions%202016%20Feb%201.pdf)（参照日: 2026-08-15）。3人32枚・auction・skat pickup/hand gameを対象にした。
| 項目 | 資料 | 実装・判断 |
|---|---|---|
| 10枚＋skat2、auction | official order | `SkatGame`、入口確認 |
| game value/Schneider/announcements | 詳細な公式表 | Runtimeは限定contract、未照合 |
秘密skatを選択Actionへ、CPU/Viewは非公開札を見ない。修正なし、seed 603完走・合法CPU試験。公式契約・得点全表未実装のためRuleSpecific維持。
