# Trump Game Lab

- Runtimeは .NET Standard 2.1 / C# 9・外部依存なしに保ち、Core.cs を具体ゲーム・CLI・候補台帳へ依存させない。Runtimeへ UnityEngine 依存を追加せず、表示層とは IGame 契約で接続する。
- 合法手は各ゲームの LegalActions()、状態変更は Apply() に集約する。CPUは観測不能情報を読まず、乱数は注入された DeterministicRandom だけを使う。ローカルルールは生成時オプションとし、グローバル状態へ残さない。
- 製品ロードマップ作業は docs/product/roadmap.md と既存マイルストーンを正本にし、同時に進めるタスクは1件だけにする。
- コード変更の完了時は dotnet build TrumpGameLab.sln -m:1、dotnet test tests/TrumpLab.Tests、両方の migration verification を実行する。bin/、obj/、Unity Library/ はコミットしない。
