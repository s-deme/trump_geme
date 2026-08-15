# たぬき監査記録

状態は`Verified`。資料は[ゴクラキズムの完全規則](https://gokurakism.com/tanuki/)
（参照日: 2026-08-15）。同ページの3人・9ディール規則を全体として採用する。

| 項目 | 完全規則 | `TanukiGame` |
|---|---|---|
| deck・役割 | 2～5とJokerを除く36枚。dealer左=trump、dealer=minus、dealer右=plus | 36枚を各12枚。3人が各役のsuitを`choose_suit`で秘密選択 |
| trick | A high。1～3局may、4～6局must、7～9局may follow | `dealsPlayed`境界で同じfollow制約。勝者はtrump優先、なければlead suit最高 |
| trump公開 | off-leadのtrumpが勝敗へ関与する最初のtrickで公開 | 条件成立時だけ`trumpRevealed`を立てる |
| 局末公開・得点 | 全役suit公開。plus +1、minus -1。同一suitなら同色+1・反対色-1 | 得点式を一致させ、完了局を`revealed_roles`へ全員共通で保持 |
| 終了 | dealerを交代して9局、合計最高点 | 9局終了、最高点者（同点併記） |

局中は各viewer自身の役とsuitだけを表示し、完了局の公開情報だけを履歴化する。CPUは自分の手札と
公開済みtrick以外を参照しない。`EighthRuleAuditTests`はseed 801/830で完走、may/must/may境界、
第1局終了後の全役公開を確認し、seed 1001で相手2手札交換後もView・合法手・CPUが同値であることを
確認する。未解決差分はない。
