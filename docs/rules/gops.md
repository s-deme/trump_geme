# GOPS検証仕様

資料は[Pagat: GOPS](https://www.pagat.com/misc/gops.html)（2026-08-15直接確認）の基本2人版を採用する。3人以上variantと、同点賞札を捨てるvariationは採用しない。

| 項目 | 採用規則 | 実装・検証 |
|---|---|---|
| setup | diamond 13枚をshuffleした賞札、P0はspade、P1はclubのA～Kを各1枚。A=1～K=13 | 注入乱数だけで賞札をshuffle |
| bid | 公開賞札に各1枚を同時・秘密入札し、高い側が賞点を獲得。使用bidは捨てる | P0→P1の逐次Actionへ正規化し、P0 bidはP1のView・合法手・CPUから非公開 |
| tie | 同点賞札をcarryし、次賞札とまとめて競る。最終bidも同点ならcarry全体は未獲得 | `prizePot`へ累積し、`Extra[unclaimed]`へ保持 |
| end | 13 bid後、獲得diamond点合計が高いplayerの勝利 | 両scoreとunclaimedの合計91を確認 |

逐次化は同時選択の情報境界を保っており、採用範囲に未解決差分はない。`Verified`とする。
