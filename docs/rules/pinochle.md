# Pinochle検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Single Deck Partnership Pinochle](https://www.pagat.com/marriage/pinmain.html)のRacehorse固定pair版を採用する。

- A-10-K-Q-J-9各2枚を各12枚配り、auction後にpartnerから落札者へ3枚、落札者からpartnerへ3枚を戻す。
- run、marriage、pinochle、around、Dixを採点する。資料の点数を10分の1へ整数正規化し、既定150点sessionとする。
- must-follow、可能ならmust-win、voidならmust-trump、trumpが既に出ていれば可能な限りovertrumpする。

`TwentyFirstRuleAuditTests`は3枚往復の組合せ数とDixを、固定seed監査はauction、meld、play、sessionを確認する。未解決差分はない。
