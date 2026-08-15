# ピケ監査記録（RuleSpecific）

## 出典と採用候補variant

公式の統一ルールブックは確認できないため、Cavendish 1882年規則を明示する
[Pagat: Piquet](https://www.pagat.com/notrump/piquet.html)を主資料とした。参照日は2026年8月15日。
照合対象は同ページの2人・32枚・各12枚・talon 8枚・6 deal partie版である。

## 項目別照合

| 項目 | 資料 | Runtime照合結果 |
|---|---|---|
| 人数 | 2人 | 一致。 |
| 使用カード | 7～Aの32枚、no-trump | 一致。 |
| 配札 | 各12枚、talon 8枚、dealer交代 | 一致。packetは2枚固定で、資料が認める2又は3枚のうち2枚を採用。 |
| 開始状態 | non-dealer=elder。Carte Blancheは任意宣言 | elder開始は一致。Carte Blancheを自動加点しており不一致。 |
| 全フェーズ | elder／younger exchange、任意宣言、12 trick、partie精算 | exchange・trick・精算はあるが、宣言会話／選択フェーズがない。 |
| 合法手 | elderは1～5枚、youngerは1～残talon枚を交換。playはmust-follow | 最低1枚を両者に強制するよう修正。must-followは一致。 |
| 特殊札・例外 | Point／sequence／setの任意宣言・sinking、Carte Blanche、Repique、Pique | 役比較・点計算は自動化され、sinkingとCarte Blancheの選択・公開時機を実装していない。 |
| 勝敗 | 6 dealの高得点者、同点なら2 deal追加、それでも同点ならdraw | 6→8 dealの限定延長とdrawへ修正。 |
| 得点 | 宣言、lead／奪取／last、cards／capot、Repique／Pique、partie精算 | 一部の自動採点はあるが、宣言順と選択が資料どおりではない。 |
| 終了条件 | 6 deal又は同点時8 deal | 一致するよう修正。 |
| ローカルルール | deal packet、partie外の慣行は選択 | 2枚packetを固定。 |

## CLI/Unityへの正規化と未解決差分

物理的なdealとcutは注入rngへ正規化でき、discardの選択も`exchange` Actionへ保持できる。しかし標準資料は
宣言を出すか隠すか、同点宣言への応答、Carte Blancheの宣言時機を戦略的選択としている。現在のRuntimeは
これらを自動集計しており、失われない選択肢を満たさない。そのため`piquet`は`Verified`に昇格しない。

## 実装・テスト・差分

この監査ではyoungerの0枚交換を禁止し、6 deal同点時の延長を無制限から追加2 dealだけへ修正した。
`CoreContractTests.PiquetRequiresBothPlayersToExchangeAtLeastOneCard`と
`ThirdRuleAuditTests.PiquetExtendsOnlyToAnEighthDealBeforeDeclaringADraw`で固定seed／境界を確認する。

未解決差分は、Carte Blancheの選択と公開、Point／sequence／setの逐次宣言・sinking・確認応答、
それらに依存するRepique／Piqueの時機である。これらをAction化し、秘密情報テストまで追加するまで
`RuleSpecific`のままとする。
