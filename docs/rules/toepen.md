# Toepen監査記録
資料は[Pagat: Toepen](https://www.pagat.com/last/toepen.html)（2026-08-15直接確認）。公開完全規則は実在する。
|項目|資料|実装・判断|
|---|---|---|
|4枚/no-trump/knock|標準規則|`ToepenGame`確認|
|challenge|任意playerが交換した4枚を表にしてchallengeできる|現状は交換者の次playerだけに二択を与える|
|knock/fold|手の取得後なら誰でもtrick途中を含めknock可。fold済みの札がtrickを取った場合は次のactive playerがlead|現状は手番playerだけがknockでき、fold札がwinnerになるとfold済みplayerへ手番を戻し得る|
会話行動は逐次Actionへ正規化できるが、上記は合法手と進行の中核差である。seed 1301の現行variant完走だけを確認し、`RuleSpecific`を維持する。
