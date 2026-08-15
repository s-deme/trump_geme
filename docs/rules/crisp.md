# Crisp 検証仕様

## 出典と採用variant

公式の統一ルールブックは確認できなかった。このため、カード集合、先手規則、特殊組合せ、補充、
3点先取を掲載するゴクラクテンの[Crisps ルール](https://gokurakism.com/crisps/)を信頼できる採用資料とした。
参照日は2026年8月15日、採用variantは同資料の2人・40枚・3ディール先取版である。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2人 | Registryと`CrispGame`は2人専用。 |
| 使用カード | 各suitの2～10・Q、計40枚。suitは強さに影響しない | 同じrank集合で40枚を生成する。Qが最強、2が最弱。 |
| 配札 | 各12枚、残りから4枚を除外、残りをstockにして先頭を表にする | `StartDeal()`が12枚ずつ、4枚除外、`faceUp`を作る。 |
| 開始状態 | 初回は任意、以降は合計得点の低い側、同点なら前回non-starter | 初回P0固定、以降は`matchPoints`と前回`starter`で同じ規則を実装する。 |
| 全フェーズ | starterの組合せ出し、応答／pass、勝者の表／伏せ選択補充、次ラウンド、ディール得点 | `play`、`pass`、`reward`、次`play`を状態遷移として保持する。 |
| 合法手 | 単札、ペア、3枚以上ラン、2ペア以上ペアラン。通常は同型・同枚数・同rank以上 | `Classify()`と`CanBeat()`が全部分集合から列挙・制限する。Qはランに入らない。 |
| 特殊札・例外 | 3／4枚同rankは特別組。Qを含む通常組へだけ特別組を重ねられ、特別組同士は枚数増または同rank以上 | `Triple`／`Quad`と`CanBeat()`がこれを実装する。passは場を消し、stockが尽きれば補充しない。 |
| 勝敗 | 手札を先に尽くした側がそのディールの勝者 | 空手札で1点を加算する。 |
| 得点 | 1ディール1点、合計3点で勝ち | `matchPoints`が3に達した時に終了する。 |
| 終了条件 | いずれかが3点 | `Result()`は`first to three deals`を返す。 |
| ローカルルール | 記載なし | 初回starterだけP0に固定し、追加optionは持たない。 |

## CLI/Unityへの正規化

初回の任意starterはP0へ固定するが、以後の先手は原規則どおり得点と前回starterから決める。除外4枚と
既出札は後続の選択に戻らないため状態から除去する。表札／伏札を取る選択、pass、全ての合法な組合せは
`Action`で保持し、失われる選択肢はない。

## 実装・テスト・差分

監査で、Runtimeが後続ディールのstarterを単純交代としていた差分を発見したため、`CrispGame.StartDeal()`を
低得点側優先・同点時前回non-starterへ修正した。`SecondRuleAuditTests.CrispUsesMatchScoreStarterAndAllowsTripleOverQueen`
はseed 211/212で先手規則、Qへのトリプル、3点終了を確認する。同
`CrispViewAndCpuIgnoreTheOtherPlayersHiddenHand`は相手手札を変えた観測同値試験である。

未解決差分はない。
