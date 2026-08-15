# Five Hundred検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Five Hundred](https://www.pagat.com/euchre/500.html)の3人・32枚＋Joker版を採用する。

- 各10枚とkitty 3枚、6～10 tricksのS/C/D/H/No Trump、Misere、Open Misereを勝ち抜きauctionで選び、落札者がkittyから3枚を戻す。
- trumpではJoker、right bower、left bowerの順とし、No Trump/MisereのJoker suit指定・Joker lead suit・void時の強制playをAction化する。
- Misereは7NTと8NTの間で250、Open Misereは9NTと10NTの間で500。契約成功で500以上、または-500以下で終了する。

`TwentyFirstRuleAuditTests`はMisere順位／得点とNo Trump Joker指定を、固定seed監査はauction・play・sessionを確認する。未解決差分はない。
