# Briscola Chiamata検証仕様

資料は[ゴクラキズム: ブリスコラ・キアマタ](https://gokurakism.com/briscola_chiamata/)（2026-08-15直接確認）の5人版を採用する。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| deck/rank | 8・9・10を除く40枚を各8枚。A>3>K>Q>J>7>6>5>4>2、card point合計120 | pack、rank、pointを独立得点テストで照合 |
| auction | dealer右隣から反時計回り。弱いrankだけを上乗せできるhard pass。全員passは同じdealerが再配布 | 逐次`bid_rank`とdealer不変境界を確認 |
| partner/trump | declarerがtrumpを選び、bid rankのtrump保持者が秘密partner。自分で保持なら1対4 | 呼札が出るまで`partner=hidden`、本人だけ`your_role`で認識 |
| play | declarer lead、反時計回り、must-follow | follow可能時の合法手をlead suitだけに限定する境界を全trickで確認 |
| score/session | declarer側61点以上で勝利。2対3はdeclarer ±2、partner ±1、相手 ∓1、soloは±4、全trickは倍。最初の11点で終了 | 1dealのtrickとcard pointを独立集計して累計点と照合し、固定seedで11点まで完走 |

相手手札を入れ替えてもView・合法手・CPU選択は同値である。採用範囲に未解決差分はなく、`Verified`とする。
