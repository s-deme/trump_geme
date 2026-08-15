# Euchre検証仕様

## 判定

`Verified`。参照日は2026-08-15。採用variantはBicycle掲載の4人固定ペア・24枚・10点戦である。

## 根拠

- [Bicycle: Euchre](https://bicyclecards.com/how-to-play/euchre)
- 補助照合: [Pagat: Euchre](https://www.pagat.com/euchre/euchre.html) のNorth American Euchre

## 採用規則

- 9、10、J、Q、K、Aの24枚を使い、dealer左から各playerへ3枚ずつ、その後2枚ずつ配る。
- 表札suitをorder upする第1roundと、表札以外をcallする第2roundを行う。全員が2回pass
  した場合は次dealerが配り直す。
- 表札が採用された場合はdealerが表札を加えて1枚捨てる。trumpのJをright bower、同色の
  別suitのJをleft bowerとし、left bowerはfollow判定でもtrumpとして扱う。
- makerは宣言時にaloneを選べる。partnerは不参加となり、通常はdealer左、そこが不参加なら
  dealer向かいからleadする。
- makerが3～4 trickなら1点、5 trickなら2点、aloneで5 trickなら4点、euchreならdefender
  teamへ2点。先に10点へ達したteamを勝者とする。

## 実装・観測境界

配札をBicycleの3枚＋2枚packetへ補正した。表札、trump、maker、alone、team score、公開trickは
全員へ表示し、各手札は本人だけへ表示する。CPUは自身の手札と公開状態だけでorder/call/playを
選ぶ。ローカル短縮は既存互換の`target_score`だけであり、規則状態をglobalへ残さない。

## 検証

`SeventeenthRuleAuditTests` seed 1702、1720、1781で、固定seed完走、packet配札、表札境界、
alone march 4点、相手手札を入れ替えた観測同値と合法CPUを確認した。未解決差分はない。
