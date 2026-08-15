# システム設計

ルールをUnity表示層から分離したモジュラーモノリスとする。

| コンポーネント | 責務 |
|---|---|
| Core | Card、Action、IGame、決定的乱数 |
| Registry | ゲーム情報、人数検証、seed付き生成 |
| Games | 合法手、状態遷移、得点、CPU方策 |
| Simulation | CPU実行、停止性・合法性検査、集計 |
| Catalogue | 92候補、生成ID、正式実装への移行状態 |
| CLI | 一覧、台帳、試遊、シミュレーション |
| Unity UI | `IGame`だけを介して表示・入力する製品層 |

依存方向は`Core ← Games ← Registry/Simulation ← CLI・Unity UI`とし、Coreは具体ゲームや
UnityEngineに依存しない。RuntimeはUPMパッケージかつ.NET Standardライブラリとして
一度だけ実装し、CLIとUnityの双方から参照する。
