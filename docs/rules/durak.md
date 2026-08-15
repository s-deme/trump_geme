# デュラック検証仕様

## 出典と採用variant

公式の統一競技規則は確認できないため、John McLeod編の
[Pagat: Podkidnoy Durak](https://www.pagat.com/beating/podkidnoy_durak.html)を信頼できる採用資料とした。
参照日は2026年8月15日。採用variantは2人・36枚・Podkidnoy Durakで、同資料のvariationとして明記される
「trump 6を表trumpと交換しない」版である。Perevodnoy（転送）と24枚短縮版は採用しない。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2～6人で可。採用は2人 | Registryと`DurakGame`は2人専用。 |
| 使用カード | 6～Aの36枚 | 同rank集合の36枚を生成する。 |
| 配札 | 各6枚、次札を表trump、残りをtalon | constructorが6枚ずつ配り、stock先頭を公開trumpとして保持・表示する。 |
| 開始状態 | 初handは最低trump保持者がattacker | 最低trumpの保持者を`attacker`に選ぶ。 |
| 全フェーズ | attack、defend／take、追加attack又はpass、bout終了、補充、終局 | `phase`、`EndBout()`、`Refill()`で遷移する。 |
| 合法手 | attackは場にあるrank、defenseは同suit上位又はtrump、take／pass。attack上限はmin(6, defender開始手札) | `LegalActions()`、`Covers()`、`attackLimit`が対応する。 |
| 特殊札・例外 | trumpでoff-suitを防御でき、follow不要。defenderがtake後も追加attack可。trump 6交換は採用しないvariation | `cover`、`take`、`defenderTaking`で前二者を実装し、交換Actionを出さない。 |
| 勝敗 | talon後、bout終了時に最後に札を残す側がdurak。両者空ならdraw | `Result()`がdurak又はdrawを返す。 |
| 得点 | 通常は勝者点でなく敗者（durak）の識別 | `Scores`は残札の負値、`Extra[durak]`で敗者を返すCLI正規化。 |
| 終了条件 | talon空かつbout終了時に一方又は両方が空手札 | `EndBout()`後に同条件を判定する。 |
| ローカルルール | trump 6交換、転送、24枚版はvariation | no-exchange・36枚・non-transferを固定する。 |

## CLI/Unityへの正規化

2人戦ではattack側とdefender側が一意なので、物理的なboutの追加attackを逐次`Action`へ分解した。
被覆済み札の束はtable pairへ、捨札は状態外へ正規化する。公開trump札は`View`に出し、相手手札・talon順は
表示もCPU入力も行わない。採用したno-exchange variantでは交換選択そのものが存在しない。

## 実装・テスト・差分

資料上公開の表trump札がViewに無かったため、`face_up_trump`を表示するよう修正した。
`ThirdRuleAuditTests.DurakFixedSeedCompletesAndAllowsTrumpDefenseWithoutFollowingSuit`はseed 305/306で
終局、off-suit trump防御、公開trumpを確認する。同
`DurakViewAndCpuIgnoreTheDefendersPrivateHand`は防御側手札の観測同値試験である。

未解決差分はない。
