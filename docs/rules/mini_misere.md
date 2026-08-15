# Mini Misere監査記録

状態は`Verified`。一次資料は作者David Parlettの
[Minimisère完全規則](https://www.parlettgames.uk/oricards/minimis.html)（参照日: 2026-08-15）。
Deuceを使う作者規則を採用し、5人を標準、3/4/6人を同ページの人数別規則として実装する。

| 人数 | deck・手札 | 得点・session | `MiniMisereGame` |
|---:|---|---|---|
| 5 | A,K,Q,J,10,2×4 + Joker、5枚 | 0/1/2/3/4/5勝=5/1/2/6/8/0、Lot=10、25点 | 一致 |
| 4 | 5人用25枚から各6枚 | 0/1/2/3/4/5/6勝=6/1/2/3/8/10/0、Lot=12、25点 | 一致（1枚undealt） |
| 6 | A,K,Q,J,10,9,8,7,2×4、Jokerなし、6枚 | 4人と同じ得点、Lot=12、25点 | 一致 |
| 3 | A,K,Q,J,10×4 + Joker、7枚 | 0/1/2/3/4/5/6/7勝=7/1/2/3/8/10/12/0、Lotなし、採用目標31点 | 一致 |

全人数でmust-follow・no-trump。2はlead時だけ最高、その他は最低。Jokerはleadなら勝ち、4～6人の
応手なら負ける。3人戦では第2手なら負け、第3手なら`play_joker_win`/`play_joker_lose`で選ぶ。
4～6人のLot宣言は、作者規則どおり各playerが第1trickで自分の札を出す直前に行うよう修正した。
失敗時は宣言者0、非宣言者へhand size点を与える。首位同点なら延長する。

3人戦は作者が許容する合意目標の例31を既定とし、4～6人は25点を既定とする。既存CLIの
`target_score`は局所オプションとして別の合意目標を指定できる。
`EighthRuleAuditTests`はseed 805、893～896、900～1000で人数別deck/手札、Lot宣言時機、
第3手Jokerの勝敗選択、完走を確認し、seed 1005で相手2手札の観測同値を確認する。未解決差分はない。
