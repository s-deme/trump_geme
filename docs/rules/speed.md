# Speed監査記録
資料は[Bicycle: Spit](https://bicyclecards.com/how-to-play/spit)および[Pagat: Spit / Speed](https://www.pagat.com/patience/spit.html)（ともに2026-08-15直接確認）。
|項目|資料|実装・判断|
|---|---|---|
|上下rank/中央pile|標準系|`SpeedGame`確認|
|同時操作・中央更新|両playerが同時に任意の中央pileへ競争して出す|現状はP0/P1の交互優先で、両者が同時に出せる局面の勝者を固定する|
|stock片側枯渇時のspit|自分のdeckがないplayerは新starterを出せない|現状は相手reserveから両方のstarterを補充し得る|
リアルタイム入力の逐次化自体は必要だが、優先権variantと片側枯渇境界が結果を変えるため`RuleSpecific`を維持する。
