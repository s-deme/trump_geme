# Dubito監査記録

状態は`Verified`。資料は[ゴクラキズムの完全規則](https://gokurakism.com/dubito/)
（参照日: 2026-08-15）。1手番1枚の標準Dubitoを採用し、複数枚を置けるSubito variantは採用しない。

| 項目 | 完全規則 | `DubitoGame` |
|---|---|---|
| deck・配札 | Jokerなし2組104枚、1～4人へ各8枚 | 同じdeckからround-robinで8枚ずつ配る |
| 個人4列 | 1=異suit可の厳密昇順、2=同suit、3=同suit厳密昇順、4=同rank | `CanPlace()`の4条件と一致 |
| 手番・停止 | 1枚置いて1枚補充。置けないplayerは終了 | `place`後にstockから補充、合法配置が0件なら`stop`のみ |
| 終了・得点 | 全員停止またはstock消尽。各列の枚数を1/2/3/4倍し、最高点 | 同じ終了条件と重み付き合計 |

`EighthRuleAuditTests`はseed 803で決定論的に完走し、seed 1003では秘密stockの先頭2枚を交換しても
View・合法手・CPU選択が同値であることを確認する。CPUが参照するのは自分の手札、個人列、公開された
stock枚数だけである。未解決差分はない。
