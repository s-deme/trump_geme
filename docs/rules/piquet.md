# ピケ検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Piquet](https://www.pagat.com/notrump/piquet.html)掲載のCavendish 1882年、2人・32枚・6 deal partie版を採用する。

- 7～Aを各12枚、talon 8枚とし、elderは1～5枚、youngerは1～残talon枚を交換する。
- Carte Blanche、Point、sequence、setはplayerが`declare`または`sink`を選ぶ明示Actionとし、elder→dealerの順で比較する。
- Repiqueは宣言完了時、Piqueはplay得点到達時に判定し、must-follow、cards、capot、partieを採点する。
- 6 deal同点なら2 dealだけ延長し、なお同点ならdrawとする。

`TwentyFirstRuleAuditTests`はCarte Blancheと宣言categoryのAction境界、固定seed決定性を確認する。packetは許容される2枚variantを採用し、未解決差分はない。
