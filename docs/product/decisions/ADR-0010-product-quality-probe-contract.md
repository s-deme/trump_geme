# ADR-0010 製品quality probe契約

- Status: Accepted
- Date: 2026-08-20

## Context

[ADR-0006](ADR-0006-product-quality-baseline.md)の`PERF-01`〜`PERF-08`と`REL-01`〜`REL-04`を
再現可能なWindows Playerで測定する必要がある。Editor、Play Mode、wall-clock frame durationだけでは、
release PlayerのCPU時間、private memory、長時間安定性を代替できない。

一方、Unity 6の`GC Allocated In Frame` profiler counterはDevelopment Playerでは利用できるが、
non-Development release Playerでは公開されない。Unityの
[Memory Profiler counter一覧](https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-counters-reference.html)
も同じ制約を明記している。利用不能なcounterを0 bytesとして扱うと`PERF-07`を誤って合格にする。

## Decision

### buildと起動

- `scripts/run-product-quality.ps1`を正本runnerとする。保存済みUnity EditorでWindows x86_64、
  D3D11、VSync on、`1280x720`のPlayerを作る。
- `PERF-01`〜`PERF-06`、`PERF-08`はnon-Development release Playerで測る。
- `PERF-07`だけは同じscene、target、resolution、D3D11設定のDevelopment Playerで
  `GC Allocated In Frame`を使う。Development buildのframe timeやmemory値を他のPERFへ流用しない。
- hostが最小CPU profileより強い場合、全probe processをWindows affinity `0x3`へ固定し、
  2 logical processorだけで実行する。host CPU、system memory、GPU、OS build、effective affinityを
  reportへ残す。memoryはprocess予算でgateし、host memory容量を合格値として扱わない。
- 通常起動ではquality objectを生成しない。明示した`-trumplab-quality-probe`引数だけがdormant probeを
  起動し、network、account、Steam clientを要求しない。

### 測定方法

- 起動は1回をwarm-upとして除外し、その後の独立した3 processがTitleのvisible focusを得るまでを測る。
- CPU frame timeはVSync待ちを含むwall durationではなく、Unityの
  `CPU Main Thread Frame Time` counterを使う。Title、Settings、Match、How to play、Resultを
  release Playerで各60秒測る。
- 入力応答は100回のscreen/focus更新を次frameまで観測する。演出delayは含めない。
- CPU方策は3難易度各100 state、保存系は100 slot、最大archiveはcodecが受理できる1 MiB境界までの
  8,198 Action前後を毎runで探索し、durable writeと全checkpoint replayを測る。
- private memoryはUnity Monoの`System.Diagnostics.Process` wrapperへ依存せず、Windows PSAPIの
  `PROCESS_MEMORY_COUNTERS_EX.PrivateUsage`を使う。5画面各1分をwarm-upとし、その後60分測る。
- allocationはDevelopment Playerで、rule / CPU / replay経路を1局warm-upしてGCとsettleを完了後、
  Title idleと100 Actionを測る。Actionと同じframeの
  `CPU Main Thread Frame Time`も収集し、GCを含むframe stallを`50 ms未満`でgateする。
  release Playerでcounterが利用不能な場合を0 bytesへ丸めない。
- soakは固定seedの自動100局に加え、実時間60分にわたりAction、atomic checkpoint、load/replay、
  screen routeを繰り返す。error/exception log、Action count、archive byte一致、GameObject count、
  presentation/input lock、memory peakを継続監視する。

`Quick`はbuildと経路の反復確認だけに使い、T05の合否証跡には数えない。`Full`のraw JSONとPlayer logは
git対象外の`TestResults/ProductQuality/`へ出し、build SHA-256と合否値を
[M06-T05品質記録](../reports/M06-T05-quality-evidence.md)へ転記する。

### 異常復旧

- session、settings、progressの破損・未知version・改ざんは既存のstrict codecとatomic storeで拒否し、
  原fileを保持する。
- Action適用後のautosaveが失敗した場合はsessionを安全停止し、最後に成功したcheckpointだけを残す。
- audio configuration changeでは既存AudioSource、clip、category volumeを再適用する。gamepadとaudioの
  実device抜き差しはM06-T06のrelease candidate matrixでも再確認する。
- Product Runtimeにnetwork / Steam APIを追加せず、offline機能を静的契約testで固定する。

## Consequences

- releaseに存在しないallocation counterを0として誤判定せず、他の性能値はrelease Playerのまま保てる。
- Windows固有PSAPIとaffinityを使うため、このprobeはM06の正式対象であるWindows専用となる。
- `Quick`と`Full`の結果を混同できず、60分soak、build hash、環境、既知の制限を同じcheckpointへ残せる。
- Player buildと65分以上の実行時間が必要になるが、時刻依存assertを通常のunit testへ混ぜずに済む。
