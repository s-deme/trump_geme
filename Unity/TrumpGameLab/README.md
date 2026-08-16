# Trump Game Lab Unity Product

Crazy Eights製品縦切り版のUnity project。ルールRuntimeは複製せず、
`Packages/manifest.json`からリポジトリ内の`com.trump-game-lab.rules`をlocal UPM packageとして参照する。

## 開く

Unity 2021.3以上でこのディレクトリをprojectとして開く。現在保存済みのEditor versionは
`ProjectSettings/ProjectVersion.txt`を参照する。

## 画面骨格の再生成

Unity menuの`Trump Lab > Regenerate Product Scaffold`を実行すると、7つのscreen Prefabと
`Assets/TrumpLab/Product/Scenes/Bootstrap.unity`を再生成し、build settingsを更新する。
command lineからは次のように実行できる。

```powershell
& <Unity.exe> -batchmode -nographics -accept-apiupdate `
  -projectPath ./Unity/TrumpGameLab `
  -executeMethod TrumpLab.Product.Editor.ProductProjectGenerator.GenerateCommandLine `
  -quit -logFile ./TestResults/product-scaffold-generate.log
```

generatorはscreen component、missing script、Bootstrap root、build scene、`TrumpLab.Core`との
assembly分離を検査し、不整合があれば非0で終了する。

## CPU難易度

Game SettingsではCrazy EightsのCPUを`Easy`、`Standard`、`Hard`の順で選択できる。保存形式と
CLIで使う安定IDは表示順と異なり、`Standard = 1`、`Easy = 2`、`Hard = 3`である。既定値は
従来互換の`Standard = 1`を維持する。

- Easy：全合法手から注入乱数で選択する。
- Standard：M03以前と同じ、playと手札内の多いsuitを優先する方策。
- Hard：自分の手札、公開された手札枚数、捨札、山札枚数だけを評価するbounded heuristic。

選択IDは新規対局のsession設定へ入り、自動保存、再戦、再開、リプレイでも維持される。CPU手番は
`CPU is thinking…`表示から0.35秒待って適用し、画面終了、session終了、再開時は未適用の待機を
cancelする。この待機は演出であり、方策の計算時間には含めない。

ID、観測境界、互換性、強度基準は
[ADR-0004](../../docs/product/decisions/ADR-0004-cpu-difficulty-contract.md)、固定800局の結果と
再現コマンドは[M04 CPU難易度評価](../../docs/product/reports/M04-cpu-difficulty-evaluation.md)を参照する。

## 保存・再開・リプレイ

対局開始時と各Action適用後に同じslotへ自動保存する。アプリを再起動した後はタイトルの
`Saved sessions`を開き、一覧でslotを選択して次を実行できる。

- `Resume`：保存時点の合法手、表示状態、CPU乱数位置を復元して対局を続ける。
- `Replay`：初期条件とAction履歴を再生し、viewer 0に公開可能な保存時点の盤面を読み取り専用で表示する。
- `Delete`：同じボタンをもう一度押して確認したslotだけを削除する。

保存先は`Application.persistentDataPath/TrumpGameLab/Sessions/`で、Productが生成したGUID名の
`.tgs`だけを一覧へ表示する。書込みは同じdirectoryの一時fileを検証してから置換し、更新前の
内容を`.bak`へ残す。破損、改ざん、未知version、再生不一致は自動修復・削除・上書きせず、
画面には安全なエラーだけを表示する。

## 製品テスト

Edit ModeとPlay Modeの製品テストはrepository rootから実行する。

```powershell
pwsh ./scripts/run-product-unity-tests.ps1 -UnityPath <Unity.exe>
```

Edit Modeは設定・難易度・presenter・session・Prefab・atomic保存と破損拒否の契約、Play Modeは
Bootstrapの主要画面遷移、難易度の保存と再戦、CPU待機cancel、二重入力lock、人間対CPUの
1局完走、保存一覧、再開、リプレイ、2段階削除、error modalを検証する。
