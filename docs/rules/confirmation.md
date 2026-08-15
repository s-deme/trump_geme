# Confirmation監査記録

状態は`Verified`。資料は[ゴクラキズムの完全規則](https://gokurakism.com/confirmation/)
（参照日: 2026-08-15）。同ページの4人規則を採用し、掲載プレイ例どおりdealerを一巡する4局を既定とする。

| 項目 | 完全規則 | `ConfirmationGame` |
|---|---|---|
| deck・強さ | K/Q/Jを除くA～10の40枚、10 high・A low、各10枚 | 一致 |
| trick | dealer lead、must-follow、no-trumpを9trick | 一致 |
| 公開保護 | 唯一のfollow札を公開してoff-suitを出せる。公開札は変更不可 | `protect_and_play`で唯一札を固定し、以後も同じ札だけを保護 |
| 目標・得点 | 最後の1枚のrank（A=1、10=0）。各勝1、秘密的中+10、公開的中+5 | 一致 |
| session | 事前合意局数 | 既定4局、`deals`で局所指定 |

`NinthRuleAuditTests`はseed 903、920～940で公開保護の全viewer反映、残札bid、得点を独立集計し、
seed 962で相手2手札交換後の観測同値を確認する。未解決差分はない。
