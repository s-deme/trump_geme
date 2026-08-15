# Trump Game Lab

Unity向けトランプゲームの採用ルールを決めるため、多数のルールを同じ条件で
CPU試遊・自動検証するC#ライブラリです。ルール本体はUnity Package Manager形式で、
CLIとUnityが同じコードを使用します。

## 構成

- `Packages/com.trump-game-lab.rules/Runtime/` — Unity/C#ルールライブラリ
- `tools/TrumpLab.Cli/` — 一覧、試遊、CPUシミュレーションCLI
- `tests/TrumpLab.Tests/` — NUnitによる.NET契約テストとTRXレポート
- `Packages/com.trump-game-lab.rules/Tests/Editor/` — Unity Test Runner契約テスト
- `docs/` — 要件と設計の正本

Unityでは、このリポジトリをプロジェクトの`Packages/manifest.json`からローカルまたは
Gitパッケージとして参照します。RuntimeアセンブリはUnity 2021.3以上、
`.NET Standard 2.1`、C# 9を対象にしています。

## 実装状況

候補台帳92件はすべてゲームIDから生成でき、全件がゲーム固有の状態機械を使用します。
CLIの`play`で合法手を選んで遊べ、`simulate`では同じ実装をCPU同士で完走できます。
会話、身体動作、同時操作などは列挙アクションまたは決定論的な入力順へ正規化し、採用した
バリアントと採用外の地域差を候補別仕様へ明記しています。台帳上は92件すべて`Verified`で、
`RuleSpecific`と`Prototype`は0件です。

完成判定、候補ごとの状態、採用バリアントの暫定仕様は
[`docs/rules/candidate-rules.md`](docs/rules/candidate-rules.md)です。正式照合の進捗と監査単位は
[`docs/rules/verification-audit-plan.md`](docs/rules/verification-audit-plan.md)で管理し、Verified候補の
正本は同計画から参照する個別照合書です。

## CLIとテスト

```bash
dotnet build TrumpGameLab.sln -m:1
dotnet test tests/TrumpLab.Tests --logger "trx;LogFileName=test.trx" --results-directory TestResults
./scripts/verify-migration.sh
pwsh ./scripts/verify-migration.ps1

dotnet run --project tools/TrumpLab.Cli -- list
dotnet run --project tools/TrumpLab.Cli -- catalogue --pending
dotnet run --project tools/TrumpLab.Cli -- simulate german_whist --games 1000
dotnet run --project tools/TrumpLab.Cli -- compare --game german_whist --game gin_rummy --format csv
dotnet run --project tools/TrumpLab.Cli -- compare --format json --output verified-comparison.json
dotnet run --project tools/TrumpLab.Cli -- play crazy_eights --players 4 --seed 10
dotnet run --project tools/TrumpLab.Cli -- simulate crazy_eights -n 100 -o wild_rank=2
```

## 新しいゲーム

1. `Runtime/Games/<GameName>Game.cs`へ`GameBase`の派生型を実装する。
2. `LegalActions`、`Apply`、`IsTerminal`、`Result`、`View`を実装する。
3. 最低限完走可能な`ChooseCpuAction`を用意する。
4. `GameInfo`とファクトリーを`BuiltInGames`へ登録する。
5. 候補台帳へ同じ`ImplementationId`を設定する。
6. 最少・最大人数と複数seedの契約テストを通す。

乱数には注入された`DeterministicRandom`だけを使います。同一seedの結果はCLIとUnityで
一致し、CPUは相手の手札や山札順など観測不能な情報を方策に使用しません。

Unity Test Runnerでパッケージテストを実行する方法は
`Packages/com.trump-game-lab.rules/README.md`を参照してください。
