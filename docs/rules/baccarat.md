# Baccarat検証仕様

## 判定

`Verified`。参照日は2026-08-15。採用variantはPagat掲載の8-deckオンラインPunto Bancoを、
1 unitの固定bet・単一coupへ正規化したものである。

## 根拠

- [Pagat: Baccarat / Punto Banco](https://www.pagat.com/banking/baccarat.html)

## 採用規則

- 8組416枚をshuffleし、各参加者はPlayer、Banker、Tieのいずれかへ1 unit賭ける。
- Aceは1、2～9は額面、10・J・Q・Kは0とし、合計の1の位をhand値とする。PlayerとBankerへ
  交互に2枚ずつ配る。
- どちらかがnatural 8/9ならstandする。それ以外はPlayer 0～5がdraw、6～7がstandし、
  BankerはPlayer第三札と自身の値によるPunto Banco固定表へ従う。
- 純損益はPlayer的中`+1`、Banker的中`+0.95`（5% commission）、Tie的中`+8`。
  Tie時のPlayer/Banker betはpushの`0`、その他の外れは`-1`とする。

## 正規化と観測境界

Pagatが説明するオンライン版どおり、coupごとshuffleとしburn/cut cardを用いない。複数人のbetを
player番号順に受け付けるが、共有Player/Banker handは1組だけである。決着前は本人のbetだけを
表示し、shoe順と結果札を隠す。決着後は両hand、値、outcome、全純損益を公開する。

## 検証

`EighteenthRuleAuditTests` seed 1801、1810、1880と`CoreContractTests`で、固定seed完走、416枚、
natural stand、第三札表、純損益、shoe順を入れ替えた観測同値、合法CPUを確認した。
未解決差分はない。
