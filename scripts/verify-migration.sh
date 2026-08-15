#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"

if rg -n -i 'python|pytest|unittest|trump_lab|\.py\b' \
    README.md AGENTS.md docs Packages tools tests; then
    echo "Legacy Python reference detected." >&2
    exit 1
fi

if rg -n '\bUnityEngine\b' Packages/com.trump-game-lab.rules/Runtime; then
    echo "Runtime must remain independent of UnityEngine." >&2
    exit 1
fi

test "$(dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue | tail -1)" = "合計 92 件"
test "$(dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue --pending | tail -1)" = "合計 75 件"

expected_verified=$'baohuang\nbohemian_schneider\nbriscola\ncard_capture\ncribbage\ncrisp\ndaifugo_two\ndurak\ngerman_whist\ngin_rummy\ngosankyo\nnapoleon\nofficer_skat\nscoundrel\nsono\nsuper_trump\ntrump_crew'
actual_verified="$(dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue |
    awk '$2 == "Verified" { sub(/^implemented:/, "", $1); print $1 }' | sort)"
test "$actual_verified" = "$expected_verified"

for audit in \
    'trump_crew:trump-crew' 'baohuang:baohuang' 'napoleon:napoleon' \
    'card_capture:card_capture' 'scoundrel:scoundrel' 'gosankyo:gosankyo' \
    'german_whist:german_whist' 'gin_rummy:gin_rummy' 'sono:sono' 'crisp:crisp' \
    'cribbage:cribbage' 'super_trump:super_trump' 'daifugo_two:daifugo_two' \
    'briscola:briscola' 'bohemian_schneider:bohemian_schneider' 'durak:durak' \
    'officer_skat:officer_skat'; do
    id="${audit%%:*}"
    document="${audit#*:}"
    test -f "docs/rules/$document.md"
    rg -F '| `Verified` | `'"$id"'` |' docs/rules/candidate-rules.md >/dev/null
done
echo "Migration verification passed."
