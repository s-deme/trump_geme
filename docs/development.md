# 開発・検証の手順

製品の概要と起動方法は[README](../README.md)を参照してください。
この文書のパスとコマンドは、リポジトリルートを基準にしています。

## 構成と動作環境

- `Packages/com.trump-game-lab.rules/Runtime/`：Unity/C#ルールライブラリ
- `tools/TrumpLab.Cli/`：一覧、試遊、CPUシミュレーションCLI
- `tests/TrumpLab.Tests/`：NUnitによる.NET契約テストとTRXレポート
- `Packages/com.trump-game-lab.rules/Tests/Editor/`：Unity Test Runner契約テスト
- `Unity/TrumpGameLab/`：Crazy Eights製品の開発用Unityプロジェクト
- `docs/`：要件と設計の正本

ルールライブラリはUnity 2021.3以上、.NET Standard 2.1、C# 9を対象とし、CLIとUnityで同じコードを使用します。
CLIと.NETテストには.NET 8 SDKを使用します。製品用UnityプロジェクトのEditorは6000.3.22f1であり、ライブラリ単体の互換範囲とは別です。
ライブラリの追加方法とUnity側のテスト手順は[パッケージREADME](../Packages/com.trump-game-lab.rules/README.md)を参照してください。

## ビルドとテスト

```bash
dotnet build TrumpGameLab.sln -m:1
pwsh ./scripts/run-dotnet-tests.ps1 -Mode Fast
pwsh ./scripts/run-dotnet-tests.ps1 -Mode Standard
pwsh ./scripts/run-dotnet-tests.ps1 -Mode Full
dotnet test tests/TrumpLab.Tests --logger "trx;LogFileName=test.trx" --results-directory TestResults
./scripts/verify-migration.sh
pwsh ./scripts/verify-migration.ps1
```

反復中は`Fast`で確認し、実装単位の完了時に`Full`と両方のmigration verificationを1回実行します。
`Fast`は広域シミュレーションを除外し、`Standard`は30 seedの全登録ゲーム試験だけを除外します。
Unity側も`run-unity-tests.ps1`で範囲を選べます。完了時は`Standard`、約23分の`Exhaustive`を含む`Full`はnightly、リリース前、または明示的な全回帰確認で実行します。
文書だけの変更に必要な確認を含む作業規則は[AGENTS.md](../AGENTS.md)を参照してください。

## ルールと実装状況

候補台帳92件はすべてゲームIDから生成でき、全件がゲーム固有の状態機械を使用します。
台帳上は全件`Verified`で、`RuleSpecific`と`Prototype`は0件です。
会話、身体動作、同時操作などは列挙アクションまたは決定論的な入力順へ正規化し、採用したバリアントと採用外の地域差を候補別仕様へ記録しています。

- [候補台帳・完成判定](rules/candidate-rules.md)
- [正式照合の計画・個別照合書](rules/verification-audit-plan.md)
- [製品開発ロードマップ](product/roadmap.md)
- [製品開発の進め方](product/README.md)

進捗状態はロードマップと個別マイルストーンで管理し、この文書へ複製しません。

## ゲームの追加

1. `Runtime/Games/<GameName>Game.cs`へ`GameBase`の派生型を実装します。
2. `LegalActions`、`Apply`、`IsTerminal`、`Result`、`View`を実装します。
3. 最低限完走可能な`ChooseCpuAction`を用意します。
4. `GameInfo`とファクトリーを`BuiltInGames`へ登録します。
5. 候補台帳へ同じ`ImplementationId`を設定します。
6. 最少・最大人数と複数seedの契約テストを通します。

上記の`Runtime/`は`Packages/com.trump-game-lab.rules/Runtime/`を指します。
乱数には注入された`DeterministicRandom`だけを使い、同一条件でCLIとUnityの結果を一致させます。
CPUは相手の手札や山札順など観測できない情報を方策に使用しません。
