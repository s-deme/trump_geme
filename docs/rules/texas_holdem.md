# Texas Hold'em検証仕様

状態は`Verified`。参照日は2026-08-15。[Bicycle: Texas Hold'em](https://bicyclecards.com/how-to-play/texas-holdem-poker)と[Pagat: Poker Betting](https://www.pagat.com/poker/rules/betting.html)に基づくfixed-limit sessionを採用する。

- 2～10人、buttonとblind 1/2をhandごとに移動し、preflop/flopは2、turn/riverは4、各street最大3 raiseとする。
- hole 2枚とboard 5枚から最強5枚を評価する。folded playerを各potの勝者候補から除外する。
- partial all-inは拠出levelごとにmain／side potへ分割し、odd chipはbutton左から配る。最後の1 stackになるまでhandを継続する。

`TwentyFirstRuleAuditTests`は複数人数のチップ総量保存とsession終了を、固定seed監査はside pot・決定性・秘密情報を確認する。未解決差分はない。
