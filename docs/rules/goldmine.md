# ゴールドマイン検証仕様

## 資料・採用variant

[Tarte Games: Goldmine English Rules](https://tartegames.com/wp-content/uploads/2020/09/Goldmine_EnglishRules.pdf)
（参照日: 2026-08-15）の作者版を採用する。これはTarte Gamesの
[Free2Play紹介ページ](https://tartegames.com/free2play/)にある2人用trick-taking作品である。
[Pagat: Goldmine](https://www.pagat.com/invented/goldmine.html)の52枚・4枚の7収集ゲームは同名の別作品なので
採用しない。

## 項目別照合

| 項目 | 資料 | Runtime | 監査判断 |
|---|---|---|---|
| 人数・札 | 2人。Sの2〜7を6金塊、H/C/Dの2〜7を18枚のplay札 | `prizes`と`hands`/`stock` | 一致 |
| 配札・切札 | 金塊6枚を伏せ列へ、各6枚、残6枚をdraw deck。topを表返しbottomへ置いてtrump表示 | `StartDeal()`、indicatorを`stock[0]`へ残す | 一致 |
| action順 | 前trick loser（初回non-dealer）がinspect/exchange、winner（初回dealer）が残るactionを行う。pass不可 | `a_first`/`a_second`、`firstChoice` | 一致 |
| inspect | 未獲得の任意金塊を秘密に見て戻す | target付き`inspect`、player別`knowledge` | 一致 |
| exchange | 手札1枚を伏せて捨て、draw deck topを取る。indicatorは最後に取る | card付き`exchange`、`Pop(stock)` | 一致 |
| play | 後にactionした側がlead、follow義務なし。lead suit高札、trumpがあれば高trumpが勝つ | `phase == play`、`TrickWinner()` | 一致 |
| 金塊・得点 | deckから最遠の金塊を勝者が公開獲得、rank合計の多い側がdeal勝者。30点短期／50点長期 | `prizeIndex`、`scores`、既定`target_score=30` | 一致 |
| deal/match | 6 trick後、到達前なら次deal | 6枚後の`StartDeal()`、`Result()` | 一致 |

## 正規化と観測境界

金塊列の両端配置は、注入seedでshuffleした`prizes[0..5]`の固定順へ正規化する。draw deckの向きは
indicatorを末尾drawとして保持することで保存する。`target_score`は資料の30/50を含むinstance-local optionで、
既定は短期戦30である。

`View(player)`は自身がinspectした未獲得金塊だけをrankで表示し、相手の`knowledge`、相手手札、
stock順を表示しない。CPUは自分の`knowledge`、公開trump、自手札、合法手だけを使用する。

## 固定seed検証・結論

`FourthRuleAuditTests`はseed 401の完走、seed 414のinspect/exchange入口、seed 424の相手手札・
stockだけを変えた観測同値性を確認する。追加のseed 429はnon-dealer先行、調査結果の本人限定表示、
反対actionの強制、indicatorが最後のdrawに残ること、無followと金塊rank得点を固定する。

資料URL、採用variant、実装、固定seed、観測境界に未解決差分はないため`Verified`とする。
