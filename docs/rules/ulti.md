# Ulti監査記録

資料は[Pagat: Ulti](https://www.pagat.com/marriage/ulti.html)（参照日: 2026-08-15）。3人32枚・talon継続auction・must beat variantを対象にした。
| 項目 | 資料 | 実装・判断 |
|---|---|---|
| talonとauction | pass後も再bid可 | `UltiGame`は限定phase、未解決 |
| must follow/trump/beat | 通常規則 | 合法手で実装確認 |
| bonus/contract表 | 多数のHungarian contracts | 限定採用、未照合 |
順次bid/discardへ正規化、非公開talonはCPU/Viewから除外。修正なし、seed 605完走・合法CPU試験。契約体系差でRuleSpecific維持。
