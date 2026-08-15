# 将校スカート検証仕様

## 出典と採用variant

統一公式ルールの発行元は確認できない。このため、公開／伏札、Jのtrump順位、得点を明記する
[KDE LSkat Offiziersskat manual](https://docs.kde.org/stable_kf6/de/lskat/lskat/lskat.pdf)と、
宣言者によるsuit trump選択・配札順を記す
[Skat Palast Offiziersskat](https://www.skat-palast.de/offiziersskat/)を信頼できる採用資料とした。
参照日は2026年8月15日。採用variantは2人・32枚・各8組の伏札＋表札・Suit gameのみ・Kontra/Reなし・
single-deal card-point精算版である。Grand、Ramsch、Null、Skat式matador精算は採用しない。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2人 | Registryと`OfficerSkatGame`は2人専用。 |
| 使用カード | 7～Aの32枚 | 同rank集合の32枚を使う。 |
| 配札 | 各4組を2列、各組は伏札の上に表札、計16枚 | 8 piles×2札を`layout`へ作る。 |
| 開始状態 | non-dealer／forehandが自分の初期表札からsuit trumpを選びlead | P0=forehandとして`choose_trump`後にP0 leadへ正規化する。 |
| 全フェーズ | trump選択、16 trick、表札の下札公開、card-point精算 | `choose_trump`、`play`、pilesのpop、最終trick終了で遷移する。 |
| 合法手 | 自分の表札のみ。可能ならeffective suitをfollow | `Available()`と`LegalActions()`が対応する。 |
| 特殊札・例外 | J4枚は常にtrumpでC＞S＞H＞D。Jは通常suitでなくtrumpとしてfollowする | `EffectiveSuit()`と`Power()`が順位・followを実装する。 |
| 勝敗 | 120 card point中61以上。60-60の扱いは採用variantで明記 | P0が60超ならP0、そうでなければP1（defender優先）とする。 |
| 得点 | A=11、10=10、K=4、Q=3、J=2 | `CardPoint()`が同じ値を加算する。 |
| 終了条件 | 16 trick終了 | 全pile空で終了する。 |
| ローカルルール | Grand／Ramsch／Null、Kontra/Re、matador精算は地域差 | Suit game・単局card-point精算・Kontra/Reなしを固定する。 |

## CLI/Unityへの正規化

物理的な配札順はすべて最初に注入rngで確定しても、trump選択前のViewではP0の初期4表札だけを見せる。
宣言後には表札を公開し、下札は対応する表札が出るまで`?`のままとする。Kontra/Reを採用しないため、
早期に残り列を確定しても失われる選択肢はない。

## 実装・テスト・差分

`OfficerSkatGame`は採用variantとの差分なし。
`ThirdRuleAuditTests.OfficerSkatFixedSeedPlaysAllTricksAndJacksFollowAsTrump`はseed 307/308で
16 trick・120点・Jのtrump follow例外を確認する。同
`OfficerSkatViewAndCpuIgnoreFaceDownCards`は相手の伏札を変えてもView、合法手、CPU選択が観測同値であることを確認する。

未解決差分はない。
