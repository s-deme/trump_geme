# Farbwechsel検証仕様

## 資料・採用variant

[ゴクラキズム: Farbwechsel](https://gokurakism.com/farbwechsel/)（参照日: 2026-08-15）の完全規則を採用する。作者の原典は取得できなかったが、この資料は使用札、配札、公開列、秘密bid、play、得点、終了を一貫して定めている。

## 項目別照合

| 項目 | 実装 | 資料根拠 | 判断 |
|---|---|---|---|
| 44枚・3人各11枚・公開11枚 | `StartDeal()`がJoker/2/3を除く4〜Aの44枚を11枚ずつ配り、残りを`trumpCards`へ公開する | 使用札、手札11枚、表向き山札11枚 | 一致 |
| card強度・秘密bid | `Strength()`はA高/4低、`bid`は各playerの0〜11 Actionで、`View`は本人のbidだけを表示する | A最強/4最弱、0〜11を非公開で記録 | 一致 |
| 第1trickとtrump更新 | `LegalActions()`が第1leadを先頭公開札suitへ制限し、以降はwinner lead、`TrickWinner()`が当該公開札suitをtrumpにする | 第1trickのみ公開札suitのマストフォロー、以後winner lead、各札がtrump | 一致 |
| 獲得札・得点 | winnerへtrickと表示札を加え、`FinishDeal()`がexact bid=20、Q/J/10各1点を加算する | 表示札も獲得、exact=20、Q/J/10各1点 | 一致 |
| bid公開・終了 | 11trick後に`revealedBids`へ全bidを保存して公開し、既定100点到達で終了する | 11trick後に予想を公開、最初の100点 | 一致 |

## 正規化・検証・結論

紙への同時秘密bidは、dealer左からの逐次`bid` Actionへ正規化する。各bidは全員が記録し終えるまで本人にのみ見え、完了後は`revealed_bids`として全員へ公開する。`FifthRuleAuditTests`は固定seed完走、bid phase・公開境界、11trick後の全bid公開、二つの相手手札を交換した観測同値性を確認する。

作者の一次資料URLは未取得だが、採用した完全規則とRuntimeに未解決差分はない。`farbwechsel`を`Verified`へ昇格する。
