# Pass Cut Run検証仕様

資料は[ゴクラキズム: Pass Cut Run](https://gokurakism.com/pcr/)（2026-08-15直接確認）。公開ページの4人固定ペア・4deal variantを採用し、`deals`明示値だけを短縮sessionとして認める。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deal/trump | 52枚を各13枚。dealer最終札を公開し、そのsuitをtrumpとして手札へ戻す | 固定seedでtrumpと全52枚playを確認 |
| pass | 隣席partnerへ、受取札を見る前に2枚ずつ渡す | 全員の2枚選択後に一括交換 |
| order | dealer対面が初lead。各trickはleaderのpartnerが必ず4番手となる向き | 従来の2番手になる方向計算を修正し、`order`境界を検証 |
| play | must-follow、trump優先、A high、勝者が次lead | trickをテスト側で独立判定 |
| score/session | 自勝ちRun=1、隣席Cut=2、対面Cut=3、partner勝ちPass=4。全員1回dealerの4deal | 1dealの個人点と固定ペア合計を独立再計算 |

逐次passは全選択完了まで受取札を加えない。相手手札を入れ替えても観測・合法手・CPU選択は同値であり、未解決差分はないため`Verified`とする。
