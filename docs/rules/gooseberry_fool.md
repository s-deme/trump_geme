# Gooseberry Fool監査記録

資料は作者の[David Parlett](https://www.parlettgames.uk/oricards/goosfool.html)（参照日: 2026-08-15）。3人、7〜A＋Joker、odd-card winner variantを対象にした。
| 項目 | 資料 | 実装・判断 |
|---|---|---|
| odd suit/color・Joker譲渡 | 原典固有規則 | `GooseberryFoolGame`、基本一致 |
| score中央値処理 | 原典の全tie規則 | session tie差分を未照合 |
Jokerの勝者指定をAction化し、相手手札は非公開。修正なし、seed 604完走・合法CPU試験。全tie/累積精算未照合のためRuleSpecific維持。
