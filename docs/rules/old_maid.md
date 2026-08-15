# Old Maid検証仕様

資料は[Bicycle: Old Maid](https://bicyclecards.com/how-to-play/old-maid)（2026-08-15直接確認）の2人以上版を採用する。`omitted_queen_suit`は除くQを決定論的に再現する局所optionで、規則上の任意のQ除外を変えない。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deck/deal | 52枚からQを1枚除いた51枚を1枚ずつ配り切り、人数で手札数が違ってよい | 注入乱数で配札し全playerがpairを除去 |
| pair | 同rank2枚を伏せて捨て、3枚なら2枚だけ、4枚なら2pairを捨てる | rankごとの偶数分を除き奇数なら1枚だけ保持 |
| draw/order | dealerから左へ、伏せて広げた手札の任意位置を左隣が1枚引き、できたpairを捨てて自分の手札を次へ提示 | 残存playerを時計回りに選び、位置を`draw` Action化 |
| end | 全pairを除いた最後のodd queen保持者だけがOld Maid | 固定seedで敗者1人・他全員winnerを確認 |

3人戦で他2人の手札内容を交換してもView・合法手・CPU選択は同値である。採用範囲に未解決差分はなく、`Verified`とする。
