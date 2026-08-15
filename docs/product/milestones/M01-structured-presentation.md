# M01 構造化表示契約

## 状態

- マイルストーン：`In Progress`
- 次のタスク：`M01-T06`
- 参照ゲーム：`crazy_eights`

## 目的

Unity UIが`IGame.View()`の表示文字列を解析せず、型付きの状態と操作候補から画面を構築できる
契約を追加する。最初にCrazy Eightsで契約を実証し、既存92ゲームを一括変更せずに段階導入できる
境界を確立する。

## 制約

- `IGame.View()`とCLI出力は互換性のため維持する。
- Runtimeへ`UnityEngine`依存を追加しない。
- `Core.cs`を具体ゲーム、CLI、候補台帳へ依存させない。
- 表示契約とCPU方策は、viewerが観測できない手札や山札順を公開しない。
- 合法手は引き続き各ゲームの`LegalActions()`だけを正本とする。
- Actionの適用は引き続き`Apply()`へ集約する。
- Unity 2021.3、.NET Standard 2.1、C# 9の範囲を維持する。

## 対象

- ゲーム、フェーズ、現在手番、プレイヤー、カード領域を表す読み取り専用モデル
- 表向き、裏向き、枚数だけ公開するカード領域の表現
- `LegalActions()`の各要素と一対一に対応するUI向け操作記述
- viewer別の構造化表示取得口
- Crazy Eightsの手札、捨札、山札枚数、指定suit、手番、終了結果
- 公開情報と非公開情報の契約テスト
- RuntimeとUnity Test Runnerの同等テスト

## 対象外

- UnityのScene、Prefab、カード画像、アニメーション
- 既存92ゲームすべての構造化表示対応
- セーブ形式、リプレイ形式、ネットワーク同期形式
- CPU難易度の追加
- `IGame.View()`またはCLIの削除・出力変更

## タスク

| ID | 状態 | 内容 | 依存 | 完了条件 |
|---|---|---|---|---|
| M01-T01 | Done | 構造化表示のAPI・データモデル設計を`docs/design/`へ追加する | なし | 公開境界、拡張方法、互換性、Action対応がレビュー可能な粒度で定義されている |
| M01-T02 | Done | 共通の読み取り専用モデルと段階導入用インターフェースをRuntimeへ追加する | T01 | Unity非依存でコンパイルでき、既存`IGame`実装を破壊しない |
| M01-T03 | Done | Crazy Eightsへ構造化状態と操作記述を実装する | T02 | viewer別状態と全合法手を文字列解析なしで取得できる |
| M01-T04 | Done | .NET契約テストを追加する | T03 | 非公開手札、山札順、指定suit、draw/play境界、Action一対一対応を固定する |
| M01-T05 | Done | Unity Edit Modeの同等テストと利用例を追加する | T04 | Unity Standard対象で同じ契約を検証し、READMEに利用例がある |
| M01-T06 | Ready | 必須検証と互換性確認を行いM01を完了する | T05 | 下記の完了条件をすべて満たし、ロードマップを更新する |

タスク完了時はその行を`Done`にし、依存を満たした次の1件だけを`Ready`へ変更する。

## 完了条件

- Crazy EightsのUnity UIが必要とする状態を型付きで取得できる。
- UI向け操作記述から元の`Action`を一意に選択できる。
- viewerを替えても非公開情報が漏れないことを固定seedで検証している。
- 既存のCLIコマンドと`IGame.View()`出力を変更していない。
- 既存92ゲームが引き続き生成・完走できる。
- `dotnet build TrumpGameLab.sln -m:1`が成功する。
- `dotnet test tests/TrumpLab.Tests`が全件成功する。
- BashとPowerShellのmigration verificationが成功する。
- 利用可能なUnity Editorで`run-unity-tests.ps1 -Mode Standard`が成功する。

## 停止条件

- `IGame`の破壊的変更が必要になった場合
- 表示モデルをセーブまたはネットワーク形式として同時に確定する必要が生じた場合
- Crazy Eights以外の具体ゲーム要件を共通契約へ入れないと進められない場合
- 非公開情報境界を既存の公開APIだけで保証できない場合

これらに該当した場合は実装を広げず、選択肢と影響をADR案として整理してユーザーへ確認する。

## 次への遷移

`M01-T06`完了時にM01を`Done`、M02と`M02-T01`を`Ready`へ変更する。通常の1タスク依頼と
M01を対象にしたGoalはM02を実装せず、状態更新後に停止する。
