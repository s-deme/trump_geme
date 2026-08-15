# 戦争検証仕様

状態は`Verified`。参照日は2026-08-15。採用variantは[Bicycle: War](https://bicyclecards.com/how-to-play/war)と[Pagat: War](https://www.pagat.com/war/war.html)に基づく2～4人版である。

- 全員が同時に表札を出し、最高rankがpotを取る。最高rankが同点なら、脱落していない全員が伏札1枚と表札1枚を追加する。
- 札不足者は手持ちをすべて出し、比較可能な表札を持つplayer間でwarを続ける。獲得potは注入rngでshuffleしてdeck末尾へ戻す。
- 既定は1人が全札を得るまで続ける。`max_rounds`を明示した場合だけCLI用の局所打切りとする。

`TwentiethRuleAuditTests`は4人warで、最初の同点者以外も次の表札によりpotを獲得できる境界と固定seed決定性を確認する。採用範囲に未解決差分はない。
