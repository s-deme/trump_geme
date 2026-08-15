# Blackjack監査記録
資料は[Bicycle: Blackjack](https://bicyclecards.com/how-to-play/blackjack)（2026-08-15直接確認）。
|項目|資料|実装・判断|
|---|---|---|
|hit/stand/double/split/insurance|標準規則|`BlackjackGame`確認|
|dealer/payout|17以上stand、通常1:1、natural 3:2、push返却、insurance 2:1|既定S17と精算は一致|
|double|最初の2枚から倍賭けし1枚だけ引く|現状はhard/soft合計9～11に限定する出典外制約|
|split|同rank pairを同額追加で2handへ|split Aceを常に1枚で強制stand、最大4handなど採用house ruleの完全根拠が未確定|
dealer hole cardとdeck順の非公開は確認済みだが、double/splitのhouse境界が残るため`RuleSpecific`を維持する。
