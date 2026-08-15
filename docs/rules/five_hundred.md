# Five Hundred監査記録

資料は[Pagat](https://www.pagat.com/euchre/500.html)（参照日: 2026-08-15）。Runtimeは3人kitty交換variantを採用する。
| 項目 | 資料 | 実装・判断 |
|---|---|---|
| auction/kitty | bid勝者が3枚を交換 | `FiveHundredGame`、基本一致 |
| bowers/misere/得点 | 国・人数別差 | 契約表と得点が完全未照合 |
入札・discardを逐次Action化、手札とkitty順は非公開。修正なし、seed 602の固定完走・CPU合法性を追加。地域variant差を残しRuleSpecific維持。
