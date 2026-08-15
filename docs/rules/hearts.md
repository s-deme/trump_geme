# Hearts検証仕様

状態は`Verified`。参照日は2026-08-15。[Bicycle: Hearts](https://bicyclecards.com/how-to-play/hearts)のAmerican 4人版、[Pagat: Hearts](https://www.pagat.com/reverse/hearts.html)の3/5人kitty版と6人Cancellation Heartsを採用する。

- pass方向、2 of Clubsまたは最低clubの初lead、must-follow、初trick失点札禁止、heart breakを人数variantごとに処理する。
- heart各1、QS 13、shoot the moon 26、100点境界を累積する。3/5人の端数kittyは最初の失点trickへ渡す。
- 6人は2 deckを使い、同一カードが同trickへ出た組をcancelして残る最高札を勝者とする。

`TwentiethRuleAuditTests`は6人102枚・17trickと全52失点、既存監査は3人kitty、pass、秘密情報の観測同値を確認する。未解決差分はない。
