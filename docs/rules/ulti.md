# Ulti検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Ulti](https://www.pagat.com/marriage/ulti.html)の3人・32枚版を採用する。

- 初手12枚、他10枚、talon 2枚とし、取得・2枚discard・再auctionを繰り返す。元の高bidderにも再取得を認める。
- suit/heart、40-100、20-100、Ulti、Betli、Durchmars、openと複合契約をrank表で比較する。
- must-follow、可能ならmust-beat、voidならmust-trump／overtrump、Ultiのtrump 7温存、marriageと各bonusを扱う。

`TwentyFirstRuleAuditTests`はtalon再取得とtalonなしraiseを、固定seed監査は契約表・play・得点を確認する。非公開talonをView/CPUから隔離し、未解決差分はない。
