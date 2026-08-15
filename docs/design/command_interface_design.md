# コマンド設計

`list`、`catalogue [--pending]`、`simulate GAME`、`compare`、`play GAME`を提供する。
`--players`、`--seed`、`--difficulty`、反復可能な`--option key=value`を維持する。

CPU比較の再現条件をそろえるため、現在正式対応する`--difficulty`は`1`だけとする。
`play`と`simulate`はそれ以外を黙って無視せず拒否する。将来難易度を追加するときは、
観測可能情報だけを使う方策、固定seed再現性、難易度ごとの合法CPU試験を先に追加する。

`compare`は既定で`Verified`候補を各100試合実行する。`--all`は全候補、`--pending`は
`RuleSpecific`候補、反復可能な`--game ID`は明示した候補だけを対象にする。これらの対象指定は
同時に使えない。`--format table|csv|json`と`--output PATH`を提供し、完走数、平均turn、draw、
席別勝数、実測時間、100試合換算時間、NFR-PERF-001の60秒判定、失敗seedを出力する。
