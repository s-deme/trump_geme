# 五行相克検証仕様

資料は[ゴクラキズム: 五行相克](https://gokurakism.com/gogyo/)（2026-08-15直接確認）。公開ページの5人・5deal規則を採用する。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deal/start | 各10枚、余り2枚を順に公開。1枚目のsuitを第1trickの仮lead suitとする | dealer左が可能なら仮lead suitを出す |
| rank/points | spade固定trump、A high、must-follow。A/K/Q/J/10が各1点 | 公開点札も第1trick勝者のcapturedへ加える |
| one partner | 公開2枚の点札が0～1枚なら、自分の右隣のさらに右隣1人が一方向partner | playが左回りの座席表現なので`player+3`へ修正し独立採点 |
| two partners | 公開2枚とも点札なら、両隣以外の2人が一方向partner | `player+2`と`player+3`を合算 |
| score/session | 1人戦は自点がpartner以下なら自点、超過なら差を失点。2人戦は3人合計と12の絶対差を失点。5deal最高 | 1人戦fixed seedの全50枚を独立再計算 |

公開kitty・獲得点以外の他者手札は隔離され、観測同値も成立する。未解決差分はなく、`Verified`とする。
