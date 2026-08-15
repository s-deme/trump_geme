# 構造化表示契約設計

## 目的

Unityなどの表示層が`IGame.View()`の文章を解析せず、viewerに公開可能な状態と
`LegalActions()`由来の操作だけから画面を構築できるようにする。本契約は表示用の
読み取り専用スナップショットであり、セーブ、リプレイ、ネットワーク同期の永続形式にはしない。

## 依存境界と段階導入

Runtimeの`TrumpLab`名前空間へ、Unity非依存のモデルと次の任意インターフェースを追加する。

```csharp
public interface IGamePresentationProvider
{
    GamePresentation Present(int? viewer = null);
}
```

- `IGame`と`GameBase`は変更しない。既存ゲーム、CLI、`View()`の公開契約を維持する。
- 表示層は`IGame`を生成し、`IGamePresentationProvider`を実装するゲームだけ構造化表示へ進む。
- 未対応ゲームは従来どおりCLIから利用できる。Unity製品UIは対応済みゲームだけを選択可能にする。
- 共通モデルは具体ゲーム、Registry、Catalogue、CLI、UnityEngineへ依存しない。
- `Present()`は状態を変更せず、返却後にゲームが進行しても内容が変わらないスナップショットを返す。
- `viewer == null`は既存`View()`と同じく`CurrentPlayer`を表す。範囲外のviewerは
  `ArgumentOutOfRangeException`で拒否する。

依存方向は次のとおりとする。

```text
Core (Card, Action, IGame)
  ↑
Presentation contract (共通の不変モデル、IGamePresentationProvider)
  ↑
対応ゲーム (最初は CrazyEightsGame)
  ↑
Unity UI
```

## 公開モデル

公開型はすべて`TrumpLab`名前空間へ置く。コレクションはコンストラクターでコピーし、
`IReadOnlyList<T>`または`IReadOnlyDictionary<TKey, TValue>`として公開する。nullのコレクション、
重複するID、負の枚数、範囲外のplayer indexはコンストラクターで拒否する。

### `GamePresentation`

1回の`Present()`で得られるトップレベルのスナップショットである。

| プロパティ | 型 | 意味 |
|---|---|---|
| `GameId` | `string` | `IGame.GameId`と同じ安定ID |
| `Phase` | `string` | ゲーム内フェーズの機械可読ID。表示文言には使わない |
| `Viewer` | `int` | このスナップショットを取得したplayer index |
| `CurrentPlayer` | `int` | 現在手番のplayer index |
| `TurnCount` | `int` | `IGame.TurnCount`と同じ値 |
| `IsTerminal` | `bool` | `IGame.IsTerminal`と同じ値 |
| `Players` | `IReadOnlyList<PlayerPresentation>` | 全playerをindex順に1件ずつ格納 |
| `CardZones` | `IReadOnlyList<CardZonePresentation>` | viewerへ公開可能なカード領域 |
| `Fields` | `IReadOnlyList<GameFieldPresentation>` | 共通形状にないゲーム固有の型付き公開値 |
| `Actions` | `IReadOnlyList<ActionPresentation>` | viewerが現在選択できる全合法手 |
| `Result` | `GameResultPresentation?` | 終了時だけ存在する公開結果 |

`Phase`、zone ID、field IDは比較可能な英小文字snake_caseとし、ローカライズ文字列を格納しない。
表示層はこれらのIDを文字列リソースやPrefabへ対応付けるが、`View()`の文章や`Action.ToString()`を
解析しない。

### `PlayerPresentation`

| プロパティ | 型 | 意味 |
|---|---|---|
| `PlayerIndex` | `int` | `0..Players-1`の安定index |
| `IsCurrent` | `bool` | 現在手番か |
| `IsViewer` | `bool` | viewer本人か |

表示名、アバター、human/CPUなど製品設定に属する値はRuntimeのゲーム状態へ混ぜず、Unity側で
player indexへ関連付ける。

### `CardZonePresentation`

カード領域は次の情報を持つ。

| プロパティ | 型 | 意味 |
|---|---|---|
| `Id` | `string` | スナップショット内で一意な機械可読ID |
| `Role` | `string` | `hand`、`stock`、`discard`などの機械可読な役割 |
| `OwnerPlayer` | `int?` | player所有領域ならそのindex、共通領域ならnull |
| `Visibility` | `CardZoneVisibility` | `FaceUp`、`FaceDown`、`CountOnly`のいずれか |
| `Count` | `int` | 領域内の総枚数 |
| `Cards` | `IReadOnlyList<Card>` | `FaceUp`時だけ、公開順で格納するカード |

公開規則は以下を不変条件とする。

- `FaceUp`では`Cards.Count == Count`とする。
- `FaceDown`と`CountOnly`では`Cards.Count == 0`とし、カード値や順序を一切格納しない。
- `FaceDown`はUIが`Count`枚のカード裏面を描く領域、`CountOnly`は枚数だけを示す領域である。
- 山札は`CountOnly`とし、先頭カード、内部配列、シャッフル順を公開しない。
- viewer本人の手札は`FaceUp`、他playerの手札は`FaceDown`を基本とする。

### `GameFieldPresentation`

カード領域以外のゲーム固有情報を、安定した`Id`と判別可能な値で公開する。
値型`PresentationValue`は`Kind`と対応する値を1つだけ持ち、factory methodで生成する。

