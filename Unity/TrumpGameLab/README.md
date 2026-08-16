# Trump Game Lab Unity Product

Crazy Eights製品縦切り版のUnity project。ルールRuntimeは複製せず、
`Packages/manifest.json`からリポジトリ内の`com.trump-game-lab.rules`をlocal UPM packageとして参照する。

## 開く

Unity 2021.3以上でこのディレクトリをprojectとして開く。現在保存済みのEditor versionは
`ProjectSettings/ProjectVersion.txt`を参照する。

## 画面骨格の再生成

Unity menuの`Trump Lab > Regenerate Product Scaffold`を実行すると、6つのscreen Prefabと
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

Edit Modeは設定・presenter・session・Prefab・atomic保存と破損拒否の契約、Play Modeは
Bootstrapの主要画面遷移、二重入力lock、人間対CPUの1局完走、再戦、保存一覧、再開、
リプレイ、2段階削除、error modalを検証する。
