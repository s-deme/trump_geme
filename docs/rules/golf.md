# Golf検証仕様

資料は[Pagat: Golf](https://www.pagat.com/draw/golf.html#six)（2026-08-15直接確認）のSix-card Golf基本版を採用する。18 hole、Joker、終了後の他player追加1手などのvariationは採用しない。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| pack/deal | 2～4人は52枚、5～8人は2組。6枚を2×3へ伏せ、各自任意の2枚を公開 | 対応範囲2～6人。dealer左から1枚ずつ配り、9dealでdealerを交代 |
| turn | 山札か公開捨て札の先頭を取って任意の1枠と交換する。山札から取った札だけは直接捨てられる | 捨て札取得後の`discard_drawn`を禁止し、交換後の札を表向きにする。山切れ時は残る捨て札取得だけを合法化 |
| end/score | 誰かの6枠がすべて表になった時点で即終了。A=1、2=-2、3～10=額面、J/Q=10、K=0、同rank縦pair=0 | 各holeの全layoutを採点し、9hole累計最少をwinnerとする |
| observation | 表向きlayoutと捨て札は公開、伏せlayout・山順・山から引いた札は非公開 | 全公開layoutを全viewerへ表示し、山からの`drawn`は手番playerだけに表示。stock順交換でもView・合法手・CPUは同値 |

固定seed 1501/1510/1580で完走、捨て札強制交換、公開盤面、秘密山札境界を確認した。採用範囲に未解決差分はなく、`Verified`とする。
