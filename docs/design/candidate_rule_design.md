# 候補ルール設計

候補名、人数、系統、出典、実装IDを`Candidate`で保持する。`ImplementationId`は
Registryから生成できることだけを表し、正式ルール準拠の完成判定には使用しない。
現在92候補すべてを生成でき、全件がゲーム固有の状態機械を使用する。
`CandidateStatus.RuleSpecific`は専用状態機械だけが完成した中間状態、`Verified`は外部規則との
項目別照合・採用variant・正規化・固定seed監査まで完了した状態を表す。現在の92件はすべて
個別監査を完了した`Verified`であり、`RuleSpecific`と`Prototype`は0件である。Verified IDの
完全な一覧は`docs/rules/candidate-rules.md`と`GameCatalogue`を正本とする。
`RuleSpecific`は正式ルール照合済みを意味しない。照合の対象、採用バリアント、正規化の
判断は、Verified候補ごとの`docs/rules/<game-id>.md`を正本とする。

`docs/rules/candidate-rules.md`は候補の暫定プロダクト仕様と監査台帳であり、個別の正式照合書の
代わりにはならない。`CandidateRuleGames`のプロファイルは候補メタデータと後方互換用の登録
フォールバックとして残すが、現在の92候補生成では`RuleDrivenGame`を使用しない。
