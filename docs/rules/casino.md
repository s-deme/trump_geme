# Casino監査記録
資料は[Pagat: Casino](https://www.pagat.com/fishing/casino.html)（2026-08-15直接確認）のAnglo-American基本版を照合した。
|項目|資料|実装・判断|
|---|---|---|
|pair/sum/build/capture|標準規則|`CasinoGame`確認|
|build所有制約|自分が最後に作成・追加したbuildを残してtrail不可。capture札を手札に保持し続ける必要あり|`CasinoEntry`にownerがなく、常に全hand cardのtrailを許す|
|single/multiple build|既存single buildの値変更、同値set追加、multiple build、他player buildのstealを扱う|現状はloose cardからのsingle build新規作成だけで、増築・multiple buildが未実装|
|score|Most Cards 3、Most Spades 1、A各1、10D 2、2S 1、21点。同点最多は点なし|基本得点は一致。sweepなしvariantを採用可能だがbuild差が残る|
stock順と手札の観測境界、seed 1405完走は確認済み。capture合法手の中核差により`RuleSpecific`を維持する。
