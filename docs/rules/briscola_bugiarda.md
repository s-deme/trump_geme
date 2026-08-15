# Briscola Bugiarda監査記録

資料は[ゴクラキズム: ブリスコラ・ブジャルダ](https://gokurakism.com/briscola_bugiarda/)（2026-08-15直接確認）。公開完全規則は実在し、5人版の40枚pack、rank、card point、may-follow、秘密partner、chip表を採用範囲として照合した。

| 項目 | 資料 | 実装・判断 |
|---|---|---|
| 通常rank bid/秘密partner | 弱いrankへhard pass、呼札保持者がpartner、自札callなら隠れsolo | 実装済み |
| play | may-follow、trump優先、なければlead suit | 実装済み |
| chip精算 | 61～70から111～119まで6段階、120/0は12単位。declarer 2、partner 1、solo 4、相手-1の倍率 | `SettlementUnit()`と一致。規定roundは採用variantとして5dealに固定 |
| 明示Solo bid | rankより強い最上位宣言。宣言時は即auction終了し、trumpを宣言しない1対4 | **未実装**。現状はrank bid後に必ずtrumpを選び、自札callによる隠れsoloしかない |

明示Soloはauction、trumpなしの勝敗、CPU観測へ及ぶ中核contractであり、省略した実装を同一variantとは扱えない。seed 1203の現行5deal完走は確認済みだが、未解決差分が残るため`RuleSpecific`を維持する。
