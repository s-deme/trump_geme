# Big Two監査記録

状態は`Verified`。採用variantの完全規則は[ゴクラキズムの大老二](https://gokurakism.com/dairoji/)、
地域差の照合には[Pagat](https://www.pagat.com/climbing/bigtwo.html)を用いた（参照日: 2026-08-15）。
ゴクラキズム記載の4人・反時計回り・異種5枚役比較を採用し、straightは2を含めない側へ固定する。

| 項目 | 採用規則 | `BigTwoGame` |
|---|---|---|
| 開始・強さ | 3Cを含む任意組から開始。2>A>…>3、S>H>D>C、反時計回り | 一致 |
| 組 | single、pair、tripleは同枚数で上回る | rank後に最高suitを比較 |
| 5枚 | straight < flush < full house < four+1 < straight flush。異種でも上位役可 | category 0～4。full houseはtriple/pair双方が上回る条件 |
| trick更新 | 他3人passで最後のplayerが自由lead | 3 pass後に`table`を消去し`lastPlayer`へ戻す |
| 終了・精算 | 上がり時、残数を支払う。残2各2倍、8枚以上3倍、13枚4倍。Dragonは3倍、同花Dragonは4倍 | 同じ非重複倍率をwinnerへ移転 |

`NinthRuleAuditTests`は5枚5category、3C開始、反時計回り、残札/2/8/13倍率をseed 904/941で照合し、
seed 963で相手2手札の観測同値を確認する。未解決差分はない。
