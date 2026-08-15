# クラバヤス検証仕様

## 資料・採用variant

[ゴクラキズム: 2人用トリックテイキング クラバヤス](https://gokurakism.com/klabberjass/)（参照日:
2026-08-15）の2人用・32枚・500点版を採用する。補助資料は
[Pagat: Klabberjass](https://www.pagat.com/jass/klabberjass.html)（同日参照）で、6枚配札後の入札と
3枚補充を照合した。4枚以上を一律50点、完全同値のsequenceは双方無得点とする前者を正本とする。
[CardRules+](https://cardrulesplus.com/games/klabberjass/)の5枚以上100点・forehand優先系統は別variantとして
採用しない。

## 項目別照合

| 項目 | 資料 | Runtime | 監査判断 |
|---|---|---|---|
| 人数・札 | 2人、7〜Aの32枚 | `KlaberjassGame` | 一致 |
| 配札・再配札 | 3枚2 packetで各6枚、表札後の入札確定で各3枚を補充して9枚 | `StartDeal()`、`CompleteDeal()` | 一致 |
| 入札 | elderから候補suitのtake/pass、全pass後は任意suitを順に指定、全passはdealer交代redeal | `bid`/`bidStep` | 一致 |
| 7交換 | 表札が切札なら切札7と開始前に交換できる | `exchange_seven` | 一致 |
| meld | 3枚=20、4枚以上=50。点数区分、最高rank、切札の順に優劣を決め、完全同値は双方0。勝者は全sequenceを公開し得点 | `declare_meld`、`meld_reply`、`declare_meld_high`、`declare_meld_trump`、`FinishMeld()` | 一致 |
| play | follow、void時trump、trump lead又はtrump応答時は可能ならovertrump | `FollowCards()`、`TrickWinner()` | 一致 |
| 特殊得点 | trump J=20、9=14、K/Qの2枚目にbella 20、最終trick 10 | `CardPoint()`、`play_bella`、最終trick処理 | 一致 |
| maker精算 | maker勝ちは各自の点、同点はmaker 0/defender自点、maker負けはdefenderが合計点 | `FinishDeal()` | 一致 |
| match | 500点到達時に最多点者が勝者 | `target_score=500`、`Result()` | 一致（同点は`IGame`の複数winner正規化） |

## Action正規化と観測境界

会話のmeld照合は次の逐次Actionに正規化する。`declare_meld`は公開する20/50だけを`Value`に持ち、
`skip_meld`は任意の不申告である。対抗者は公開済みの比較項目だけで`meld_reply`（`win`、`lose`、`tie`）を返す。
同値なら先の宣言者が`declare_meld_high`、続いて`declare_meld_trump`を返し、完全同値でなければ勝者の
全sequenceを公開する。`View()`は途中では点数区分・応答・公開済み比較項目だけを表示し、
勝敗確定後だけ`meld_reveals`へ全sequenceを載せる。

sequenceにはtrick強度と独立した自然順7-8-9-10-J-Q-K-Aの`SequenceStrength()`を使う。
CPUは自手札と`LegalActions()`に含まれる公開済み比較結果だけで選び、相手手札・stock順・未公開meldの
rank/suitを参照しない。初回dealerのcutはseed付き初期席に正規化し、`target_score`は既定500の
instance-local test optionである。

## 固定seed検証・結論

`FourthRuleAuditTests`はseed 401の完走、seed 411の6→9枚境界、seed 421の相手手札・stockだけを
変えた観測同値性を確認する。追加のseed 427/428は5枚sequenceの50点、比較前の非開示、勝者の公開、
20点同値から最高rank・切札照合を経た双方無得点を固定する。

資料URL、採用variant、実装、固定seed、観測境界に未解決差分はないため`Verified`とする。
