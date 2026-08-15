# Finesse検証仕様

資料は[ゲームファーム: フィネス](https://gamefarm.jp/rule/finesse.html)（2026-08-15直接確認）。同ページが参照するMiles Edward Allen作4人戦の、初lead suitを必ずtrumpとし42点到達時に高得点teamが勝つvariantを採用する。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| pack/deal | 52枚＋各suitのJ/Q/Kを1枚ずつ加えた64枚。各手札13枚、公開table各3枚 | 複製IDを保った64枚を固定seedで配布 |
| lead/table | lead時だけ自分の手札またはpartnerのtable札を使える。table所有者をleaderとして残り3人がplay | 所有者基準の順番・勝者を検証 |
| trump/follow | 第1trickの最初の札のsuitが固定trump。must-follow、同一札は先出し勝ち | 出典にないAce no-trump分岐を除去 |
| refill | table札使用後、その所有者が手札から1枚を公開補充 | `refill_table`に集約し常時3枚を維持 |
| score | 7～13 trickを2/5/10/20/10/5/2点。残table trump1枚につき3点を引き下限0、その後last trick +4 | last trick点を罰点より後へ修正し独立再計算 |
| game end | 42点以上が一方だけなら高得点team勝利、同点なら継続 | 出典にない5点差/60点条件を除去 |

公開table以外の他者手札はViewに出さず観測同値を確認した。未解決差分はなく、`Verified`とする。
