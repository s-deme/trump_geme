# M06-T03 音響・演出受入記録

## 受入境界

- 実施日：2026-08-16
- OS：Windows 11 x64 build 26200（正式対象のbuild 26100以降）
- Unity：6000.3.22f1、Input System 1.17.0
- scene：`Unity/TrumpGameLab/Assets/TrumpLab/Product/Scenes/Bootstrap.unity`
- rule対象：2人Crazy Eights、既存game ID / Action ID / seed / archive契約を変更しない
- 音源：repository generator所有のmono 44.1 kHz PCM16 WAV。外部sample、service、licenseなし

この記録は[ADR-0006](../decisions/ADR-0006-product-quality-baseline.md)の`AV-01`〜`AV-03`と
[ADR-0008](../decisions/ADR-0008-product-av-feedback-contract.md)を、保存済みscene、生成asset、
固定seedの製品flowへ接続する。batch testはaudio deviceを開かないため、実スピーカーでの主観的な
聞き分けはM06-T06のrelease candidate手動受入で再確認する。

## AV-01 操作feedback

| event | 視覚 | SFX | 自動証跡 | 結果 |
|---|---|---|---|---|
| Navigation | 共通bannerの`Focus moved`とsymbol | `navigation.wav` | UI move event、catalog / audio全件契約 | Pass |
| Submit | 共通bannerの`Confirmed`とsymbol | `submit.wav` | Submitによる実screen遷移 | Pass |
| Reject | validation領域と共通banner。busy / staleは無音 | `reject.wav` | settings / tutorial validationとdisabled input契約 | Pass |
| Card play | hand / discardと共通banner | `card-play.wav` | tutorial固定trace、Action classifier | Pass |
| Draw | stock / handと共通banner | `draw.wav` | tutorial固定trace、Action classifier | Pass |
| Wild suit | card play後にhand / discardとsuit確定banner | `wild-suit.wav` | wild playの`CardPlay -> WildSuit`順序 | Pass |
| CPU turn | statusとhuman input lock | `cpu-turn.wav` | 通常対局とtutorialのCPU待機 | Pass |
| Win / Loss | 構造化outcome、別symbol / text / palette | `win.wav` / `lose.wav` | result全分岐と固定seed完走 | Pass |
| Error | focus trap modalと共通error banner | `error.wav` | 実modal表示と安全な戻り先 | Pass |

生成WAV 11件は44 byte header後に非zero PCM payloadを持ち、SHA-256によるwaveform signatureが
全件相互に異なることを`ProductContractTests.GeneratedAudioAssetsUseOwnedPcmContract`で検査する。
各cueは`ProductAudioControllerTests`でexactly oneのbindingとlogical dispatchを検査する。

## AV-02 音量

| 項目 | 自動証跡 | 結果 |
|---|---|---|
| Master | Playerだけ`AudioListener.volume`へ0〜100を正規化。Editor hostは変更しない | Pass |
| Music / SFX | 既存の別AudioSourceへ0〜100を同じframeで反映 | Pass |
| mute | master / categoryの0を無音として扱い、music 0はloopを停止、SFX 0はPlayOneShotしない | Pass |
| Apply / Reset / Load | 保存成功後または起動Load時だけ既存sourceへ反映。保存失敗時は現値維持 | Pass |
| 永続化 | `settings.v1` round-trip後もmaster / music / SFXを維持 | Pass |

`ProductAudioControllerTests`が0 / 60 / 80 / 100と全cueを、`ProductFlowTests`がSettingsの
Apply / Resetから次frameのMusic / SFX sourceを、`ProductSettingsServiceTests`が成功・失敗時の
applier境界を検査する。Player masterの実device確認はT06手動matrixへ含める。

## AV-03 演出速度とrule不変性

| 項目 | 自動証跡 | 結果 |
|---|---|---|
| Reduced | fade / scale / transitionを0、static text / symbol / audioを維持 | Pass |
| Normal / Fast | 同じstate machineで異なる有限duration | Pass |
| Action順 | 同seed・同human policyの3速度でactor / Action / orderが一致 | Pass |
| archive / result | encoded archive bytes、turn、winner、score、reasonが3速度で一致 | Pass |
| input lock | Action演出中のbutton、Help、Rulesをlockし、二重clickを0 Action・無音で無視 | Pass |
| lifecycle | HelpはCPU commit前waitだけをcancel。終了・別route・destroyでcoroutineとlockを解放 | Pass |
| terminal | 最後のcard / wild feedback完了後にResultへ遷移 | Pass |

主要fixtureは次のとおり。

- `ProductActionFeedbackTests.PresentationSpeedChangesTimingOnlyNotSemanticOrderOrGameOutcome`
- `ProductPresentationTests.PresentationPolicyChangesOnlyTimingAndStopsReducedMotion`
- `ProductFlowTests.FullMatchRematchTitleAndErrorModalUseScreenControls`
- `ProductFlowTests.ContextHelpPausesCpuAndDestroyCancelsTheResumedWait`
- `ProductFlowTests.TutorialCompletesWithPointerAndSubmitThenCanBeRestarted`
- `ProductFlowTests.PresentationReportsRouteSubmitAndErrorEvents`
- `ProductFlowTests.PresentationSpeedsPreserveActionSaveSnapshotAndFocusLock`

## 実行結果

| 検証 | 結果 |
|---|---|
| Product Unity EditMode | Pass、96/96 |
| Product Unity PlayMode | Pass、10/10 |
| generator compile / asset validation | Pass、AudioListener 1、AudioSource 2、WAV 11 |
| WAV再生成決定性 | Pass、再生成前後の11件のSHA-256が全件一致 |
| `dotnet build TrumpGameLab.sln -m:1` | Pass、警告0、エラー0 |
| `dotnet test tests/TrumpLab.Tests` | Pass、262/262 |
| Bash / PowerShell migration | Pass / Pass |
| Unity Standard EditMode | Pass、260/260 |

再現コマンド：

```powershell
& <Unity.exe> -batchmode -nographics -quit -accept-apiupdate `
  -projectPath ./Unity/TrumpGameLab `
  -executeMethod TrumpLab.Product.Editor.ProductProjectGenerator.GenerateCommandLine `
  -logFile ./TestResults/product-generate.log
pwsh ./scripts/run-product-unity-tests.ps1 -UnityPath <Unity.exe>
```

## Release candidateで再実施する手動matrix

Windows 11 x64 Player、build hash、出力device、実施者、実施日を記録し、上表10 cueを
`visible` / `audible` / `他cueと聞き分け可能`の3列で確認する。master / music / SFXは各0 / 50 / 100、
演出はReduced / Normal / Fastを確認する。この実device受入、audio device切断、performance / soakは
それぞれM06-T06、M06-T05のgateであり、batch testの合格だけで代替しない。
