# 替え玉トリック検証仕様

## 資料・採用variant

[ゴクラキズム: 替え玉トリック](https://gokurakism.com/kaedama/)（参照日: 2026-08-15）の3人用完全規則を採用する。

## 項目別照合

| 項目 | 実装 | 資料根拠 | 判断 |
|---|---|---|---|
| 30枚・強さ・カード点 | `StartDeal()`は8〜Aの28枚+Joker2枚を各10枚、`Strength()`と`PointValue()`はJoker/A/10/K/Q/Jの強さと15/11/10/4/3/2点を実装 | 使用札、spade固定trump、Joker先出し、得点表 | 一致 |
| followと役職 | `LegalActions()`はspade/Jokerを同一trumpとしてfollowし、最初のJokerでsoloist、2枚目でAkechi/Kobayashiを公開する | マストフォロー、最初のJokerが怪人、Joker所持を口外しない | 一致 |
| 明智探偵ありの判定 | `FinishDeal()`は76点、小林≥明智、両者差30のいずれかで怪人勝利とし、低い指定点（最低10）を配分 | 明智探偵ありの3勝敗条件と得点 | 一致 |
| Joker2枚を怪人が所持した場合 | 100超で怪人敗北、それ以外の76点/少年探偵団差30、低い指定点（最低10）を実装 | 少年探偵団の判定と得点 | 一致 |
| session | 既定`deals=9`、`Result()`が合計最多得点を勝者とする | 9ディール後の最多得点 | 一致 |

## 正規化・検証・結論

Jokerによる役職決定は`play` Actionの副作用へ正規化し、二つ目が出るまでpartnerを`View`へ出さない。`SeventhRuleAuditTests`は固定seed完走、最初と二つ目のJoker公開境界、二つの相手手札を交換した観測同値性を確認する。

未解決差分はない。`kaedama_trick`を`Verified`へ昇格する。
