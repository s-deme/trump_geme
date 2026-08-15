# Spades検証仕様

状態は`Verified`。参照日は2026-08-15。採用variantは[Pagat: Spades](https://www.pagat.com/auctionwhist/spades.html)の4人固定pair基本版である。

- 各13枚、0～13 bid、0はNil、spade固定trump、must-follow、break前のspade lead禁止とする。
- pair契約成功は`10 × bid + bags`、失敗は`-10 × bid`。Nilは個別に±100、10 bagsごとに-100とし、既定500点へ到達した単独首位で終了する。
- CPUの既定bidは最低2とし、観測不能なpartner／opponent手札を参照しない。

`TwentiethRuleAuditTests`はNilをpartner契約から分離した採点とbagsを固定し、固定seed完走・観測境界も確認する。未解決差分はない。
