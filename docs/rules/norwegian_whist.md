# ノルウェージャンホイスト検証仕様

## 出典と採用variant

[Pagat: Two Player Whist](https://www.pagat.com/whist/twoplayer.html)（参照日: 2026-08-15）の
Norwegian/Minnesota Whist系2人variantを採用する。52枚、各10枚手札、8組の伏札＋表札、high/low、
13点先取である。資料は各trickを双方が2枚ずつ出す4枚trickと明記する。

## 項目別照合

| 項目 | 資料 | Runtime照合 | 判断 |
|---|---|---|---|
| 配札 | 各8伏せ＋8表＋10手札 | `StartDeal()`の`layouts`と`hands` | 一致 |
| bid | non-dealerがhigh/low、low時だけdealerが選ぶ | `bid_non_dealer`/`bid_dealer` | 一致 |
| lead | lowはnon-dealer、highはhighを言わなかった者 | `BeginPlay()` | 一致 |
| trick | 交互に各2枚、follow、最高lead suit | `Apply()`と`LegalActions()` | 一致 |
| 表札の裏 | 表札使用時に直ちに伏札を表向きにする | pileのtopを公開 | 一致 |
| 得点・終了 | high/lowの超過trick、13点先取 | `FinishDeal()`、`target_score=13` | 一致 |

## 正規化・観測境界・試験

物理的な4枚trickを4回の逐次`play`へ分解する。Viewは表札、hand count、公開trickだけを表示し、
伏札・相手手札は出さない。`FourthRuleAuditTests`はseed 402で13×4 playと1〜2 bid（53〜54 turn）を
境界確認し、相手手札と伏札を交換しても当事者のView、合法手、CPU選択が等しいことを確認する。
実装修正・未解決差分はなく、`Verified`へ昇格する。
