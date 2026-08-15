# Toepen検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Toepen](https://www.pagat.com/last/toepen.html)の4枚・no-trump版を採用する。

- hand交換後は交換者以外の全員へ順番にchallenge機会を与え、公開した4枚の条件でpenaltyを処理する。
- 各trick前にactive player全員へknock/fold機会を与える。会話的同時行動は座席順の逐次Actionへ正規化する。
- fold済みの札がtrickを取った場合も、次のactive playerへleadを移す。

`TwentiethRuleAuditTests`は全3対戦相手のchallengeと全4人のknock offerを確認し、固定seed完走も行う。未解決差分はない。
