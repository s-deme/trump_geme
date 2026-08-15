# I/O契約設計

RuntimeはコンソールI/Oを行わない。CLIだけが標準入出力を扱い、Unity UIは`View`、
`LegalActions`、`Apply`、`Result`を介して独自表示を構築する。
