# Cheat検証仕様

状態は`Verified`。参照日は2026-08-15。採用variantは[Pagat: Cheat](https://www.pagat.com/beating/cheat.html)のAからKへ昇順申告する2～10人版である。

- 52枚を配り切り、手札から1枚以上の任意枚数を逐次選択して伏せて申告する。組合せ全列挙はせず、`select_claim_card`と`finish_claim`へ正規化する。
- 他playerは申告者の左から順に`pass/challenge`し、正しいchallengeなら申告者、誤りならchallengerがpileを取る。
- 最終申告は全challenge機会を通過して初めて勝利となる。

`TwentiethRuleAuditTests`は5枚申告、challenge段階、2～10人境界と固定seed決定性を確認する。秘密の実札は公開せず、未解決差分はない。
