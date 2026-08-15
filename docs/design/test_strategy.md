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

全登録ゲームを30 seedで走らせる試験は全体でおよそ90秒かかる。監査中は変更中の
個別照合テストだけをfilterして短く確認してよいが、`Verified`へ昇格する単位の最後には
filterなしの全テストを必ず1回実行する。migration verificationは全テストを再実行せず、
レガシー依存、Runtime境界、CLI台帳件数、Verified ID集合、個別照合書の対応だけを検査する。
これによりテストのseed数やカバレッジを減らさず、同じ全体試験の重複実行を防ぐ。
