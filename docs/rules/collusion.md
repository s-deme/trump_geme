# Collusion監査記録

状態は`Verified`。一次資料は作者David Parlettの
[Collusion完全規則](https://www.parlettgames.uk/oricards/collude.html)、補助資料は
[ゴクラキズム](https://gokurakism.com/collusion/)（参照日: 2026-08-15）。4人・100点規則を採用する。

| 項目 | 作者規則 | `CollusionGame` |
|---|---|---|
| trick | 52枚を各13枚、A high、must-follow、no-trump | 一致 |
| 得点 | 各trick 1。ちょうど2人同数なら各10、全員別なら最少20、3人同数なら残り1人30 | 勝数groupから同じ排他的bonusを計算 |
| 100点条件 | bonusを伴う到達だけ有効。trick点だけで到達する場合はその勝数を減点 | `plainReach`時だけ`-tricks`、bonusありは加点 |
| 会話 | 手札内容を明言せず、非拘束の提案・談合を自由に行える | 表示層の任意chatへ正規化。`IGame`はカード状態を公開しない |

既定100点を採用し、作者が紹介する50点短縮は既存`target_score`で局所指定できる。
`NinthRuleAuditTests`はseed 902/911でbonus分布とbonusなし到達の反転を独立計算し、seed 961で
相手2手札の観測同値を確認する。未解決差分はない。
