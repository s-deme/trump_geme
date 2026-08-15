# ハムレット検証仕様

## 出典と採用variant

[ゴクラキズム: ハムレット](https://gokurakism.com/hamlet/)（参照日: 2026-08-15）のDavid Parlett作3人版を採用する。7〜A＋Jokerの33枚、各人がJoker以外の1枚を同時提示してtrump/to-beを決め、250点先取とする。

## 項目別照合

| 項目 | 資料 | Runtime照合 | 判断 |
|---|---|---|---|
| 札・11 trick | 33枚を3人へ全配札 | `StartDeal()` | 一致 |
| mode札境界 | Joker以外の3枚を同時提示し手札へ戻す | 逐次`choose_mode_card`、`ResolveMode()` | 等価正規化 |
| trump/to-be | 同suitならそのsuit、全異suitなら欠けsuit、絵札でto-be | `ResolveMode()` | 一致 |
| Joker/follow | lead最強、void時だけ出せて最弱 | `LegalActions()`/`TrickWinner()` | 一致 |
| Hamlet役・得点 | 中間trick数、to-be/not-to-be点 | `FinishDeal()` | 一致 |

## 観測境界・試験・結論

同時提示を入力順だけ決定論的な逐次Actionへ分解し、未公開mode札・相手手札をView/CPUから隔離する。`FifthRuleAuditTests`はseed 501完走、mode選択境界、二つの相手手札を交換してもView・合法手・CPUが等しい観測同値性を確認する。実装修正・未解決差分はなく`Verified`へ昇格する。
