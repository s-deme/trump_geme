# 七並べ検証仕様

資料は[トランプスタジアム: 七並べ](https://playingcards.jp/m/game_rules/modal/sevens_rules_modal.html)（2026-08-15直接確認）のジョーカーなし・A/K非接続・パス上限3回版を採用する。PagatのFan Tan基本版やJoker/A-K variationは採用しない。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| setup | 3～8人へ52枚を配り切り、4枚の7を最初から公開。Diamond 7所持者から時計回り | 全7を各手札から除き、注入乱数によるDiamond 7所持者を開始playerにする |
| play/pass | 同suitで7から連結する直隣rankを1枚出す。合法札があっても戦略的pass可、3回まで | `play`と任意`pass`を`LegalActions()`だけで列挙し、使用済みpassをplayer別に保持 |
| elimination | 4回目のpassで失格し全手札を所定位置へ公開。ただし7から孤立した公開札は間のrankを飛び越して次を解放しない | 4回目を`bankrupt`へ正規化。公開済みrankと7から連結した上下端を別管理し、孤立区間を固定seedで確認 |
| ranking | 非失格者は上がり順、全非失格者より失格者が下位で、先に失格した者ほど下位 | 上がり順と失格順を別記録し、全player終了時に順位へ確定 |

3人戦で他2人の手札を交換してもView・合法手・CPU選択は同値である。固定seed 1502/1520～1599/1581で完走と3回pass境界を確認し、採用範囲に未解決差分はないため`Verified`とする。
