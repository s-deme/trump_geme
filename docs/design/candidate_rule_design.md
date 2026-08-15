# 候補ルール設計

候補名、人数、系統、出典、実装IDを`Candidate`で保持する。`ImplementationId`は
Registryから生成できることだけを表し、正式ルール準拠の完成判定には使用しない。
現在92候補すべてを生成でき、全件がゲーム固有の状態機械を使用する。
`CandidateStatus.RuleSpecific`は専用の合法手・状態遷移・終了・得点を持つことを表すが、
外部ルールとの項目別照合を終えた`Verified`とは区別する。現在の92件はすべて
生成でき、ゲーム固有の状態機械を使用する。うち`Verified`は個別監査を完了した
`trump_crew`、`baohuang`、`napoleon`、`card_capture`、`scoundrel`、`gosankyo`、
`german_whist`、`gin_rummy`、`sono`、`crisp`、`cribbage`、`super_trump`、`daifugo_two`、
`briscola`、`bohemian_schneider`、`durak`、`officer_skat`の17件のみであり、残る75件は`RuleSpecific`である。
`RuleSpecific`は正式ルール照合済みを意味しない。照合の対象、採用バリアント、正規化の
判断は、Verified候補ごとの`docs/rules/<game-id>.md`を正本とする。

`docs/rules/candidate-rules.md`は候補の暫定プロダクト仕様と監査台帳であり、個別の正式照合書の
代わりにはならない。`CandidateRuleGames`のプロファイルは候補メタデータと後方互換用の登録
フォールバックとして残すが、現在の92候補生成では`RuleDrivenGame`を使用しない。
