# ネイブ検証仕様

## 資料・採用variant

[White Knuckle Cards: Knaves](https://whiteknucklecards.com/games/knaves.html)（参照日: 2026-08-15）の
3人標準版を採用する。2/4〜6人のvariationは採用しない。

## 項目別照合

| 項目 | 資料 | Runtime | 監査判断 |
|---|---|---|---|
| 人数・札 | 3人、標準52枚 | `KnaveGame` | 一致 |
| 配札・開始 | 各17枚を1枚ずつ配り、残る1枚を表向きtrump。dealer左がlead | `HandSize()`、`ChooseTrump()`、`StartRound()` | 一致 |
| play | follow可能なら強制、voidなら任意。最高trump、なければlead suit最高が勝つ | `LegalActions()`、`Beats()` | 一致 |
| 得点 | trickごとに+1、JH=-4/JD=-3/JC=-2/JS=-1 | `OnTrickWon()`、`ScoreRound()` | 一致 |
| match | 複数handで20点到達 | `MatchOver()` | 一致 |

## 正規化・観測境界

3人の物理trickを3個の逐次`play`へ正規化する。dealerの初期選定は注入seedのP0開始へ固定し、以後は
roundごとに交代する。CPUは`LegalActions()`、公開trump、自手札だけを使い、相手手札とturn-up前のdeck順を
Viewへ出さない。

## 固定seed検証・結論

`FourthRuleAuditTests`はseed 401の完走、seed 425の二つの相手手札だけを変えた観測同値性を確認する。
追加のseed 430は17枚×3、dealer左lead、表向きtrump、JH/JD/JCを同一trickで取った際の1-4-3-2=-8を固定する。

資料URL、採用variant、実装、固定seed、観測境界に未解決差分はないため`Verified`とする。
