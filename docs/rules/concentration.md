# 神経衰弱検証仕様

資料は[Bicycle: Concentration](https://bicyclecards.com/how-to-play/concentration)（2026-08-15直接確認）の標準52枚版を採用する。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| setup | 52枚を混ぜ、重ならないよう全札を伏せて配置。開始playerは任意 | 注入乱数だけで52位置を作り、開始をP0へ決定論的に正規化 |
| turn | 異なる位置を2枚順に公開。同rankならpairを獲得して続行、不一致なら一定時間公開後に伏せて左隣へ | 位置番号`flip`を2回入力し、全viewerが2枚を見られる`resolve/continue` Actionを5秒待機の代わりに置く |
| memory | 既に公開された位置とrankは全playerが観測可能 | CLI/UIの履歴とCPUの`knownRanks`だけを利用し、未公開layoutをCPU判断へ使わない |
| end/tie | 全pair取得後、獲得pair最多者がwinner | 同数最多は全員winner。固定pairで得点と再手番を確認 |

未公開layout順を交換しても開始View・合法手・同一乱数CPU選択は同値である。固定seed 1503/1530/1582で完走し、採用範囲に未解決差分はないため`Verified`とする。
