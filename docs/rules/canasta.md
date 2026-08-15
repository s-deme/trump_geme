# Canasta検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Canasta](https://www.pagat.com/rummy/canasta.html)と[Bicycle: Canasta](https://bicyclecards.com/how-to-play/canasta)のClassic 4人固定pair版を採用する。

- 108枚を各11枚。初回starterがwild／3なら追加でめくり、pileをfreezeする。赤3は公開・補充する。
- 自然札2枚以上、wild最大3枚、初回meld下限を満たす組合せを`initial_meld`値として明示選択する。frozen/unfrozen pileの取得条件を分ける。
- 上がり前にpartnerへ許可を求め、回答は申請者の当該手番だけに拘束する。concealed out、black three、自然／混成canastaを得点化する。
- 5000点先取の複数hand sessionとする。

`TwentyFirstRuleAuditTests`は許可の申請者scopeと旧停止seed 98を、固定seed監査はmeld・pile・sessionを確認する。未解決差分はない。
