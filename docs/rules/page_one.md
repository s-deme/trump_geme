# Page One検証仕様

資料は[Pagat: Page One](https://www.pagat.com/inflation/page_one.html)および[Bicycle: Page One](https://bicyclecards.com/how-to-play/page-one)（ともに2026-08-15直接確認）の古典的trick-taking版を採用する。5枚配札、Joker使用制限、stock枯渇時にtrickを拾うvariationは採用しない。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| setup | 52枚＋Joker 1枚、2～5/6人、各4枚。dealer左から時計回り | 対応範囲2～6人、開始playerをP0へ決定論的に正規化 |
| trick/draw | must-follow。従えなければ同suitが出るまで1枚ずつ引き、その札を出す。完了trickはstock枯渇時にshuffle再利用 | `draw`後も同playerを維持。stockも完了trickもなくfollow不能なら原典どおりdraw終了 |
| Joker | いつでも出せて常に勝つ。leadされた場合は第2手がsuitを定める | Jokerをfollow候補へ常時含め、第2手の通常札から`led`を設定 |
| Page One | 2枚から1枚出す際に宣言し、次playerの行動前に忘れたら5枚引く。1枚から引いた最初の札を出す場合も再宣言 | `play_page_one`を宣言、同じ札の`play`を宣言忘れへ正規化して直ちに5枚罰。CPUは必ず宣言 |
| end | 最初に手札0のplayerが単dealのwinnerで、規則上の得点はない | `GameResult`の1/0は勝敗表示専用。draw時は全員0で、残札罰点は付けない |

相手手札とstockの札を交換してもView・合法手・CPU選択は同値である。固定seed 1505/1540～1559/1583で完走・宣言罰を確認し、採用範囲に未解決差分はないため`Verified`とする。
