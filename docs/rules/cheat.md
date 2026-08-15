# Cheat監査記録

資料は[Pagat: Cheat](https://www.pagat.com/beating/cheat.html)（2026-08-15直接確認）のAからKへ昇順申告する基本版を採用候補とした。

一致する範囲は52枚配り切り、伏せ札、指定rankと実札の不一致判定、正しいchallengeなら申告者・誤challengeならchallengerが全pileを取る処理、最終申告がchallengeを生き残って初めて勝つ点である。「any player」のchallenge機会は、情報を増やさず申告者左隣から順次`pass/challenge`するActionへ正規化している。

未解決差分は次のとおり。

- 原典は2～10人だが実装は3～6人だけである。
- 原典の申告は1枚以上で上限を置かず、虚偽なら5枚以上も伏せられる。実装は組合せ爆発を避けて1～4枚へ制限している。
- 上記制限により、手札を5枚以上まとめて捨てる合法なbluffとそのchallenge結果を再現できない。

固定seed 1504は完走するが中核合法手差が残るため、`RuleSpecific`を維持する。
