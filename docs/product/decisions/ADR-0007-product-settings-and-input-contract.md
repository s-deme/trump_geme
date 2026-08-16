# ADR-0007 製品設定と入力契約

- Status: Accepted
- Date: 2026-08-16

## Context

M06-T02では表示、音量、演出速度、入力bindingを再起動後も保持し、欠落・破損・未知versionから
安全に復旧できる必要がある。後続のlocalizationとaccessibilityも同じ設定へ追加すると既存fileの
migrationが必要になるため、M06で利用する全fieldを最初のversionへ含める。

設定はrule stateではない。game ID、Action ID、seed、session archive、replay、tutorial progressへ
deviceや表示環境を混ぜると、CLIとUnityの再現性および既存の公開契約を壊す。Input Systemも
製品Unity projectだけの依存とし、CoreとUPM Runtimeへ伝播させない。

## Decision

### 設定modelと保存先

engine非依存のimmutable `ProductSettings`をformat version 1として定義し、次を保持する。

- windowed / borderless fullscreen、対応解像度、VSync
- master / music / SFX音量の`0..100`
- `Reduced` / `Normal` / `Fast`の演出速度
- keyboard / gamepad別のnavigation 4方向、submit、cancel、help binding
- `ja-JP` / `en-US`、text scale `100 / 125 / 150%`、high contrast、reduced motion

保存先は`Application.persistentDataPath/TrumpGameLab/settings.v1`とし、`Sessions/`、session archive、
`progress.v1`から分離する。既定値はADR-0006のCFG-01/CFG-02に従い、UI cultureが日本語なら
`ja-JP`、それ以外は`en-US`とする。

### v1形式と更新

`settings.v1`はBOMなしのstrict UTF-8、固定field名、固定順、LF終端のcanonical textとする。
最大長は16 KiBで、欠落field、余分なfield、順序違反、重複binding、範囲外値、非canonicalな
control path、未知versionをfile全体の失敗として扱う。

保存は同一directoryの固有temp fileへ全byteを書き、`Flush(true)`、上限付き再読込、decode、
元modelとの一致を確認してからatomic replaceする。正常な旧fileは固有`.bak`へ残す。

起動時のLoadは次の3状態を返す。

- `Missing`: 安全な既定値を適用するがfileを作らない。
- `Loaded`: 検証済みの保存値を適用する。
- `Invalid`: 安全な既定値を適用するが原本へ書込み、移動、削除を行わない。

破損・未知versionの原本を置換できるのは、設定画面でユーザーがApplyまたはResetを明示した場合だけ
とする。その場合も原byteを固有`.invalid`へflushして保全してから置換する。Resetは設定fileだけを
既定値へ戻し、session、replay、tutorial progressを変更しない。

v1のfieldを削除、改名、意味変更しない。将来の追加でv1を読めなくする場合は新versionとmigration、
失敗時の原本保全を別のADRで決定する。

### 適用責務

`ProductSettingsService`がstoreとhost適用を調停し、Load時と成功した明示Save/Reset後だけ現在値を
更新する。`UnityProductSettingsApplier`はPlayerでresolution、fullscreen mode、VSync、master volumeを
適用する。windowが`1280x720`未満へ縮小された場合は毎frameのdisplay guardが最後に適用した対応
resolutionとmodeへ戻す。Editor testではhostの画面・quality・audioを変更せず、pure policyとfakeを
注入して検査する。

music / SFXの保存値はM06-T03のcategory別AudioSourceが参照し、locale、text scale、high contrast、
reduced motionはM06-T04が同じv1 modelから適用する。演出速度はrule処理やAction順を変えず、
製品controllerのpresentation delayだけを変更する。

### 入力

製品projectは`com.unity.inputsystem` 1.17.0をmanifestとlock fileへ固定し、Active Input Handlingを
Input System Package (New)だけにする。`StandaloneInputModule`とlegacy `UnityEngine.Input` pollingは
併用しない。

UI ActionはPoint、Click、RightClick、MiddleClick、ScrollWheel、Navigate、Submit、Cancel、Helpを
製品起動時に専用`InputActionAsset`へ構築する。mouse pointer操作は固定し、保存対象は次のstableな
semantic slotとcanonical control pathだけとする。

- `keyboard|gamepad` × `up|down|left|right|submit|cancel|help`
- keyboard pathは`<Keyboard>/...`、gamepad pathは`<Gamepad>/...`

device ID、表示label、scene object参照、rule Action IDは保存しない。同一device内の重複を拒否し、
14 slotを常に有効なpathで埋める。字句検証に加え、Input Systemへ登録済みのcontrol layoutから
各pathが実在するButton controlだと確認する。意味検証に失敗したLoaded設定も`Invalid`へ降格し、
原本を書き換えず既定値を使う。rebind中は通常UI Actionを停止し、cancelまたはtimeoutでは保存値と
有効Actionを変更しない。gamepad rebind中の切断は即時cancelしてkeyboard / mouse Actionを再開する。
変更は設定画面のdraftへ置き、Apply成功後に永続化と実Actionへ反映する。

Cancelは`InputSystemUIInputModule`から選択controlの`ICancelHandler`へ一度だけ配送する。globalな
Cancel購読を重ねず、dropdownやmodalと画面遷移が同一入力で二重実行されないようにする。Helpだけを
global Actionとし、現在画面のcontext helpへ接続する。

## Consequences

- 設定破損を起動不能や暗黙のdata lossへ変えず、安全な既定値で操作を継続できる。
- 設定とgame/session stateが分離され、既存game ID、CLI、seed、replay契約を維持できる。
- keyboard、mouse、gamepadが同じuGUI navigationとfocus経路を使い、legacy/new inputの二重適用を
  防止できる。
- v1はM06後続fieldを先に含むため、T03/T04は保存形式を変えず適用層を追加できる。
- semantic slotは各commandにつき1 pathであり、複数代替bindingやdevice glyph自動切替が必要なら、
  新versionとmigrationを設計する必要がある。
