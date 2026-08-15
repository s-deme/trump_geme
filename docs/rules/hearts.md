# Hearts監査記録

資料は[Bicycle: Hearts](https://bicyclecards.com/how-to-play/hearts)および[Pagat: Hearts](https://www.pagat.com/reverse/hearts.html)（2026-08-15直接確認）。American 4人版とPagatの3/5人kitty variationを採用候補として照合した。

4人のleft/right/across/hold pass、2 of Clubs lead、must-follow、初trickの失点札禁止、Heart/QSによるbreak、Heart各1・QS 13、shoot the moonで他者+26、100点終了は一致する。3/5人はleft/right/hold、端数kittyを最初の失点trick獲得者へ渡すvariationを採用している。3人kittyに2 of Clubsがあるときは札を移動せず、pass後の最低club保持者をleadに直した。

未解決差分は、実装が6人を通常の1 deck Heartsとして受け付ける一方、Pagatの6人以上は2 deckと同一札cancelを使うCancellation Heartsであり、対応する完全規則と一致しない点である。3人固定seedでkittyの2 of Clubsと最低club lead、秘密手札の観測同値を確認したが、対応人数の中核差が残るため`RuleSpecific`を維持する。
