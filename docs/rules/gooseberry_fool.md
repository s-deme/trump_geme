# Gooseberry Fool監査記録（Verified）

資料は作者の[David Parlett](https://www.parlettgames.uk/oricards/goosfool.html)（参照日: 2026-08-15）。3人、7〜A＋Joker、odd-card winner variantを対象にした。
| 項目 | 資料 | 実装・判断 |
|---|---|---|
| odd suit/color・Joker譲渡 | 原典固有規則 | `GooseberryFoolGame`、基本一致 |
| score | 各自のtrick＋右隣のtrick×2。3人のdeal得点は必ず相異なり、中間へ10点 | 全11 trick分布を列挙し相異性を確認。`FinishDeal()`と一致 |
| session | 1人または2人が100点到達時に終了。累計中間が勝者、2人同点なら同点でない1人が勝者 | `Result()`の中間値・untied third分岐と一致 |

Jokerの勝者指定を`goose` Action化し、相手手札は非公開とする。`NineteenthRuleAuditTests`は
全trick分布、累計中間、2人同点、固定seed 1902完走、観測同値を確認する。未解決差分はないため
`Verified`とする。
