# Trump Crew検証仕様

## 検証状態と参照資料

ゲームID `trump_crew` は、次の資料と仕様・実装・シナリオテストを項目別に照合済みとする。

- 草場純「[トランプクルーのルール](https://kusabazyun.banjoyugi.net/%E8%8D%89%E5%A0%B4%E7%B4%94%E3%81%95%E3%82%93%E3%81%AE%E7%A0%94%E7%A9%B6%E5%8D%9A%E7%89%A9%E9%A4%A8-on-web/%E6%96%87%E6%9B%B8/%E3%83%88%E3%83%A9%E3%83%B3%E3%83%97%E3%82%B2%E3%83%BC%E3%83%A0%E3%81%AE%E9%83%A8%E5%B1%8B/%E3%83%88%E3%83%A9%E3%83%B3%E3%83%97%E3%82%AF%E3%83%AB%E3%83%BC%E3%81%AE%E3%83%AB%E3%83%BC%E3%83%AB)」：人数、用具、stage、宣言、Joker例外、成功・再挑戦・終了の一次照合先。
- ゴクラキズム「[協力型トリックテイキング：トランプクルー](https://gokurakism.com/tcrew/)」：人数別stage数、配札、余り札、bid、プレイの補助照合先。同記事も上記草場資料を参照している。

参照日は2026年8月14日。原資料間で実装対象項目の矛盾はない。

## 採用ルール

| 項目 | 採用仕様 | 検証箇所 |
|---|---|---|
| 人数・stage上限 | Runtime対象は3～5人。標準上限は3人17、4人13、5人10（52÷人数、余り切捨て） | 生成境界、固定seedテスト |
| 用具・配札 | 標準52枚とJoker 1枚。stage Nではdealer左から各N枚を配る。次の余り1枚だけを表にする | 固定seedの手札、trump表示 |
| 切札 | 表向き余り札のsuit。余り札がJokerならno-trump。表示札と残札はプレイしない | seed 19のno-trump、各人数完走 |
| dealer | 初回はP0、各deal後は成功・失敗を問わず左隣へ交代 | 成功・失敗シナリオ |
| 強度宣言 | dealerだけが自分の手札とtrumpを見てweak/middle/strongのいずれかを主観で公開する | `announce_strength`合法手 |
| bid | dealer左から時計回りに0～残chip数を公開宣言する。dealerは最後にstage数－他者合計を強制される | stage 1固定seedシナリオ |
| カード交換 | なし | phase/action集合 |
| 通常の合法手 | lead suitを持つ場合はmust-follow。持たなければ任意札 | stage 2固定seedシナリオ |
| Jokerの合法手 | follow可能な通常札を持っていても常に出せる。Jokerを出す義務はない | seed 50固定seedシナリオ |
| Joker lead | C/D/H/Sの指定または無指定を選ぶ。指定時はそのsuitをmust-follow、無指定時は全員任意札 | seed 42固定seedシナリオ |
| trickの強さ | Joker、trump A～2、lead suit A～2の順。最高札のplayerが次をleadする | Joker勝利・通常勝者テスト |
| stage成功 | 全員の獲得trick数が各bidと完全一致した場合だけ成功し、次stageへ進む | seed 42成功シナリオ |
| stage失敗 | dealerを交代し、同じstageを新しい配札で再挑戦する | seed 42失敗シナリオ |
| ゲーム終了 | 最終stage成功で全員勝利。再挑戦回数に標準上限はない | 短縮campaign、任意試行上限テスト |

## 明示的な基盤ローカル仕様

- 原資料はより広い人数を扱うが、この基盤のCLI/Unity契約は従来互換の3～5人に限定する。
- `final_stage`は練習・テスト用の短縮optionで、1～52÷人数に丸める。未指定時は原資料どおりの人数別上限を使う。
- 原資料どおり`max_attempts`の既定値は0（無制限）とする。自動実行や練習を有限回で打ち切る場合だけ、正整数を指定するとその回数の失敗でcampaign未達終了とする。
- 会話による手札・方針の伝達は禁止という原資料に合わせ、Runtimeに通信actionは設けない。CLI外の自由会話もルール状態には影響しない。
- 得点ゲームではないため、成功時は全員をwinnerとして最終stage数を共通scoreにし、試行上限終了時はwinnerなし・完了済みstage数を共通scoreにする。これは`IGame`結果契約への正規化である。

## CPUの観測と方策

CPUが参照するのは、自分の手札、公開trump、dealerの公開強度、全員の公開bid・獲得数、
現在の公開trickだけである。相手の手札、未使用札、山札順は参照しない。観測が同じで相手の
非公開手札だけが異なる固定seed対に対し、同じ宣言・bidを返すテストを置く。

bidはstageの均等期待値を基準に、自分のJoker・trump・rank強度とdealerの公開強度で補正する。
プレイ時は、未達のplayerが現在勝っていれば勝ちを譲り、自分だけが未達なら必要最小の勝札を使う。
自分がleadするときは未達なら強札、達成済みなら弱札を選ぶ。Joker leadは他playerの自由度を
最大にする無指定を既定選択とする。この方策は完全情報探索ではなく、公開情報だけを使う協力的heuristicである。

## テストと既知差分

`CoreContractTests`で人数別上限、Joker余り札、宣言、dealer残数bid、通常follow、Joker例外、
Joker lead指定/無指定、Joker最強、stage成功、失敗再挑戦、任意の5回上限、標準の無制限再挑戦、観測同値、
CPUによる短縮campaign完走、決定性、人数境界と複数seed完走を検証する。

上記の明示的ローカル仕様を除き、参照資料・本仕様・Runtime実装・テスト間の既知の不一致はない。
