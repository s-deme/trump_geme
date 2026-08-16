# M04 Crazy Eights CPU難易度評価

## 再現条件

- 評価日：2026-08-16
- game：2人Crazy Eights
- game seed：`44000`～`44199`
- pair：Standard対Easy、Hard対Standard
- 席順：各seedで強い側をseat 0とseat 1へ入れ替える
- policy乱数：difficultyごとに独立し、席を替えても同じseed系列を使う
- 局数：pair当たり400局、合計800局
- turn limit：1局`50000`
- 強度基準：強い側のscore率`53%以上`。勝ち1点、draw 0.5点
- 性能基準：Release製品目標`15秒以下`、Debug/共有CI hard limit`30秒以下`

次のコマンドは決定的集計、強度基準、性能上限をまとめて検証し、同じ形式のreportを標準出力する。

```powershell
dotnet test tests/TrumpLab.Tests/TrumpLab.Tests.csproj -c Release `
  --filter FullyQualifiedName~CpuDifficultyEvaluationTests `
  --logger "console;verbosity=detailed"
```

## Reference結果

Windows上の.NET 8 Releaseで得たreference結果は次のとおり。経過時間は環境ごとに再測定されるが、
`stable`行はfixtureに固定され、1件でも変われば回帰testが失敗する。

| pair | 強い側seat 0 W/L/D | 強い側seat 1 W/L/D | 平均turn | 強い側score率 | 判定 |
|---|---:|---:|---:|---:|---|
| Standard > Easy | 193/7/0 | 185/15/0 | 55.205 | 94.50% | Pass |
| Hard > Standard | 128/72/0 | 100/100/0 | 28.535 | 57.00% | Pass |

- 完了：800/800局
- 失敗・turn limit超過：0件
- 評価suite：501.524 ms（15秒目標、30秒hard limitともPass）
- Hard固定observation 200件：p95 0.012 ms、最大0.152 ms（5 ms/25 msともPass）
- 決定的集計：
  `standard>easy|games=400|seat0=193-7-0|seat1=185-15-0|turns=22082;hard>standard|games=400|seat0=128-72-0|seat1=100-100-0|turns=11414;failures=0`

このscore率は固定corpusに対する回帰基準であり、一般プレイヤー母集団への勝率保証ではない。
