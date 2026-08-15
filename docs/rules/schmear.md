# Schmear検証仕様

資料は[Pagat: Smear](https://www.pagat.com/allfours/schmier.html)（2026-08-15直接確認）。同ページのSt Paul, Minnesota版と、その直下のFive Handed Schmier variantを採用する。別節のMinnesota Smear、Ten Point Smear、Wisconsin版、Fort Frances版は採用しない。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deck/deal | 6人は52枚＋Joker、5人は2・3を除く44枚＋Joker。各6枚、残りはdealerが保持 | 5人stock 15枚、6人stock 17枚を固定seed確認 |
| auction | 3～6を1巡だけbid。全員passは同じdealerが再配布 | dealer不変の再配布境界を専用テスト化 |
| exchange | dealer以外は非trumpを最大3枚捨てて補充。dealerは残りを取り6枚へ戻し、全trump時もA、正J、裏J、Low、Jokerを捨てない | 3枚上限と保護札を`LegalActions()`で固定 |
| partner | 6人は交互3対3。5人は手札にない任意のcardを呼び、捨札ならsolo。partnerは呼札が出るまで秘密 | Aceを含む実pack全体をcall候補にし、`called_card`だけを公開 |
| play | bidder lead、must-follow。A>K>Q>正J>裏J>10…>Low>Jokerのtrump順 | 裏Jをtrump suitとしてfollow/winner判定 |
| score | High、Low（取札でなく保持者）、正J、裏J、Joker、team合計のGame。bid成功は獲得点、失敗は-bid、相手側は常に獲得点。21点同着はbid側優先 | Gameを個人でなくteam合算し、各team memberへ同じ点を反映。target同着winnerもbid側へ限定 |

`target_score`の明示値だけをCLI再現用の短縮戦として認め、既定21点は維持する。固定seed完走、交換・call・再配布境界、相手手札の観測同値を確認した。採用範囲に未解決差分はなく、`Verified`とする。
