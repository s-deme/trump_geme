# Five Card Draw監査記録

## 判定

`RuleSpecific`を維持する。参照日は2026-08-15。

## 直接確認した完全規則

- [Pagat: Five Card Draw Poker](https://www.pagat.com/poker/variants/5draw.html)
- [Pagat: Poker Betting](https://www.pagat.com/poker/rules/betting.html)

## 採用範囲と一致点

現Runtimeは2～6人、52枚、ante 1、各5枚、1回目betting、0～3枚のdraw、2回目betting、
showdownを実装する。chips 20、前半1／後半2のfixed limit、最大3 raiseの単handを局所variant
として採用する。各手札と山札順は非公開で、draw枚数とbetting状態だけを公開する。

## 不一致項目と保留理由

Pagat基本形では最初のbettingが全checkならhandを流してpotを持ち越し、次dealerで再dealするが、
現Runtimeはそのままdrawへ進む。またpartial all-in時のmain pot／side pot分割とpot別参加資格が
なく、複数handのdealer移動・pot持越しもない。中核のpot帰属が変わるため昇格しない。

## 検証

`SeventeenthRuleAuditTests` seed 1705で固定seed完走、0～3枚draw、手札と山札順の隔離、合法CPUを
確認した。all-check redeal、side pot、継続sessionは未解決である。
