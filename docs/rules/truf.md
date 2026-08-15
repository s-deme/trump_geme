# Truf検証仕様

資料は[Pagat: Truf](https://www.pagat.com/exact/truf.html)（2026-08-15直接確認）。3～4人、単札bid、13deal、Pagatの第3得点方式へ第2方式の0bid成功5点を組み合わせるvariantを採用する。`deals`の明示値だけを再現用の短縮sessionとして認める。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deck/deal | 4人52枚、3人はclubを除く39枚、各13枚。初回counterclockwise、以後方向交互、最低累計者dealer | `TrufGame`の3/4人固定seedで確認 |
| 再配布 | 全札が2～10、または全札がA/K/Q/Jの手札があれば同dealer・同方向で再配布 | `StartDeal()`で手札条件を満たすまで注入乱数だけで再配布 |
| bid | 手札1枚を秘密に選び全員選択後に公開。A=1、J/Q/K=0、同値suit順S>H>D>C | 逐次Actionへ正規化し、選択中は本人以外`XX`、決定後は`bid_cards`を全公開 |
| mode | 合計13超はatas、未満はbawah、13なら最高bidderが全bidを同量増減（負値可） | `increase_all`/`decrease_all`を合法手として列挙 |
| play/観測 | must-follow、break前trump lead禁止。trump札はtrick終了まで伏せ、終了時公開 | 途中`XX`と`revealed_trick`を専用固定seedで確認 |
| score/session | 正差を2倍、0以下はそのまま。bawahで調整後bid 0かつ0 trickは+5。13deal累計最高 | trickを独立再計算して`GameResult`と照合 |

相手手札を入れ替えてもView・合法手・CPU選択が同値である。採用範囲に未解決差分はなく、`Verified`とする。
