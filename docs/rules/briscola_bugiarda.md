# Briscola Bugiarda監査記録

資料は[ゴクラキズム: ブリスコラ・ブジャルダ](https://gokurakism.com/briscola_bugiarda/)（2026-08-15直接確認）。公開完全規則は実在し、5人版の40枚pack、rank、card point、may-follow、秘密partner、chip表を採用範囲として照合した。

| 項目 | 資料 | 実装・判断 |
|---|---|---|
| 通常rank bid/秘密partner | 弱いrankへhard pass、呼札保持者がpartner、自札callなら隠れsolo | 実装済み |
| play | may-follow、trump優先、なければlead suit | 実装済み |
| chip精算 | 61～70から111～119まで6段階、120/0は12単位。declarer 2、partner 1、solo 4、相手-1の倍率 | `SettlementUnit()`と一致。規定roundは採用variantとして5dealに固定 |
| 明示Solo bid | rankより強い最上位宣言。宣言時は即auction終了し、trumpを宣言しない1対4 | 最上位`bid_solo`で即playへ進み、`trump=none`・公開soloとして処理。一致 |

`NineteenthRuleAuditTests`は明示Soloが全viewerへ公開されること、no-trumpの合法CPU完走、
固定seed 1903、二つの相手手札を入れ替えた観測同値を確認する。未解決差分はないため
`Verified`とする。
