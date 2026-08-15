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

```csharp
using TrumpLab;

IGame game = BuiltInGames.Registry.Create("crazy_eights", players: 4, seed: 10);
IReadOnlyList<TrumpLab.Action> actions = game.LegalActions();
game.Apply(actions[0]);
```
