# Portland検証仕様

資料は[ゲームファーム: Portland](https://gamefarm.jp/rule/portland.html)および[ゴクラキズム: Portland](https://gokurakism.com/portland/)（ともに2026-08-15直接確認）。Reiner Kniziaのルールを翻訳したゲームファームを正本とし、同ページが未規定と明記する同一Poker handと5枚未満だけを局所variantとして固定する。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deck/session | 2～5人、各自が独立した52枚deckを持ち、捨札を戻さず6round | 注入乱数だけで個別shuffleし、固定seed完走 |
| table | 各round開始時に5枚を表向きに置く。残り1～4枚ならその枚数、0枚なら以後play不能 | 全員の`tables`を全viewerへ公開 |
| turn | passしてroundから抜けるか、山の先頭1枚を引いて5枠のどれかへ**必ず**上書き | `draw`ではpass/reveal、`decide`ではoverwriteだけを合法化 |
| score | Poker順位ごとに（人数－順位）×round。次roundは前round1位から | round winnerを`roundStarter`へ引継ぎ |
| 未規定箇所の採用 | 初回開始者P0。同一handは前round開始者から時計回りに近い方を上位。5枚未満は全5枚handより下位とし、相互は同じ席順で決定 | ゲームファームが合意事項とする範囲だけを決定論的に固定 |

私有deckの順を入れ替えてもView・合法手・CPU選択が同値である。採用範囲に未解決差分はなく、`Verified`とする。
