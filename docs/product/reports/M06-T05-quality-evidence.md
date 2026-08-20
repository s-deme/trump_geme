# M06-T05 異常復旧・性能・長時間安定性記録

## 受入境界

- 実施日：2026-08-20〜21
- OS：Windows 11 x64 build 26200
- Unity：6000.3.22f1
- Player：Windows x86_64、D3D11、`1280x720`、VSync on
- host：Intel Core i5-13400F（16 logical processor）、32581 MiB memory、
  NVIDIA GeForce RTX 4060 Ti
- 最小CPU profile：全probe processをaffinity `0x3`へ固定し、effective 2 logical processor
- release build tree SHA-256：
  `6ddc547d7d5c7e8b9332f5b8021b511f9abd78a218140b5188f1a341663c2a51`
- Development build tree SHA-256：
  `7a21f051565bea6fff8f0fdeb5ef0b4f189ec794ea81f40d2827b93bc2f6b991`

この記録は[ADR-0006](../decisions/ADR-0006-product-quality-baseline.md)の`REL-01`〜`REL-04`、
`PERF-01`〜`PERF-08`と
[ADR-0010](../decisions/ADR-0010-product-quality-probe-contract.md)を、Windows Player、自動test、
再現runnerへ接続する。`Quick`は経路確認だけとし、以下の値は最終sourceからbuildした
`Full`のみから転記した。

## 異常復旧・offline

| ID | 自動証跡 | 結果 |
|---|---|---|
| REL-01 | session / settings / progressのstrict codec、unknown version・破損・改ざん原本保全、atomic replace | Passed（store / contract回帰） |
| REL-02 | Action後autosaveに注入したI/O失敗でsessionを安全停止し、最後に成功したcheckpoint bytes / Action countのみを保持 | Passed（`InterruptedAutosaveRestoresOnlyTheLastAtomicCheckpoint`） |
| REL-03 | gamepad disconnect / reconnect、rebind中disconnect、audio configuration change後のclip / source / category volume復旧、error modal / input lock解放 | Passed（Input System / Product Audio / PlayMode回帰、Full log 0） |
| REL-04 | Product Runtimeのnetwork / Steam / external account API非依存をsource scanし、全Player probeをoffline実行 | Passed（`ProductRuntimeContainsNoNetworkOrSteamDependency`） |

## 性能証跡

### 起動・画面・入力

warm-up processを除外した独立3起動は`4.388 s`、`4.374 s`、`4.392 s`で、
`PERF-01`の各5.0秒以下を満たした。Full process内の参考起動値は`4.365 s`だった。

| ID / context | samples | p95 | p99 | max | 予算 | 結果 |
|---|---:|---:|---:|---:|---|---|
| PERF-02 Title | 3592 | 2.080 ms | 2.409 ms | 3.972 ms | 16.67 / 33.33 / <100 ms | Passed |
| PERF-02 Settings | 3597 | 1.936 ms | 2.250 ms | 3.458 ms | 16.67 / 33.33 / <100 ms | Passed |
| PERF-02 Match | 3597 | 2.011 ms | 2.330 ms | 4.073 ms | 16.67 / 33.33 / <100 ms | Passed |
| PERF-02 How to play | 3597 | 1.923 ms | 2.207 ms | 12.226 ms | 16.67 / 33.33 / <100 ms | Passed |
| PERF-02 Result | 3597 | 2.118 ms | 2.424 ms | 6.511 ms | 16.67 / 33.33 / <100 ms | Passed |
| PERF-03 screen / focus update | 100 | 17.653 ms | 20.675 ms | 45.670 ms | p95 100 / max 200 ms | Passed |

### CPU方策・保存・最大archive

| ID / context | samples | p95 | max | 予算 | 結果 |
|---|---:|---:|---:|---|---|
| PERF-04 easy | 100 | 0.091 ms | 1.016 ms | p95 50 / max 100 ms | Passed |
| PERF-04 standard | 100 | 0.088 ms | 1.094 ms | p95 50 / max 100 ms | Passed |
| PERF-04 hard | 100 | 0.829 ms | 1.867 ms | p95 50 / max 100 ms | Passed |
| PERF-05 atomic save | 100 | 34.838 ms | 47.233 ms | p95 250 / max 500 ms | Passed |
| PERF-05 load | 100 | 4.282 ms | 5.726 ms | p95 250 / max 500 ms | Passed |
| PERF-05 resume | 100 | 1.795 ms | 2.234 ms | p95 250 / max 500 ms | Passed |
| PERF-05 replay | 100 | 3.443 ms | 5.449 ms | p95 250 / max 500 ms | Passed |
| PERF-05 list 100 slots | 100 | 6.842 ms | 58.723 ms | p95 250 / max 500 ms | Passed |

