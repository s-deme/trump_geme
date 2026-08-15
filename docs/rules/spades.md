# Spades監査記録

## 判定

`RuleSpecific`を維持する。参照日は2026-08-15。

## 直接確認した完全規則

- [Bicycle: Spades](https://bicyclecards.com/how-to-play/spades)
- [Pagat: Spades](https://www.pagat.com/auctionwhist/spades.html)

## 採用範囲と一致点

現Runtimeは4人固定ペア、52枚を各13枚、各自1～13の公開bid、spade固定trump、
must-follow、spadeが切れるまでのlead禁止を実装する。目標点はBicycleが短縮戦として
認める200点を既定値とし、契約成功時はteam合算bidの10倍とovertrick、10 bagsごとの
100点罰を累積する。Bicycleの契約失敗0点に合わせて減点は行わない。同点で目標点へ
到達した場合は次handまで続行するよう補強した。

## 不一致項目と保留理由

Bicycle本文は最低bid 1、契約失敗0点を明記する一方、得点を各playerのものとして記述し、
固定ペアのteam合算契約を完全には定義していない。Pagatの固定ペア完全規則では0のNil、
失敗時`-10 × team bid`、通常500点が中核規則であり、現RuntimeにはNilがない。
両資料の要素を混成した現variantを単一の完全規則へ帰属できないため昇格しない。

## 検証

`SeventeenthRuleAuditTests` seed 1701、1710、1711、1780で、完走再現、契約失敗0点、
目標同点延長、相手手札を入れ替えた観測同値と合法CPUを確認した。
