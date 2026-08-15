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

## 構造化表示

構造化表示に対応したゲームは`IGamePresentationProvider`も実装します。`Present(viewer)`が返す
スナップショットだけを使うと、`View()`の表示文字列を解析せずに盤面と操作候補を構築できます。
viewerはplayer indexで指定し、省略時は現在手番です。

```csharp
using System.Linq;
using TrumpLab;

IGame game = BuiltInGames.Registry.Create("crazy_eights", players: 2, seed: 10);

if (game is IGamePresentationProvider provider)
{
    const int humanPlayer = 0;
    GamePresentation screen = provider.Present(humanPlayer);

    CardZonePresentation hand = screen.CardZones.Single(zone =>
        zone.Role == "hand" && zone.OwnerPlayer == humanPlayer);
    CardZonePresentation stock = screen.CardZones.Single(zone => zone.Id == "stock");
    CardZonePresentation discard = screen.CardZones.Single(zone => zone.Id == "discard");

    // FaceUpだけCardsを描画する。FaceDownはCount枚の裏面、CountOnlyは枚数だけを描画する。
    // 非手番viewerと終了後のActionsは空になる。
    if (screen.Actions.Count > 0)
    {
        ActionPresentation selected = screen.Actions[0];
        game.Apply(selected.Action);
        screen = provider.Present(humanPlayer);
    }
}
```

`ActionPresentation.Id`は現在のスナップショット内だけで有効です。入力時はdescriptorが保持する
`Action`をそのまま`IGame.Apply()`へ渡し、画面のラベルやカード文字列からActionを再構築しません。
ゲーム進行後は古いスナップショットを捨て、`Present()`を再取得してください。Crazy Eightsでは
手札、山札、捨札、指定suit、手番、終了結果を取得できます。他playerの手札はカード値を含まない
`FaceDown`、山札は順序を含まない`CountOnly`として返ります。