codecが1 MiB上限内で受理する境界は8,198 Action / 1,048,560 bytesだった。
`PERF-05B`のsave / encodeは`62.312 ms`、load / 8,199 checkpoint全replayは
`183.008 ms`で、それぞれ1秒 / 2秒以下を満たした。

### memory・allocation・soak

| ID / context | samples | 実測 | 予算 | 結果 |
|---|---:|---:|---:|---|
| PERF-06 warm private bytes | 1 | 329.402 MiB | 512 MiB以下 | Passed |
| PERF-06 60分peak private bytes | 1 | 331.242 MiB | 768 MiB以下 | Passed |
| PERF-06 warm比増加 | 1 | 1.840 MiB | 64 MiB以下 | Passed |
| PERF-07 Title idle | 3597 frames | p95 / max 0 B/frame | p95 0 B/frame | Passed |
| PERF-07 rule Action | 100 | p95 13,680 B / max 14,404 B | p95 256 KiB以下 | Passed |
| PERF-07 Action frame stall | 100 | p95 4.072 / p99 4.802 / max 5.697 ms | max 50 ms未満 | Passed |

`PERF-08`は、固定seedの独立100局に加え、`3600.035 s`の実時間soakで
1,205局 / 42,400 Actionを実行した。Actionは毎回exactly onceで、100 Actionごとまたは
局終了時のatomic save / load / encoded byte一致を確認した。GameObjectは開始 / peak /
終了すべて`271`、error log `0`、exception log `0`、input / presentation lock残留`0`で
Passedとした。presentation coroutineは単一owner / generation tokenの回帰と、soak後の
transition / context help / input lock全解放を組み合わせ、増え続ける状態を検出する。

## 再現手順

```powershell
pwsh ./scripts/run-product-quality.ps1 -UnityPath <Unity.exe> -Mode Full
```

runnerはreleaseとDevelopmentの出力directoryを作り直し、releaseのみで`PERF-01`〜`06`、
`08`を測る。`PERF-07`だけは同じscene / D3D11 / resolutionのDevelopment Playerで、
rule / CPU / replayを1局warm-up、full GC、30 frame settleの後に測る。全processはaffinity
`0x3`を使う。raw JSON、Player log、buildは`TestResults/ProductQuality/`のgit対象外成果物とする。

## 最終自動検証

| 検証 | 結果 |
|---|---|
| Windows quality probe `Full` | Passed（startup 3 / 3、screen 5 / 5、100局、60分soak、allocation） |
| Product Unity EditMode | Passed（124 / 124、skip 0） |
| Product Unity PlayMode | Passed（17 / 17、skip 0） |
| Unity Standard EditMode | Passed（260 / 260） |
| `dotnet build TrumpGameLab.sln -m:1` | Passed（warning 0、error 0） |
| `dotnet test tests/TrumpLab.Tests` | Passed（262 / 262、skip 0） |
| Bash / PowerShell migration | Passed / Passed |

## 既知の制限と引き継ぎ

| 重大度 | 制限 | 回避策 / owner |
|---|---|---|
| S3 | Windows native screen reader連携とfull WCAG conformanceは正式対応外 | 視覚focus、locale別label、非色情報を使う。M07-T05の公開制限へ記載 |
| S3 | Windows 10はbest-effort・非保証で、今回の正式Player計測はWindows 11だけ | Windows 11 build 26100以降を正式対象とし、Windows 10 smokeはM06-T06の利用可能環境で実施 |
| S3 | host GPU / memoryは最小仕様より強い | CPUは2 logicalへ制限済み。process private bytesとframe budgetをgateし、実機表示はM06-T06で再確認 |
| S3 | 実物Xbox / XInput gamepad、audio device抜き差し、スピーカーで10 cueの主観的な聞き分けはbatchで代替できない | 自動Input / audio configuration回帰はPassed。実device手動matrixはM06-T06のrelease candidate gate |

S0、S1、S2の既知の問題は0件とする。上表の実device / font / layout / audio手動matrixは
M06-T06の完了条件であり、T05の自動証跡で代替しない。
