# Trick of the Dead検証仕様（Verified）

## 資料・採用範囲

[ゴクラキズム: Trick of the Dead](https://gokurakism.com/totd/)
（3人用の公開完全規則、参照日: 2026-08-15）を候補元索引から直接取得した。資料は使用する
3スートを特定しないため、同型なdiamond/heart/clubを採用する。

| 項目 | 完全規則 | Runtime・判断 |
|---|---|---|
| カード・deal | 3スートの3～9・K、24枚。各7枚、余り3枚は伏せて不使用 | spadeを除く同じ24枚から各7枚。一致 |
| 前半 | 残り1枚まで6trick。メイフォロー、スート無関係の高rank、同rank先出し、1点 | `first_half`は全合法札を列挙し、2026-08-15補正後は全3枚をrankだけで安定比較。一致 |
| Zombie選択 | 低rankを出した順に場の3枚から1枚を選び、以後確認不可 | `zombie_pick`を低rank・同rank先出し順に逐次化し、選択後はcountだけ表示。一致 |
| 後半 | 残り1枚＋Zombie6枚。must-follow、K固定trump、複数Kは先出し、各2点 | `second_half`の合法手、Kを独立trump扱いする勝者判定、7trickが一致 |
| 勝敗 | raw合計の1の位が最大 | `Result()`が`points % 10`の最大をwinnerにする。一致 |
| 観測・乱数 | 手札、伏せた余り、回収済Zombieは非公開 | 相手手札とZombie内容をView/CPUへ出さず、注入乱数だけを使用。一致 |

Zombie選択を低rank順の3 Actionへ分け、同rankは公開play順で決める。選べる場札はすべて
残るため選択肢を失わない。修正前は前半勝者をlead suitへ誤って限定していたため、スートを
無視するrank比較へ根治した。`SeventhRuleAuditTests`はseed 760～799でoff-suit高rank勝利、
低rank順Zombie選択、勝者leadを固定し、seed 821で二相手手札の観測同値を確認する。
未解決差分はなく`Verified`とする。
