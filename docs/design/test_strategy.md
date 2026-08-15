# テスト戦略

CLIと同じ.NET実行環境の契約テストに加え、UPMパッケージ内の同等テストをUnity Test
RunnerのEdit Modeで実行する。

1. Card、ブラックジャック得点、ジン・ラミーのメルドを単体検証する。
2. 全登録ゲームを30 seed完走させる。
3. 全ゲームの最少・最大人数を複数seedで完走させる。
4. 人数範囲外拒否と設定インスタンス分離を検証する。
5. 成功テスト0件を成功として扱わない。
6. Verified候補では固定seedでルール例外、秘密情報だけが異なる観測同値状態、CPU合法性、
   CLI固有actionを追加検証する。Baohuangは168枚、秘密guard、複数枚組、soft pass、
   早期終了、倍率得点、2deal目上納を独立シナリオで固定する。

必須ゲートは`dotnet build TrumpGameLab.sln -m:1`と
`dotnet test tests/TrumpLab.Tests`、`scripts/verify-migration.sh`である。
Windowsネイティブ環境では同等の`pwsh ./scripts/verify-migration.ps1`も利用できる。
Unity Editorを利用できる環境ではパッケージ内のEdit Modeテストも実行する。
`pwsh ./scripts/run-unity-tests.ps1 -UnityPath <Unity.exe>`は最小Unityプロジェクトを一時領域へ
展開してEdit Modeテストを実行し、テスト0件を失敗として扱う。互換性の下限確認には
Unity 2021.3.48f1を使用し、より新しいEditorでの成功は前方互換性の確認として区別する。

テスト範囲は次の3段階とする。

| Mode | 除外対象 | 用途 |
|---|---|---|
| `Fast` | `BroadSimulation` | 実装中の反復確認。既定値 |
| `Standard` | `Exhaustive` | 実装単位の完了時、Unity互換確認 |
| `Full` | なし | nightly、リリース前、全回帰の明示実行 |

.NETでは`pwsh ./scripts/run-dotnet-tests.ps1 -Mode <Mode>`、Unityでは
`pwsh ./scripts/run-unity-tests.ps1 -UnityPath <Unity.exe> -Mode <Mode>`を使用する。
Unity Test Frameworkの`testCategory`で同じカテゴリを除外するため、両実行環境で範囲が一致する。

2026-08-16の現行環境では、.NETの`Fast`は214件で約10～12秒、`Standard`は233件で約78秒、
`Full`は234件で約6～7分だった。Unity 6000.3.22f1では
`Fast`が213件で約67秒、`Standard`が232件で約5分48秒、
`EveryRegisteredGameCompletesAcrossSeeds`単独が約23分だった。時間は実行環境で変動するため、
回帰の合否条件には使用しない。同テストだけ30分の`Timeout`を許容し、Unityスクリプト全体の
上限は40分とする。

監査中は変更中の個別照合テストまたは`Fast`だけで短く確認してよい。ソースを保存するたびに
`Full`を実行せず、実装単位の最後に.NETのfilterなし全テストを1回実行する。
Unityの`Full`は定期実行へ分離する。migration verificationは全テストを再実行せず、
レガシー依存、Runtime境界、CLI台帳件数、Verified ID集合、個別照合書の対応だけを検査する。
これによりseed数やFullのカバレッジを減らさず、長時間試験の重複実行を防ぐ。
