# Rummy 500検証仕様

資料は[Pagat: 500 Rummy](https://www.pagat.com/rummy/500rum.html)（2026-08-15直接確認）。同ページに明記された「Jokerなし」「Rummy callなし」「深い捨て札を取る場合は最下札を新規meldへ使う」variationを組み合わせて採用する。discard必須上がり／floatingは採用しない。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| pack/deal | 2～8人。2人13枚、3人以上7枚、5人以上は2組。Jokerなし | 注入乱数だけで52/104枚をshuffleし、dealer左から配る |
| draw | 山先頭、捨て札先頭、または深い公開捨て札から選ぶ。深い札は即時新規meldへ使い、その上を全取得 | `draw_stock`、`draw_discard`、`take_discard_meld`を列挙。先頭捨て札を取った場合は同じ札の即捨てを禁止 |
| meld/layoff | 異suit同rank 3～4枚、同suit連続3枚以上。AceはA-2-3…または…Q-K-Aで使う。全playerの公開meldへ付け札可 | meld種別と対象をAction化し、札ごとの得点ownerを公開表示 |
| value | 2～10額面、J/Q/K=10、Ace=15。ただしlow sequenceのAceだけ1 | 新規meld・深い捨て札meld・low runへのAce付け札で文脈得点を分離し、A23=6、QKA=35、Ace set=45を確認 |
| end/session | 誰かの手札0、または山切れ時に捨て札を取らない選択でhand終了。公開得点－残手札点を累積し500以上の単独首位がwinner。同着なら続行 | `target_score`局所optionで短縮可。同着首位をterminalにせず次handへ進める |

相手手札とstock札を交換してもView・合法手・CPU選択は同値である。固定seed 1602/1620/1680で完走し、採用範囲に未解決差分はないため`Verified`とする。
