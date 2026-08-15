# ADR-0003 Action履歴によるセーブ・リプレイ契約

- Status: Accepted
- Date: 2026-08-16

## Context

M03ではCrazy Eightsの中断再開と決定的なリプレイを追加する。保存のためにゲーム具体型の
private fieldや山札順を公開すると、`IGame`境界、秘密情報、92ゲームへの展開可能性を損なう。
一方、単に現在の表示snapshotを保存しても、非公開状態、ゲーム内乱数、CPU用乱数を復元できない。

次の判断を実装より先に固定する。

- リプレイと途中保存で共有するRuntime契約
- snapshotとAction履歴のどちらを正本にするか
- Action、seed、option、CPU条件の安定表現
- schemaとrules互換versionの扱い
- 破損、不明version、rules差異、非合法Actionの失敗時動作
- Unityの保存先、atomic write、backup、削除境界
- seedやAction履歴を含むlocal fileの脅威境界

## Decision

### 正本と責務境界

途中保存とリプレイは、同じimmutableな`SessionArchive`契約を使用する。正本は初期条件と
適用済みAction履歴であり、ゲーム内部snapshotは保存しない。

```text
SessionArchive
├─ formatVersion / rulesVersion
├─ gameId / players / seed / difficulty
├─ humanPlayers / sorted options
├─ ordered SessionActionRecord[]
└─ integrity algorithm / digest
```

- `TrumpLab.Core`にarchive model、検証、codec、recording、replayを置く。
- Coreはbyte列または文字列までを扱い、`File`、UnityEngine、保存先、画面へ依存しない。
- `SessionRecorder`は現在の`IGame`と初期条件を所有し、最新の`LegalActions()`に含まれる
  `Action`だけを`Apply()`と同時に履歴へ追加する。UIが別経路で履歴を書き足さない。
- `SessionReplayer`はRegistryから新しい`IGame`を生成し、履歴を先頭から順に検証・適用する。
- Unity Product層は`ISessionStore`相当のfilesystem adapter、slot一覧、autosave timing、
  replay playback cursorだけを所有する。Coreの具体ゲームをcastしない。
- replayの一時停止・早送り位置は再生器の一時状態であり、v1 archiveの正本へ含めない。

Action履歴はCrazy Eightsの主要phaseを初期状態から再構築でき、ゲームごとのprivate snapshot
公開を不要にする。復元時間は履歴長に比例するが、M03では1局10,000 Actionを上限とし、
snapshot最適化は実測で必要になった場合に別versionで追加する。

### 初期条件とCPU再現

初期条件は次を必須とする。

- `game_id`：Registryの安定ID
- `players`：生成時の人数
- `seed`：符号付き64 bit値。JSON numberの精度差を避けるため10進文字列で保持する
- `options`：生成時optionの完全なkey/value集合。key昇順でcanonical化する
- `difficulty`：CPU方策へ渡した値
- `human_players`：人間として入力したplayer indexの昇順集合

ゲーム内乱数は同じseedからRegistryで再生成し、Actionを再適用することで同じ位置まで進む。
CPU用乱数はM02と同じ`new DeterministicRandom(seed + 99991)`を1回生成して再利用する。

各`SessionActionRecord`は`actor`と構造化`Action`を持つ。再生時は記録actorが
`CurrentPlayer`と一致することを確認する。actorがCPUなら`ChooseCpuAction(actor, cpuRandom,
difficulty)`を呼び、返されたActionが記録Actionと完全一致することを確認してから適用する。
これによりCPU乱数の消費位置も復元する。不一致は記録Actionを推測適用せずreplay divergenceとする。
人間actorでは記録Actionが現在の`LegalActions()`に含まれることを確認して適用する。

各recordには適用後の`turn_after`、`current_player_after`、`terminal_after`をcheckpointとして持たせ、
再生器は毎Action後に一致を検証する。最終結果と構造化表示の完全一致はcontract testで確認し、
表示文字列`View()`や`Action.ToString()`は記録・復元に使用しない。

### v1の安定Action表現

`TrumpLab.Action`は次のfieldへ一対一に変換する。

| JSON field | 型 | 規則 |
|---|---|---|
| `kind` | string | 空でない完全なAction kind |
| `card` | object/null | `suit`を`clubs/diamonds/hearts/spades`、`rank`を1～13で保持 |
| `target` | integer/null | nullableなplayer/position targetをそのまま保持 |
| `value` | string/null | 文字列値を加工せず保持 |

label、翻訳文、`ToString()`結果、hand indexの再計算結果は保存しない。復元時は全fieldから
新しい`Action`を構築し、engineの値等価性と`LegalActions()`で照合する。

### wire formatとcanonical化

拡張子は`.tgs`、encodingはUTF-8 BOMなし、wire formatはJSONとする。外部packageをRuntimeへ
追加しないため、v1 schemaだけを扱う小さなstrict codecをCoreに実装する。

top-levelは次のfieldだけを持つ。

```json
{
  "format": "trump_lab_session",
  "format_version": 1,
  "rules_version": 1,
  "game": {
    "id": "crazy_eights",
    "players": 2,
    "seed": "23",
    "difficulty": 1,
    "human_players": [0],
    "options": [{"key": "wild_rank", "value": "8"}]
  },
  "actions": [],
  "integrity": {"algorithm": "sha256", "digest": "...lowercase hex..."}
}
```

