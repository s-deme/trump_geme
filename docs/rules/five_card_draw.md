# Five Card Draw検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Five Card Draw](https://www.pagat.com/poker/variants/5draw.html)と[Poker Betting](https://www.pagat.com/poker/rules/betting.html)のfixed-limit版を採用する。

- 2～6人、ante 1、各5枚、0～3枚draw、前半1／後半2、各street最大3 raiseとする。
- 最初のbettingが全checkならhandを流し、potを保持したままdealerを移動して全員が再ante・再dealする。
- partial all-inは拠出levelごとのmain／side potと参加資格を処理し、odd chipをdealer左から配る。最後の1 stackまで継続する。

`TwentyFirstRuleAuditTests`は全check時のpot 2→4持越し、チップ総量、session終了を確認する。手札とdeck順をView/CPUから隔離し、未解決差分はない。
