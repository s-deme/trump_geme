# Skat検証仕様

状態は`Verified`。参照日は2026-08-15。[International Skat Order](https://www.ispaworld.info/images/ispa-world/canada/ISkO%20Revisions%202016%20Feb%201.pdf)に基づく3人版を採用する。

- 7～Aの32枚を各10枚＋skat 2枚に配り、公式game value列の数値auction、skat取得またはhandを選ぶ。
- Diamonds/Hearts/Spades/Clubs、Grand、Null、Null Openと、hand時のSchneider/Schwarz/Open宣言を明示Actionにする。
- Jの切札順、card points、matadors、hand／Schneider／Schwarz倍率、overbidの二倍損失を精算し、既定18 dealを行う。

`TwentyFirstRuleAuditTests`はhand contract全集合を、固定seed監査はauction・得点・秘密skat・決定性を確認する。未解決差分はない。
