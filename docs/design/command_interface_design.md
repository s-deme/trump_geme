# コマンド設計

`list`、`catalogue [--pending]`、`simulate GAME`、`compare`、`play GAME`を提供する。
`--players`、`--seed`、`--difficulty`、反復可能な`--option key=value`を維持する。

CPU比較の再現条件をそろえるため、既定の`--difficulty`は互換ID`1`（Standard）とする。
Crazy Eightsは`1`（Standard）、`2`（Easy）、`3`（Hard）を正式対応し、他gameは対応IDを
`GameInfo`から検証する。`play`、`simulate`、`compare`は非対応値を黙って無視せず拒否する。
追加方策は観測可能情報だけを使い、固定seed再現性と難易度ごとの合法CPU試験を先に追加する。

`compare`は既定で`Verified`候補を各100試合実行する。`--all`は全候補、`--pending`は
`RuleSpecific`候補、反復可能な`--game ID`は明示した候補だけを対象にする。これらの対象指定は
同時に使えない。`--format table|csv|json`と`--output PATH`を提供し、完走数、平均turn、draw、
席別勝数、実測時間、100試合換算時間、NFR-PERF-001の60秒判定、失敗seedを出力する。
