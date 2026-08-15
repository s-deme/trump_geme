# Trump Game Lab Rules

Unity向けの決定的トランプルールエンジンです。Runtimeは`UnityEngine`へ依存しないため、
Edit Mode、Play Mode、ヘッドレスCLIで同じ状態遷移を使用できます。

## Unityプロジェクトから参照

Package Managerの「Add package from disk」でこのディレクトリの`package.json`を選ぶか、
プロジェクトの`Packages/manifest.json`へローカルパスを追加します。

```json
{
  "dependencies": {
    "com.trump-game-lab.rules": "file:../../trump_geme/Packages/com.trump-game-lab.rules"
  },
  "testables": [
    "com.trump-game-lab.rules"
  ]
}
```

`testables`を追加すると、Unity Test RunnerのEdit Modeにパッケージ内契約テストが表示されます。

リポジトリの検証用テンプレートから一時プロジェクトを生成し、WindowsでEdit Modeテストを
実行する場合は、インストールしたEditorのパスを指定します。テスト結果とログは
`TestResults/`へ出力されます。

```powershell
pwsh ./scripts/run-unity-tests.ps1 `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\2021.3.48f1\Editor\Unity.exe"
```

既定の`-Mode Fast`は広域シミュレーションを除外する。完了時の互換確認には
`-Mode Standard`、30 seedの全登録ゲーム試験を含む定期回帰には`-Mode Full`を指定する。
全件実行の上限は40分。調査時は`-TestFilter <完全修飾テスト名>`で対象を限定でき、
カテゴリとの両方に一致するテストだけを実行する。

```powershell
pwsh ./scripts/run-unity-tests.ps1 -UnityPath <Unity.exe> -Mode Standard
pwsh ./scripts/run-unity-tests.ps1 -UnityPath <Unity.exe> -Mode Full
```

```csharp
using TrumpLab;

IGame game = BuiltInGames.Registry.Create("crazy_eights", players: 4, seed: 10);
IReadOnlyList<TrumpLab.Action> actions = game.LegalActions();
game.Apply(actions[0]);
```
