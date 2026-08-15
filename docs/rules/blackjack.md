# Blackjack検証仕様

状態は`Verified`。参照日は2026-08-15。採用variantは[Bicycle: Blackjack](https://bicyclecards.com/how-to-play/blackjack)のS17基本版である。

- dealerは17以上でstandし、通常1:1、natural 3:2、insurance 2:1、pushは賭金返却とする。
- 最初の任意の2枚からdoubleでき、同rank pairは資金と上限が許す限りsplitできる。split Aceにも通常のhit/doubleを認める。
- dealer hole card、shoe順、他playerのhandはViewとCPUから隔離する。

`TwentiethRuleAuditTests`は任意2枚doubleとsplit Ace継続を固定seedで確認する。採用house境界は生成時optionに閉じ、未解決差分はない。
