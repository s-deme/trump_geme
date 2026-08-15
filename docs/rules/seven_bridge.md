# セブンブリッジ監査記録

従来の「完全原典未取得」を再検索し、[Pagat: Seven Bridge](https://www.pagat.com/rummy/7bridge.html)および[任天堂: セブンブリッジ](https://www.nintendo.com/jp/others/playing_cards/howtoplay/seven_bridge/index.html)（2026-08-15直接確認）の公開完全規則を取得した。Pagat基本版を採用候補として照合した。

一致する範囲は52枚、7枚配札、A low、山札draw→任意meld/layoff→必須discard、通常手番経験後のPon優先・次playerだけのChi、7=20点、初公開なしの7枚一括上がり倍である。Pon候補を捨てたplayerの左から逐次照会し、その後に次playerのChiを照会する正規化は優先順位と情報境界を保つ。

未解決差分は次のとおり。

- 7を含む2枚meld（異suitの7二枚、同suit 6-7／7-8）が合法手にない。
- Pagatは2～5人だが実装は2～6人である。
- Pagatは必要なら捨て札を複数回再利用するが、実装は1回後に引き分ける。
- Pagatは勝者累積点が合意目標（例200）へ達するsessionだが、実装は1 handで終了する。

固定seed 1601は完走するが、meld・終了・sessionの中核差が残るため`RuleSpecific`を維持する。
