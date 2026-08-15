# シェリフ検証仕様

## 資料・採用variant

[ゴクラキズム: シェリフ](https://gokurakism.com/sherif/)（参照日: 2026-08-15）を完全規則として採用する。紹介元はThe Game Galleryであり、本文は使用札、強さ、配札、役選択、trump、Joker、得点、終了条件を明記する。

## 項目別照合

| 項目 | 実装 | 資料根拠 | 判断 |
|---|---|---|---|
| 21枚・3人各7枚 | `StartDeal()`がA/K/Q/J/10各4枚とJokerをshuffleして7枚ずつ配る | 使用札・配札 | 一致 |
| 強さとJoker | `Strength()`はA>K>Q>J>10、`LegalActions()`はJokerをいつでも許可、`TrickWinner()`から除外する | カード強さ・Jokerはいつでも出せて必ず負ける | 一致 |
| Joker保持者起点の役選択 | `CurrentPlayer`をJoker保持者にし、`choose_role`を市長・保安官・強盗の重複なし逐次Actionへ正規化 | Joker保持者から役割を選ぶ | 一致 |
| 市長のtrump/no-trumpとlead、マストフォロー | `choose_trump`は4 suitと`N`、市長をCurrentPlayerにしてplayを開始し、`LegalActions()`がfollowを制限 | 市長が事前にtrump/no-trumpを選び、市長leadの通常trick-taking | 一致 |
| 役別得点 | `FinishDeal()`が保安官K、強盗10、市長Q/J−未獲得K−強盗10を非負で加算 | 各役の得点式・最低0点 | 一致 |
| 終了 | 既定`target_score=8`で誰かが到達時に終了 | 標準8点到達 | 一致 |

## 正規化・検証・結論

役選択はJoker保持者から時計回りに1つずつ`choose_role`するActionへ正規化する。`View`は全員に公開済みの役職・trump・得点と、自分の手札および他者の枚数だけを表示する。`FifthRuleAuditTests`は固定seed完走、役選択/no-trump境界、Joker leadがtrickを取れないこと、二つの相手手札を交換した観測同値性を確認する。

未解決差分はない。`sheriff`を`Verified`へ昇格する。
