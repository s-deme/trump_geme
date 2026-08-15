# Texas Hold'em監査記録

## 判定

`RuleSpecific`を維持する。参照日は2026-08-15。

## 直接確認した完全規則

- [Bicycle: Texas Hold'em Poker](https://bicyclecards.com/how-to-play/texas-holdem-poker)
- [Pagat: Poker Betting](https://www.pagat.com/poker/rules/betting.html)

## 採用範囲と一致点

現Runtimeは2～10人、52枚、各2枚の非公開hole cards、small/big blind、preflop・flop・turn・
riverの4 betting street、5枚の公開board、7枚からの最強5枚評価を実装する。chips 20、blind
1/2、preflop/flop 2、turn/river 4のfixed-limit単handを局所variantとして採用している。

## 不一致項目と保留理由

partial all-in時のmain pot／side pot分割とpotごとの参加資格がなく、異なる拠出額でも単一potを
全showdown参加者で争う。Bicycleが示すbutton移動後の次handもなく、1 hand終了時のstackを
即時結果とする。all-inと継続sessionはいずれも勝敗へ直結するため昇格しない。

## 検証

`SeventeenthRuleAuditTests` seed 1704で固定seed完走、4 street、hole cardと山札順の隔離、合法CPUを
確認した。side pot、all-in参加資格、複数hand sessionは未解決である。
