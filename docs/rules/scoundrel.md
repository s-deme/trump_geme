# Scoundrel 検証仕様

## 出典と採用variant

ゲームID `scoundrel` はZach Gage・Kurt Biegによる2011年版。
現行の公式配布窓口は確認できないため、著者名・版・著作権表示を保持する
[Scoundrel v1.0 原ルールPDF](https://aiscoundrel.com/Scoundrel.pdf)を主資料、
[ゴクラクテンの日本語手順](https://gokurakism.com/scoundrel/)を相互確認資料とした。
参照日は2026年8月15日で、採用variantは44枚・health 20の原ルール版である。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 1人 | Registryは1人専用。 |
| 使用カード | Joker、赤A、赤絵札を抜く44枚 | constructorがC/S 26 monster、D 9 weapon、H 9 potionだけを作る。 |
| 配札 | dungeonから表4枚のroom | `FillRoom()`が注入rngのdungeonから4枚まで補充する。 |
| 開始状態 | health 20、weaponなし | 同じ初期値と状態を保持する。 |
| 全フェーズ | roomを避けるか、3枚を順に解決して1枚をcarry | `avoid`、`equip`、`potion`、fight actionを順番に適用し、3枚後に補充する。 |
| 合法手 | roomの任意順の3枚、monsterは素手又は許されるweapon | 現在roomとweapon履歴だけから全actionを列挙する。 |
| 特殊札・例外 | 連続avoid禁止、potionはroomで1回、weaponは前monsterより低い値だけ、weapon交換は強制 | `lastAvoided`、`potionUsed`、`weaponLastMonster`、`equip`で同じ制限を実装する。 |
| 勝敗 | health 0又はdungeon踏破 | `health depleted` / `dungeon cleared`で終了する。 |
| 得点 | 死亡時は現在healthから未処理monster合計を引く。踏破時はhealth、health 20で最後のdungeon cardがpotionならその値を加える | `finalScore`を同式で算出する。 |
| 終了条件 | 上記 | 不完全な最終roomが生じた時点で踏破とし、最後にdungeonから出た札を保存してpotion bonusを判定する。 |
| ローカルルール | なし | 紙のhealth記録を内部整数へ置換するだけで、追加optionはない。 |

## CLI/Unityへの正規化

カードを物理的に順番に取る行為を、room index付きの逐次`Action`へ置換する。任意順、avoid、
素手/weaponの選択は全て残る。未処理monsterの合計と最後のdungeon cardは表示のためではなく
原ルール得点のためだけに内部保持し、乱数は注入rngのみを使う。

## 実装・テスト・差分

監査で、死亡得点が現在healthを落としていたこと、最終potion bonusがcarry card併存時に落ちることを
発見し、`ScoundrelGame`を修正した。
`InitialRuleAuditTests.ScoundrelDeathScoreIncludesCurrentHealthAndRemainingMonsters`と
`ScoundrelScoresTheFinalDungeonPotionEvenWhenAnotherRoomCardCarriesOver`はseed 95で両得点境界を、
既存`CoreContractTests.ScoundrelCannotAvoidTwoRoomsInSuccession`は連続avoid例外を検証する。秘密情報はない。

未解決差分はない。
