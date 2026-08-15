# 戦争監査記録
資料は[Bicycle: War](https://bicyclecards.com/how-to-play/war)および[Pagat: War](https://www.pagat.com/war/war.html)（ともに2026-08-15直接確認）。
|項目|資料|実装・判断|
|---|---|---|
|最高rank/war伏札|標準規則|`WarGame`確認|
|2人war|同rank時は各1枚伏せ、1枚表。札不足時処理は2variant|`war_down_cards=1`、札不足は戦えるplayerがpot取得|
|3～4人war|最高rank同点時も全員が伏札・表札を出す|現状は同点playerだけが次のbattleへ参加|
|終了|1人が全札を獲得するまで|既定10000 turnで打切り、最多札をwinnerとする出典外終了|
乱数とdeck観測境界は確認済みだが、多人数warと既定終了条件が一致しないため`RuleSpecific`を維持する。
