# Go Fish検証仕様

資料は[Bicycle: Go Fish](https://bicyclecards.com/how-to-play/go-fish)（2026-08-15直接確認）の2～5人版を採用する。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deck/deal | 52枚。2～3人は各7枚、4～5人は各5枚 | 3人を誤って5枚としていた条件を修正し、人数境界を固定seed確認 |
| ask/catch | 自分が1枚以上持つrankを任意の相手へ要求し、相手は全該当札を渡す。catch成功なら続けて要求 | rank/targetをAction化し、成功後のCurrentPlayer不変を確認 |
| fish | 相手が0枚ならstock先頭を1枚引き、要求rankと一致したときだけ続行 | 注入乱数のstockだけを使用 |
| book/end | 4枚同rankを即公開book化。全13 book完成で終了し最多book勝利。空手札は自分の手番にstockから補充し、stockも空なら脱落 | `books`公開、空手札用`draw`、13組終了を固定seed完走 |

相手手札とstockを交換してもView・合法手・CPU選択は同値である。採用範囲に未解決差分はなく、`Verified`とする。
