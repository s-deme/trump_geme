# Oh Hell検証仕様

## 判定

`Verified`。参照日は2026-08-15。採用variantはPagat掲載のdealer hook付き標準ラウンド列と、
同ページが「最も広く使われる」とする実trick＋的中10点方式である。

## 根拠

- [Pagat: Oh Hell](https://www.pagat.com/exact/ohhell.html)

## 採用規則

- 3～5人は10枚、6人は8枚、7人は7枚から1枚ずつ減らして1枚handまで行い、その後2枚から
  同じ最大枚数まで増やす。dealerはhandごとに時計回りで交代する。
- 各handの次札を公開してtrumpを決める。dealer左から0～hand枚数を順にbidし、最後のdealerは
  全bid合計がhandのtrick数と等しくなる値を選べない。
- dealer左がleadし、must-follow、follow不能なら任意札、最高trumpまたはlead suit最高札が
  trickを取る。
- 各handで全playerが取ったtrick数を得点し、bid的中者だけ10点を追加する。全ラウンド後の
  累積最高点を勝者とし、同点も結果として保持する。

## 観測境界

trump、dealer、公開bid、取得trick、累積score、現在trickは全員へ表示し、各手札は本人だけへ
表示する。CPUは自身の手札とこれら公開情報だけでbid/playを選ぶ。

## 検証

`SeventeenthRuleAuditTests` seed 1703、1730、1782で、固定seed完走、10→1→10の3人列、
dealer hook、的中12点／外れ実trick点、相手手札を入れ替えた観測同値と合法CPUを確認した。
未解決差分はない。
