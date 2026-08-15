# Card Capture 検証仕様

## 出典と採用variant

ゲームID `card_capture` の配布元による公式ルールブックは確認できなかった。このため、原作
Card Capture の紹介と実プレイ手順を掲載するゴクラクテンの
[Card Capture ルール](https://gokurakism.com/cardcapture/)を信頼できる採用資料とし、同記事が
リンクする [BoardGameGeek の作品ページ](https://boardgamegeek.com/boardgame/264566/card-capture)を
作品同定の補助資料とした。参照日は2026年8月15日である。採用variantは同記事の54枚・敵列4枚版。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 1人 | Registryは1人専用。 |
| 使用カード | 52枚＋Joker 2枚 | `CaptureCard`でJokerを区別して54枚を表す。 |
| 配札 | 2/3/4各4枚とJoker 2枚を個人deck（14枚）、残40枚をenemy deck | constructorが同じ集合を注入rngでshuffleする。 |
| 開始状態 | 敵4枚を表にし、J/Q/K/Aはenemy deck底へ戻して空きを残す | 初期化だけはhigh cardを補充し直さず、空きのまま開始する。 |
| 全フェーズ | 敵補充、任意捨札、4枚までdraw、捕獲 | `StartRound`、`discard_cards`、draw、`capture`/代替actionの順に一箇所で遷移する。 |
| 合法手 | 同suit合計が敵以上となる任意組を選ぶ | `LegalActions()`が全対象・全部分集合を列挙する。 |
| 特殊札・例外 | Jokerは手札札のcopy。捕獲不能なら敵からの捕獲又は生贄。A/J/Q/Kは後二者に使えない | Jokerは同suit手札の値をcopyする等価な捕獲集合へ正規化し、`enemy_capture`/`sacrifice`/`game_over`で制限する。 |
| 勝敗 | enemy deckと敵列が空なら勝ち | `won`と`enemy deck cleared`に対応する。 |
| 得点 | 資料は勝敗のみ | `IGame`結果用に勝利1、敗北は残A・絵札数の負値を付けるローカル正規化。 |
| 終了条件 | 上記の勝利、又は代替を選べない敗北 | `Result()`が二つの終了理由を返す。 |
| ローカルルール | 記載なし | 人数・deck・乱数はinstance内で固定し、追加optionは設けない。 |

## CLI/Unityへの正規化

物理的な敵捕獲場所や伏せた個人捨札は後続の選択へ影響しないため、Runtimeでは状態外へ除去する。
Jokerのcopy先は、捕獲後に残らず結果も同じであるため、合法な同suit手札の最大値として決定する。
一方で、捨札の任意部分集合、捕獲対象、捕獲に使う札、生贄対象はすべて`Action`で選べ、選択肢は失われない。

## 実装・テスト・差分

`CardCaptureGame`は資料との差分なし。`InitialRuleAuditTests.CardCaptureFixedSeedExecutesDiscardCaptureAndEndPhases`
はseed 94で捨札・draw・Joker copyを含む捕獲・終了を、既存の
`CoreContractTests.CardCaptureDiscardsThenDrawsToFour`は開始フェーズを検証する。秘密情報はない。

未解決差分はない。
