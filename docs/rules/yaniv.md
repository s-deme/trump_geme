# Yaniv検証仕様

資料は[ゴクラキズム: ヤニブ](https://gokurakism.com/yaniv/)および[Pagat: Yaniv](https://www.pagat.com/draw/yaniv.html)（ともに2026-08-15直接確認）。ゴクラキズムの5点宣言・101点終了・50/100点半減を採用し、deck枯渇、次round開始者、4人以上の2deckはPagatのIsraeli標準規則で補う。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deck/deal | 52枚＋Joker2、4人以上は2組。各5枚と初期公開札 | 3人stock 38枚、4人stock 87枚を固定seed確認 |
| discard | 単札、同rank2枚以上、同suit連番3枚以上。A low、K-A非接続、Jokerはrun代用 | setの両端選択とJoker位置をAction値の順序で保持 |
| draw | 捨てた後、山札または「直前playerが捨てた組」の端から1枚 | 自分の今回捨札を引けた状態混同を`drawOptions`分離で修正 |
| stock枯渇 | 最新の捨札組を除くdumpを注入乱数でshuffleして再利用 | 回数制限と出典外の強制精算を除去 |
| Yaniv/Asaf | 手番開始時5点以下で宣言し全手札公開。単独最少なら本人0、同点以下がいれば本人は手札点+30 | `revealed_hands`を次roundにも保持して公開を観測可能にした |
| session | 50ちょうどは25、100ちょうどは50。誰か101以上で終了し最少累計勝利 | round勝者（同点はcaller左から最初）が次開始者 |

2～8人の採用範囲を固定seed完走し、相手手札・stockを入れ替える観測同値も確認した。未解決差分はなく、`Verified`とする。
