# Minimo検証仕様（Verified）

## 資料・採用範囲

[ゴクラキズム: ミニモ](https://gokurakism.com/minimo/)
（3人用の公開完全規則、参照日: 2026-08-15）を候補元索引から直接取得し、同variantを採用する。

| 項目 | 完全規則 | Runtime・判断 |
|---|---|---|
| カード・資産 | spade/heartの2～6、計10枚。各10chip、pot | 同じ10枚、既定`starting_chips=10`、`pot`。一致 |
| ante・deal | 各自1chipをpotへ払い、各3枚、残り1枚は伏せて不使用 | `StartRound()`で支払後に3枚ずつ配り、1枚を使用しない。一致 |
| double | dealer右だけがplay前に宣言可 | `double` phaseの手番をdealer右に固定。一致 |
| trick | dealer左lead、no-trump、must-follow、6高 | `LegalActions()`だけがfollowを制約し、通常rankで勝者判定。一致 |
| 2-1-0 | 1trickが一人だけならpot獲得。double時は他2人が追加1chip | `ScoreRound()`が同じ支払とpot移動。一致 |
| 1-1-1 | pot carry。double時は宣言者が追加1chip | 同じ。一致 |
| 3-0-0 | sweep者が追加1chip。double時は宣言者も追加（同一なら計2） | 残chipを下限0として順に`Pay`。同じ。一致 |
| 終了 | deal終了時に0chip者がいれば終了、最多chip勝ち | `chips.Contains(0)`と`Result()`が一致 |
| 観測・乱数 | 相手手札と不使用札は非公開 | 不使用札を状態から除外し、相手手札をView/CPUへ出さない。注入乱数だけを使用。一致 |

`double`の意思表示だけを1 Actionとして逐次入力するため、紙上の選択肢は失われない。
`SeventhRuleAuditTests`はseed 701～705の完走に加え、seed 710～759でdouble時の2-1-0、
1-1-1、3-0-0を全て実測し、期待chip/potと一致させる。seed 820では相手2手札を交換しても
View・合法手・CPU選択が同一であることを確認する。未解決差分はなく`Verified`とする。
