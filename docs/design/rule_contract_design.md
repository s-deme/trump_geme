# ゲームルール契約設計

全ゲームは`IGame`を実装し、通常は`GameBase`を継承する。

- `LegalActions`は現手番だけを許可し、副作用を持たない。
- `Apply`は直前の合法手だけを受理し、状態変更を一か所へ集約する。
- `IsTerminal`、`Result`、`View`はゲーム内部状態を変更しない。
- `ChooseCpuAction`は必ず`LegalActions`の要素を返す。
- `CurrentPlayer`は常に`0 <= value < Players`とする。
- `Card`と`Action`は値等価で、結果の得点数はプレイヤー数と一致させる。
- CPUの判断材料は対象プレイヤーが観測できる情報に限定する。
- 乱数はファクトリーから注入された`DeterministicRandom`だけを使用する。
- ゲーム設定はインスタンスごとの読み取り専用辞書として受け取る。
