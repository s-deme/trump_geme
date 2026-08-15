# Pinochle監査記録

資料は[Pagat: Single Deck Partnership Pinochle](https://www.pagat.com/marriage/pinmain.html)（2026-08-15直接確認）の4人固定pair・auction版。A-10-K-Q-J-9各2枚、12枚配札、競り、meld、must-follow／must-trump、契約精算の骨格を照合した。

未解決差分は次のとおり。

- 参照版のRacehorseでは落札者とpartnerがcardを往復passするが、実装にpass phaseがない。
- trump 9のDix meldが得点表にない。
- trump lead時に勝てるtrumpを持つなら上回る義務があるが、現在の`LegalActions()`は任意のtrumpを許す。
- 参照版のbid上限・session 1500を、実装は20～59・既定150へ縮尺しているが、その完全な換算表を実装・文書で確定していない。

固定seed 1604は完走するが、競り後交換・合法手・得点差が残るため`RuleSpecific`を維持する。
