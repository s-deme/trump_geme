# The Trick監査記録

状態は`Verified`。資料は[ゴクラキズムの完全規則](https://gokurakism.com/thetorite/)
（参照日: 2026-08-15）。掲載された3～4人協力規則を採用する。

| 項目 | 完全規則 | `TheTrickGame` |
|---|---|---|
| deck | 4人52枚。3人は2～4を除く40枚、各13枚と伏札1枚 | 一致 |
| trick | C2（3人C5）保持者が自由lead、spade固定trump、must-follow、12trick | 保持者を開始playerにするがstarter自体は強制しない |
| 公開情報 | suit別背面を全員が観測し、rankに関する相談は禁止 | 他playerはsuit別枚数だけ`View`へ出し、rankを隠す |
| 成功 | 3人8/4/0＋残札/伏札4 suit、4人6/4/2/0＋残札4 suit | 一致 |
| 勝利score | 最多trick者残rank－最少者残rank＋12 | 一致 |

自由相談は表示層に委ね、Runtimeはrankを漏らさない。`TenthRuleAuditTests`はseed 1005、1033/1034で
人数別deck、開始、全12trick、quota・残suit・scoreを照合する。seed 1041では他2人の同suit rankを
交換し、公開suit枚数、View、合法手、CPUの同値を確認する。未解決差分はない。
