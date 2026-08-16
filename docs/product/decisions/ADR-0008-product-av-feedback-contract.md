# ADR-0008 製品音響・演出feedback契約

- Status: Accepted
- Date: 2026-08-16

## Context

M06-T03ではnavigation、決定、拒否、card play、draw、wild suit確定、CPU手番、勝敗、errorを
視覚と音で区別し、master / music / SFX音量と演出速度を実際の再生・表示へ接続する必要がある。
feedbackをrule state、Action ID、session archive、replayへ混ぜると、CLIとUnityのseed再現性および
既存の公開契約を壊す。外部音源はlicense判断も必要になる。

## Decision

### semantic cue

製品Unity層だけに次のstable cueを置く。

- `Navigation`, `Submit`, `Reject`
- `CardPlay`, `Draw`, `WildSuit`, `CpuTurn`
- `Win`, `Lose`, `Error`

各cueはstable key、文字またはsymbolを含む視覚表現、固有の短いSFXを持つ。色だけを意味の正本に
しない。通常対局とtutorialのAction feedbackは表示文字列を解析せず、適用に成功してarchiveへ追加
された`SessionActionRecord`のactor、kind、card、valueだけから分類する。resume時の既存record、
同一frameの二重click、古いsnapshotのAction ID、CPU待機中のclickはfeedbackを再生せず従来どおり
無視する。理由を表示するvalidation拒否だけ`Reject`、session/tutorial faultは`Error`とする。

### 音源とvolume

音はrepository内のgeneratorが数式波形から決定論的に作るmono PCM16 WAVとし、外部sample、asset、
生成serviceを使わない。生成元、周波数、包絡、出力形式をrepositoryで所有し、同じ入力から同じbyteを
生成する。短いmusic loopを1件、各semantic cueのSFXを1件ずつcommitする。

Bootstrapは`AudioListener`を1件、2DのMusic / SFX `AudioSource`を各1件持つ。masterはPlayerの
`AudioListener.volume`、music / SFXは各sourceの`volume`へ適用し、masterをcategoryへ二重乗算しない。
いずれも`0`はmuteとし、起動時のLoadおよび成功したApply / Resetの同じframeで既存sourceへ反映する。
保存失敗時は再生中の設定を変更しない。

### 視覚feedbackと画面遷移

共通bannerを再利用し、cueのsymbolと英語fallbackを表示する。Matchではsnapshot更新後に対象を明示する。

- card play / wild suit: handとdiscard
- draw: stockとhand
- CPU turn: statusとinput lock
- reject: action feedback領域

Resultは`Win` / `Loss` / `Draw`を構造値として保持し、文字・symbol・paletteを分ける。Errorは既存modalの
focus trapと安全な戻り先を維持する。全`ScreenRouter`遷移には共通の非blocking visual transitionを
適用し、遷移演出をrule Actionの適用条件にしない。

### timing、順序、lifecycle

`Reduced`または`reduced_motion=true`では非本質的fade、scale、pulse、flashを行わず、staticな文字・symbol
と音を残す。`Normal`と`Fast`は異なる短いdurationを使うが、semantic cue列、input lock、Action適用、
atomic save、snapshot、結果は同じ順序にする。rule Actionは同期的に1回だけ適用し、演出からCoreを再実行
しない。terminal ActionのMatch feedbackだけを完了してからResultを表示し、session終了・別route・destroy
ではgeneration tokenで古いcoroutineを無効化してlockやfeedbackを残さない。

CPU待機は現在のpresentation profileを毎frame参照し、待機中の設定変更へ追従する。Helpでcancel可能なのは
Action commit前の待機だけとし、commit済みActionの保存・snapshot・結果処理を演出cancelで失わない。

## Consequences

- Core、UPM Runtime、game ID、Action ID、CLI、session/replay形式、seed再現性を変更せず製品feedbackを追加できる。
- 外部licenseやnetworkなしで全音源を再生成・監査できる。
- T04はstable cue keyへ日本語・英語catalog、font、text scale、contrastを接続できる。
- T05はaudio device切断、Player性能、allocation、soakを測定する。基本cue mappingとlifecycleはT03で完了する。
