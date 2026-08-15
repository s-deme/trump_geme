# Crazy Eights監査記録

資料は[Bicycle: Crazy Eights](https://bicyclecards.com/how-to-play/crazy-eights)（2026-08-15直接確認）の基本版を採用する。`wild_rank`を明示した場合だけローカルvariantとし、Verified判定は既定8で行う。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deck/deal | 52枚、人数によらず各5枚。starterが8なら山へ埋めて次を開く | 2人でも5枚、初期top非8を固定seed確認 |
| play | topと同suitまたは同rank、8は常時playでき次suitを指定 | 8のsuitをAction値で明示 |
| draw/pass | 合法札があっても任意にdraw可能。play可能になるかstockが尽きるまでdrawし、尽きてplay不能ならpass | drawを常時合法化。全員pass膠着を避けるため、Pagat基本版のtop以外再shuffleを合成 |
| score | 最初に手札をなくしたplayerが、相手の残札点（8=50、10/J/Q/K=10、A=1、他pip）を受け取る | 勝者を全残札点、各敗者を自身の残札負点とするゼロ和表現で独立照合 |

相手手札とstockを交換してもView・合法手・CPU選択は同値であり、固定seedは完走する。ただしBicycle版にPagatのstock再利用を合成した差は解消していない。Bicycleの全員pass膠着時の終了も資料にないため、`RuleSpecific`を維持する。
