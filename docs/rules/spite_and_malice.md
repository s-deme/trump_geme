# Spite and Malice検証仕様

資料は[Pagat: Spite and Malice](https://www.pagat.com/patience/spitemal.html)（2026-08-15直接確認）の冒頭に記載された最普及2人版を採用する。後半のbook version、Joker、追加人数、積み込みvariationは採用しない。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| setup | 52枚2組、pay-off各20枚、手札各5枚。公開pay-off topが高い側から開始し、同rankなら両pay-offをshuffleして再比較 | Aを誤ってhigh扱いしていた開始比較をA lowへ修正し、20 seedで非同rank・高い側開始を確認 |
| center | 最大3列、AからQまで昇順。Kはwild。Q/Kで完成した12枚をstockへshuffle | `play_center`でpay-off、hand、side topを統一処理 |
| side/turn | sideは各最大4列で順序制限なし。handからsideへ1枚置くと手番終了 | `discard_side`だけが相手へ手番を渡す |
| refill/end | 手番開始時5枚へ補充。handを使い切れば即5枚補充して続行。pay-off最終札をcenterへ出せば勝利、stock枯渇はdraw | 固定seed完走とpay-off残数scoreを確認 |

相手手札とstockを交換してもView・合法手・CPU選択は同値である。採用範囲に未解決差分はなく、`Verified`とする。
