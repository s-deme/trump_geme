# 設計判断記録

製品開発で後から覆すコストが高い判断をArchitecture Decision Record（ADR）として残す。
日々の小さな実装判断や、既存のゲーム別採用ルールはここへ重複して記録しない。

## 記録対象

- RuntimeとUnity表示層の公開境界
- セーブ、リプレイ、ネットワークで共有する状態形式
- 製品の参照ゲームや収録範囲
- 後方互換性を変える公開API
- 外部サービス、SDK、永続形式の採用

## 状態

- `Proposed`：検討中
- `Accepted`：採用済み
- `Superseded`：別ADRにより置換済み
- `Rejected`：不採用

## ファイル名

`ADR-NNNN-short-title.md`とし、番号は連番にする。置換時は旧ADRと新ADRの双方からリンクする。

## テンプレート

```markdown
# ADR-NNNN タイトル

- Status: Proposed
- Date: YYYY-MM-DD

## Context

判断が必要になった背景と制約。

## Decision

採用する方針。

## Consequences

得られる利点、受け入れる欠点、今後必要になる作業。
```
