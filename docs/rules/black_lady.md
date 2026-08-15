# Black Lady検証仕様

## 判定

`Verified`。参照日は2026-08-15。採用variantはゴクラキズムとゲームファームが掲載する
「なかよし村」系の3～7人clear/carry方式であり、一般的なshoot-the-moon版とは区別する。

## 根拠

- [ゴクラキズム: ブラックレディー](https://gokurakism.com/black_lady/)
- [ゲームファーム: ブラックレディー（なかよし村ルール）](https://gamefarm.jp/rule/blacklady.html)

## 採用規則

- 52枚を3～7人へ均等に配れる最大枚数だけ配り、余りをtableへ置く。余り2枚なら1枚、
  3～4枚なら2枚を表向きにする。
- 全員が右隣へ2枚を裏向きにpassし、受領後にさらに右隣へ1枚をpassする。
- dealer左から任意札をleadし、no trump・must-followで、lead suit最高札がtrickを取る。
  最終trickの勝者はtableの表裏すべての余り札も獲得する。
- heart各1点、spade Q13点を減点する。減点札0枚のclear者はcarryを含む26点を等分し、端数、
  またはclear者なしの全額を次dealへcarryする。
- 既定は各playerが1回dealerとなる`players` deals。終了後の累積最高点を勝者とする。

## 正規化と観測境界

既存`rounds`オプションは表示・CLI互換のためdeal数を直接指定する局所短縮／延長として残す。
規則上表向きのtable札を全viewerへ追加表示し、残りは枚数だけを示す。pass札、相手手札、裏向き
table札は隠し、CPUは自身の手札と公開情報だけでpass/playを選ぶ。

## 検証

`EighteenthRuleAuditTests` seed 1803、1820、1881で、固定seed完走、5人10枚＋表1/裏1、
2枚→1枚pass、clear 3人の8点とcarry 2点、相手手札を入れ替えた観測同値、合法CPUを確認した。
未解決差分はない。