writerは上記field順、option key昇順、human player昇順、Action適用順でcanonical JSONを生成する。
digestは`integrity`を除くcanonical UTF-8 bytesのSHA-256とする。readerはJSON field順には依存しないが、
unknown/duplicate/missing field、duplicate option、非canonical seed、範囲外値、末尾garbageを拒否する。
read後にmodelからcanonical payloadを再生成してdigestを照合する。

SHA-256は偶発破損と不用意な手編集を検出するための整合性であり、秘密鍵を使う署名ではない。
local fileを書き換えられる攻撃者はdigestも再計算できるため、実績や対戦の信頼根拠にはしない。

### versionと互換性

- `format_version`はwire schema version、`rules_version`は初期化・合法手・Apply・CPU選択の
  再現契約versionとする。v1 reader/replayerはそれぞれ値`1`だけを受理する。
- 未知の新旧versionを部分読込、field推測、best effort再生しない。
- 将来互換が必要な場合は、元bytesを保持したまま純粋な`vN -> vN+1` migratorとfixture testを追加する。
- rules変更で既存Action列またはCPU選択が変わる場合は`rules_version`を上げる。旧replayerを
  維持できないversionは「非対応」と明示し、現在slotへ上書きしない。
- game IDがRegistryにない、players/optionsが現在のfactoryで無効、CPU選択が不一致、
  Actionが非合法、checkpointが不一致の場合は種類を区別した例外で停止する。

公開model/wire fieldの変更はこのADRを置換するか、version移行方針を追記してから行う。

### 入力上限とエラー境界

archiveは信頼できない入力として扱い、decode前後に次の上限を適用する。

- file最大1 MiB、Action最大10,000件、option最大64件
- identifier/key最大128 UTF-16 code unit、任意value最大4,096 code unit
- players/human playerはRegistryと生成gameの範囲内、human playerは重複不可
- JSON numberは有限integerだけを許し、seedはcanonicalな10進Int64文字列だけを許す
- 再生は最初の不一致で停止し、部分復元gameを呼出元へ返さない

Coreは`SessionFormatException`、`UnsupportedSessionVersionException`、
`SessionIntegrityException`、`ReplayDivergedException`のように原因を分類する。Product UIは安全な
要約とslot名だけを表示し、file内容、seed、Action列、stack traceを通常logや画面へ出さない。
失敗archiveは自動修復・削除・上書きせず、元fileを保持する。

### Unity保存先とatomic write

Unity adapterは`Application.persistentDataPath/TrumpGameLab/Sessions/`配下だけを使用する。
slot file名はProduct側で生成したlowercase GUIDと`.tgs`から組み立て、表示名や外部入力をpathへ連結しない。
一覧はこのdirectory直下だけを対象にし、symlink/reparse pointやdirectory traversalを辿らない。

保存は同一directory内で次の順に行う。

1. 新しいcanonical bytesを一意な`.tmp`へ書く
2. streamをflushしてcloseする
3. 書いたtempを再読込し、size、decode、digest、replay checkpointを検証する
4. 既存slotがある場合は同一volumeのreplaceで`.bak`を残し、ない場合はatomic moveする
5. directory一覧を更新し、成功後にだけ不要tempを掃除する

失敗時は既存`.tgs`と`.bak`を変更しない。起動時に残ったtempは正常slotとして表示せず、sizeと
保存directory境界を確認してから掃除する。backupからの復元、slot削除、破損fileの隔離は
ユーザー操作を伴い、正常slotへ暗黙上書きしない。

autosaveは各`Apply()`成功とsnapshot更新の後に要求し、1時点に1 writeだけを実行する。
終了・タイトル遷移では進行中writeの完了または安全なcancelを待つ。Play Mode testでは実filesystemを
直接使わずtemporary storeを注入し、atomic adapterは専用integration testで検証する。

### 秘密情報と信頼境界

archiveへprivate handやstock順のsnapshotは直接書かない。ただしseedとAction履歴を持つため、
アルゴリズムを知るlocal userは非公開状態を再構築できる。M03はlocal single-player saveであり、
OS userからseedを秘密にすることは保証しない。

- save一覧、通常log、error UIへseed、option値、Action内容を表示しない。
- archiveをCPU判断や実績の権威証明に使わない。
- M08の対戦へ流用する場合はserver/host authority、相手へ渡す情報、署名またはMAC、replay公開範囲を
  別ADRで決定する。v1 local archiveを対戦protocolとして送信しない。

## Consequences

- replayと途中保存が1つの契約を共有し、game具体型のprivate snapshot公開なしで復元できる。
- seed、game内乱数、CPU乱数、difficultyを含む決定性をcheckpointごとに検出できる。
- JSONは調査しやすく、canonical writerとdigestによりfixtureと破損検査を安定化できる。
- Coreにstrict JSON codecとversioned modelを実装するコストが増える。
- 履歴が長いほど復元時間が増える。v1では上限で制御し、snapshot cacheは非正本の将来最適化とする。
- SHA-256だけでは悪意あるlocal改ざんを認証できない。実績と対戦の信頼境界は別途必要になる。
- rules変更時はversion判断と旧fixture検証が必須になり、互換性を暗黙に約束できなくなる。
