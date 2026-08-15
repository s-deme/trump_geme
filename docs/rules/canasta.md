# Canasta監査記録

資料は[Pagat: Canasta](https://www.pagat.com/rummy/canasta.html)および[Bicycle: Canasta](https://bicyclecards.com/how-to-play/canasta)（2026-08-15直接確認）のClassic 4人固定pair版。108枚、11枚配札、自然札2枚以上＋wild最大3枚、赤3、初回meld下限、自然／混成Canasta、5000点sessionの骨格は一致する。

未解決差分は次のとおり。

- 初回表札がwildまたは赤3のとき、原典は追加表札をめくってpileをfreezeするが実装は1枚のまま止める。
- 原典は初回meldの合法な組合せをplayerが選ぶが、実装の`initial_meld`は内部で高得点群を自動選択する。
- 原典はpartnerに上がり許可を尋ね、その回答に拘束されるが、そのActionがない。
- concealed going-out等のbonusとblack threeの上がり時meldが未実装である。

固定seed 1603は完走するが、合法選択・上がり・得点差が中核に残るため`RuleSpecific`を維持する。
