# セブンブリッジ検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Seven Bridge](https://www.pagat.com/rummy/7bridge.html)と[任天堂](https://www.nintendo.com/jp/others/playing_cards/howtoplay/seven_bridge/index.html)を基にPagat 2～5人版を採用する。

- 各7枚、A low、draw→meld/layoff→discard、通常手番経験後のPon優先と次playerだけのChiを扱う。
- 通常set/runに加え、異suitの7二枚、同suit 6-7／7-8を2枚meldとして認める。
- stock切れでは進展がある限りdiscardを繰り返し再利用し、一巡して進展がなければ再配布する。
- 7=20点、初公開なし7枚上がり倍を累積し、既定200点へ達したsession winnerを決める。

`TwentyFirstRuleAuditTests`は2枚7 meldを、固定seed監査はsession、再利用、決定性を確認する。未解決差分はない。
