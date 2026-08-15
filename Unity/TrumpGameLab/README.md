# Trump Game Lab Unity Product

Crazy Eights製品縦切り版のUnity project。ルールRuntimeは複製せず、
`Packages/manifest.json`からリポジトリ内の`com.trump-game-lab.rules`をlocal UPM packageとして参照する。

## 開く

Unity 2021.3以上でこのディレクトリをprojectとして開く。現在保存済みのEditor versionは
`ProjectSettings/ProjectVersion.txt`を参照する。

## 画面骨格の再生成

Unity menuの`Trump Lab > Regenerate Product Scaffold`を実行すると、4つのscreen Prefabと
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
