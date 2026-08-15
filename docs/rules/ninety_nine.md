# Ninety-Nine検証仕様（Verified）

## 資料・採用範囲

- 作者一次資料: [David Parlett: Ninety-Nine](https://www.parlettgames.uk/oricards/ninety9.html)
  （参照日: 2026-08-15）
- 補助資料: [Pagat: Ninety-Nine](https://www.pagat.com/exact/99.html)
  （作者提供の1990年改訂規則を掲載、参照日: 2026-08-15）

3人用のParlett `Junk the Joker` variantを採用する。36枚を使い、作者が明示的に許容する
初回no-trumpを選ぶ。以後は直前ディールの成功者数で切り札を決め、作者ページの完全な
session選択肢である9ディールを既定とする。Pagat掲載の「100点gameを3回行うrubber」は
代替sessionであり採用しない。

`deals`は固定seed試験・CLI試遊用のinstance-local短縮オプションで、既定9だけが採用する
完全sessionである。既存CLI互換用の`target_score`を明示した場合は、Pagatの1ゲーム分と同じ
到達者+100点で終了する短縮戦へ切り替える。この短縮戦は3ゲームrubberではなく、Verifiedの
根拠には用いない。いずれのoptionも他instanceへ残らない。

## 項目別照合

| 項目 | 作者規則 | Runtime・判断 |
|---|---|---|
| 人数・カード | 3人、A-K-Q-J-10-9-8-7-6の36枚。Junk the JokerではJokerなし | 3人固定、`StandardDeck`のA・6～Kを1組使用。一致 |
| deal・席順 | 各12枚。dealとplayは左へ移る | `StartDeal()`が1枚ずつ12巡配り、dealerを毎deal左へ移す。一致 |
| 秘密bid | 各自3枚を伏せ、D/S/H/Cを0/1/2/3として合計0～9を表す | `set_bid_card`を3回行い、残り9枚をplayする。rankをbid値に使わない。一致 |
| premium手順 | dealer左からdeclare/revealを申し出る。revealはdeclareに優先し、同levelは早い席が優先。先のdeclareは後のrevealへraise可 | `premium` phaseの`declare`、`reveal`、`pass_premium`で正式手順を逐次化。優先・raiseを専用テストで確認。一致 |
| premium公開 | declareはbid-card、revealはbid-cardと残り手札を開始前に公開 | `bids`と`open_hand_Pn`を全viewerへ公開。一致 |
| trump | 採用variantは初回no-trump可。以後、成功者3/2/1/0人でC/H/S/D | `StartDeal(previousSuccesses)`が同じ写像を適用。一致 |
| trick | dealer左がlead。must-follow、voidなら任意。lead suit最高またはtrump最高が勝ち、勝者lead | `LegalActions()`だけがfollow制約を決め、`Apply()`と`TrickWinner()`が勝者・次leadを更新。一致 |
| claim公開 | 成功者はbid-cardを表にして証明し、失敗者は見せなくてよい | 精算時に成功者のbid値だけを`revealed_bids`へ保存し、失敗者は`hidden`。次deal開始後もclaimを確認できる。一致 |
| deal得点 | trick各1点。成功者は成功人数3/2/1人に応じ10/20/30点。成功者0人はbonusなし | `FinishDeal()`のtrick点と`contractPoints`が一致 |
| premium得点 | declare 30、reveal 60。成功なら本人、失敗なら各相手へ加算 | `premiumHolder`の成否で本人または相手2人へ同額を加算。一致 |
| session | 9ディール、または9の倍数を行い合計最高点 | 既定`deals=9`で終了し、`Result()`は合計最高点（同点は複数winner）を返す。一致 |
| 乱数・観測 | bid-card、相手手札、山順は規則上非公開 | 注入された`DeterministicRandom`だけでshuffle。Viewは自己bid／premium公開分／成功claimだけを示し、CPUは自己手札と公開状態だけを使う。一致 |

## 逐次入力への正規化

紙上の秘密bidはdealer左から各人3回の`set_bid_card`へ分解する。各人が選べる3枚の組合せは
失われず、完了までは本人以外へカードも値も出さない。premiumの同時意思表示は作者が示す
正式手順どおりdealer左からの逐次Actionにし、revealの上位性と早い席のraise権を保存する。
成功claimはカード画像ではなくスート値の合計を公開するが、契約成否の検証に必要な情報は
失われない。

## 専用試験

`SixthRuleAuditTests`は次を固定seedで確認する。

- seed 601で既定9ディールを完走し、終了理由と`deal=9/9`を確認
- seed 606～615で実際に選んだbid、獲得trick、`revealed_bids`を照合し、成功公開と失敗非公開の両境界を確認
- seed 616で成功者数から次dealのC/H/S/Dを確認
- seed 617でdeclare、後席reveal、先席raiseの優先順とopen hand公開を確認
- seed 607で相手2人の手札だけを交換してもviewerのView・合法手・CPU選択が同一であることを確認
- seed 608で既存`target_score`短縮戦の受理と終了を確認

資料、採用variant、Runtime、固定seed、観測境界に未解決差分はないため、`ninety_nine`を
`Verified`とする。