| `PresentationValueKind` | 値 | 用途例 |
|---|---|---|
| `Text` | `string` | 公開された宣言ID。表示用の自由文には使わない |
| `Number` | `double` | 得点、bid、counter |
| `Boolean` | `bool` | 公開フラグ |
| `Suit` | `Suit` | trump、指定suit |
| `Player` | `int` | dealer、leader、対象player |
| `Card` | `Card` | zoneに属さない単一の公開カード |

値が存在しない状態はfield自体を省略する。Crazy Eightsの指定suitは`called_suit`というfieldで
表し、未指定時はfieldを含めない。新しいゲームは共通モデルへ具体ゲーム用プロパティを追加せず、
まずfield IDを追加する。複数ゲームで同じ意味が安定した時だけ将来の共通型への昇格を検討する。

### `ActionPresentation`

UIへ公開する各操作は次の情報を持つ。

| プロパティ | 型 | 意味 |
|---|---|---|
| `Id` | `string` | 当該スナップショットの`action_0`から始まる一意ID |
| `Action` | `Action` | 同じindexにある`LegalActions(viewer)`の値 |
| `LabelKey` | `string` | ローカライズ用の機械可読キー |

生成時に`LegalActions(viewer)`を一度だけ取得し、列挙順を保って全要素を1件ずつ変換する。
Actionの`Kind`、`Card`、`Target`、`Value`は`ActionPresentation.Action`から型付きで参照できる。
カード選択、対象player選択、suit選択はこれらの値からUIを構築し、表示文字列を逆変換しない。

`Id`は永続IDではなく、そのスナップショット内だけで有効である。UIは選択した項目が保持する
`Action`を`IGame.Apply()`へ渡す。適用時の合法性は従来どおり各ゲームの`Apply()`が現在の
`LegalActions()`に対して再検証する。これにより、古いスナップショット、二重入力、CPU手番への
切替後の入力はルールエンジンで拒否される。

`Actions`は次の条件を満たす。

- 非終了かつ`Viewer == CurrentPlayer`なら、`LegalActions(Viewer)`と件数、順序、値が一致する。
- `Viewer != CurrentPlayer`または終了時は空とし、他playerの手札に由来する操作を漏らさない。
- 同値のActionが複数存在しても、それぞれ異なる連番IDを持つため元の列挙要素と一対一になる。
- `LabelKey`は原則`action.<GameId>.<Action.Kind>`とする。既知の共通操作へ将来共通キーを
  割り当てても、Actionとの対応は変更しない。

### `GameResultPresentation`

終了後に必要な`Winners`、`Scores`、`Reason`、`Turns`を不変コレクションと値で公開する。
`Reason`はゲーム実装が返す安定IDとして扱い、UI側で表示文言へ変換する。`GameResult.Extra`は
任意の`object`を含み得るため自動転送せず、UIに必要な公開値だけを`Fields`へ明示的に写す。

## Crazy Eightsの対応表

| ゲーム状態 | 構造化表示 |
|---|---|
| `phase` | `GamePresentation.Phase`: `play`または`choose_starter_suit` |
| 各手札 | zone ID `hand_<player>`、role `hand`。viewer本人は`FaceUp`、他者は`FaceDown` |
| 山札 | zone ID `stock`、role `stock`、`CountOnly` |
| 捨札 | zone ID `discard`、role `discard`、`FaceUp`。公開順は古い札からtopまで |
| `calledSuit` | 指定中だけfield ID `called_suit`、kind `Suit` |
| 手番、turn、終了 | `CurrentPlayer`、`TurnCount`、`IsTerminal` |
| 勝者、得点、理由 | 終了時の`GameResultPresentation` |
| draw/play/pass/suit指定 | `LegalActions(viewer)`と同順の`ActionPresentation` |

捨札は公開情報として全履歴を表示モデルへ含めてよいが、UIは通常topだけを描画する。
山札は枚数だけを公開し、順序は含めない。

## 互換性と失敗時動作

- `IGame`、`GameBase`、`Action`、`GameResult`の既存シグネチャを変更しない。
- `IGame.View()`の文面、CLIの引数と出力、game ID、既存difficultyの意味を変更しない。
- 表示取得は乱数を消費せず、`LegalActions()`以外の状態変更処理を呼ばない。
- UIはprovider非対応を機能未対応として扱い、`View()`解析へフォールバックしない。
- 未知のphase、zone role、field ID、action kindはデータ破損とはせず、安全な汎用表示または
  明示的な未対応エラーにする。UIが推測したActionを生成してはならない。
- セーブ、リプレイ、ネットワークでは本モデルを直列化せず、それぞれのマイルストーンで
  バージョン付き形式を別途定義する。

## 検証方針

M01の後続タスクで、.NETとUnity Edit Modeの両方に次の契約テストを置く。

- providerを実装しない既存ゲームを含め、全登録ゲームの生成と既存CLI出力が変わらない。
- 各viewerのplayer一覧、zone ID、枚数、公開カードが不変条件を満たす。
- viewer本人以外の手札のカード値と、山札のカード値・順序を到達可能な公開値へ含めない。
- Crazy Eightsのstarter suit、通常play、wild suit指定、draw、pass、終了の各phaseを検証する。
- 手番viewerでは`Actions`と`LegalActions()`がindexごとに値一致し、非手番viewerでは空になる。
- 同じseedとAction列では各スナップショットが等価で、`Present()`の呼出しが進行を変えない。
- 古い`ActionPresentation`を適用した場合も`Apply()`の合法性検査を迂回できない。
