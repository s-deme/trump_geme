# 設定設計

ゲーム生成時に`IReadOnlyDictionary<string,string>`で受け取り、型変換は`GameOptions`へ
集約する。設定は生成したインスタンスだけに作用し、Registryへ書き戻さない。
