# Casino検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Casino](https://www.pagat.com/fishing/casino.html)のAnglo-American基本版を採用する。

- pair／sum capture、loose cardsからのsingle build、既存buildのraise、同値追加によるmultiple buildとstealを扱う。
- buildにowner unitを保持し、capture札を手札に残す義務と、自分の未回収buildを残してtrailできない制約を合法手へ反映する。
- Most Cards 3、Most Spades 1、A各1、10D 2、2S 1を累積し、21点以上の単独首位で終了する。sweepは採用しない。

`TwentyFirstRuleAuditTests`は所有buildのraiseを、固定seed監査は2～4人、capture、得点を確認する。未解決差分はない。
