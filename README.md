# Trump Game Lab

トランプゲームをCPUと遊び、さまざまなルールを試せるゲーム集です。
画面を操作して遊ぶUnity版では **Crazy Eightsの2人対戦（自分とCPU）**、ターミナルで遊ぶCLI版では **92種類のゲーム**を利用できます。

Unity版は開発中で、配布前の最終動作確認が残っています。このページでは、リポジトリから起動する方法を案内します。

## できること

- **CPUと対戦**：Unity版のCrazy Eightsでは、Easy・Standard・Hardの3段階から難易度を選べます。ワイルドカードのランクも変更できます。
- **遊びながらルールを覚える**：チュートリアルで基本操作を体験し、対局前に遊び方を読み返せます。
- **途中から再開する**：通常の対局は自動保存され、保存した対局の続きを遊べます。
- **保存した盤面を確認する**：Replayで保存時点の盤面を閲覧できます。コマ送りや動画のような自動再生には対応していません。
- **遊びやすさを調整する**：画面表示、音量、操作割り当て、演出速度を変更できます。日本語・英語、文字サイズ、高コントラスト、動きを抑える設定にも対応しています。
- **多くのゲームを試す**：CLI版では、Crazy Eights、ハーツ、スペード、ジンラミー、ブラックジャックなどを遊べます。CPU同士の対戦結果を集計し、CSV・JSONで比較結果を保存することもできます。

各ゲームで採用しているルールや人数は、[ゲーム一覧とルール](docs/rules/candidate-rules.md)で確認できます。

## Unity版を起動する

必要なものは **Unity Hub** と **Unity 6.3 LTS（6000.3.22f1）** です。

1. このリポジトリ全体を取得し、Unity Hubで `Unity/TrumpGameLab` フォルダを開きます。
2. `Assets/TrumpLab/Product/Scenes/Bootstrap.unity` を開きます。
3. Unity EditorのPlayを押します。
4. タイトルの `Tutorial` で操作を覚えるか、`Play` から対局を始めます。

マウス、矢印キーとEnter／Space、ゲームパッドで操作できます。Escapeで戻り、F1でヘルプを開けます。
CPUの難易度などは対局前の `Game Settings`、画面・音・操作・言語などは `Settings` で設定します。
日本語表示には対応する日本語フォントが必要です。利用できるフォントがない場合は英語表示になります。

### 保存した対局を開く

タイトルの `Saved sessions` で対局を選びます。

- `Resume`：保存したところから再開します。
- `Replay`：保存時点の盤面を閲覧します。
- `Delete`：もう一度同じボタンを押して、選んだ対局を削除します。

通常の対局は開始時と各操作後に自動保存されます。チュートリアルの対局は保存一覧に入りません。

## CLI版を使う

**.NET 8 SDK** が必要です。以下のコマンドはリポジトリのルートで実行します。

まずビルドし、遊べるゲームと対応人数を確認します。

```bash
dotnet build TrumpGameLab.sln -m:1
dotnet run --project tools/TrumpLab.Cli --no-build -- list
```

Crazy Eightsを4人で始める例です。自分の番になったら、表示された操作の番号を入力します。ほかの席はCPUが担当します。

```bash
dotnet run --project tools/TrumpLab.Cli --no-build -- play crazy_eights --players 4 --seed 10
```

CPU同士で100局を試す場合や、2種類のゲームを比較してCSVに保存する場合は、次のように実行します。

```bash
dotnet run --project tools/TrumpLab.Cli --no-build -- simulate crazy_eights --games 100
dotnet run --project tools/TrumpLab.Cli --no-build -- compare --game german_whist --game gin_rummy --format csv --output comparison.csv
```

`--seed` で乱数の初期値を指定できます。同じゲーム・人数・ルール設定・難易度・seedと同じ操作なら、対局を再現できます。
CLIのコマンドとオプションは[コマンドリファレンス](docs/design/command_interface_design.md)を参照してください。

## 詳しい情報

- [ゲーム一覧と採用ルール](docs/rules/candidate-rules.md)
- [Unity版の詳細](Unity/TrumpGameLab/README.md)
- [ルールライブラリをUnityに組み込む](Packages/com.trump-game-lab.rules/README.md)
- [開発・検証の手順](docs/development.md)
