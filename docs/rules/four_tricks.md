# Four Tricks検証仕様

## 判定

`Verified`。参照日は2026-08-15。以前の「score表原典未取得」は再検索で解消し、候補元の
公開完全規則ページを直接確認した。

## 根拠

- [ゴクラキズム: フォートリックス](https://gokurakism.com/fourtricks/)

## 採用規則

- 3人専用。52枚からJokerと2～5を除いた6～Aの36枚を、dealer左から1枚ずつ各12枚配る。
- dealer左がleadし、no trump・must-followで、lead suit最高札がtrickを取って次をleadする。
- 最終の12番目のtrickだけ2 trick分と数え、round合計を13 trickとする。
- 0/1/2/3/4 trickはそれぞれ-5/+1/+3/+6/+10点、5～13 trickは獲得数と同じ負点とする。
- dealerを時計回りに交代して3の倍数dealを行う。既定は最小の3 dealsで、累積最高点を勝者とする。

## 観測境界

dealer、roundごとの取得trick数、累積点、現在trickは公開し、各手札は本人だけへ表示する。
CPUは自身の手札と公開trick数だけで4 trickを目標に合法札を選ぶ。

## 検証

`EighteenthRuleAuditTests` seed 1804、1830、1882で、固定seed完走、36枚構成、最終trick二重、
全得点表の代表境界、相手手札を入れ替えた観測同値、合法CPUを確認した。未解決差分はない。
