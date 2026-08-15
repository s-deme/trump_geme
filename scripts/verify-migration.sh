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

for directory in \
    Packages/com.trump-game-lab.rules/Runtime/bin \
    Packages/com.trump-game-lab.rules/Runtime/obj; do
    if test -d "$directory"; then
        echo "Generated .NET build directory must stay outside the Unity package: $directory" >&2
        exit 1
    fi
done

test "$(dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue | tail -1)" = "合計 92 件"
test "$(dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue --pending | tail -1)" = "合計 0 件"

actual_verified="$(dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue |
    awk '$2 == "Verified" { sub(/^implemented:/, "", $1); print $1 }' | sort)"
test "$(printf '%s\n' "$actual_verified" | awk 'NF { count++ } END { print count+0 }')" = "92"

for audit in \
    'trump_crew:trump-crew' 'baohuang:baohuang' 'napoleon:napoleon' \
    'card_capture:card_capture' 'scoundrel:scoundrel' 'gosankyo:gosankyo' \
    'german_whist:german_whist' 'gin_rummy:gin_rummy' 'sono:sono' 'crisp:crisp' \
    'cribbage:cribbage' 'super_trump:super_trump' 'daifugo_two:daifugo_two' \
    'briscola:briscola' 'bohemian_schneider:bohemian_schneider' 'durak:durak' \
    'officer_skat:officer_skat' 'klaberjass:klaberjass' 'norwegian_whist:norwegian_whist' 'schnapsen:schnapsen' 'goldmine:goldmine' 'knave:knave' \
    'hamlet:hamlet' 'whos_who:whos_who' 'mizerka:mizerka' 'sheriff:sheriff' 'farbwechsel:farbwechsel' 'kaedama_trick:kaedama_trick' \
    'ninety_nine:ninety_nine' 'minimo:minimo' 'trick_of_the_dead:trick_of_the_dead' 'corpo:corpo' \
    'tanuki:tanuki' 'multi_stack:multi_stack' 'dubito:dubito' 'three_tricks:three_tricks' 'mini_misere:mini_misere' \
    'agony_aunt:agony_aunt' 'collusion:collusion' 'confirmation:confirmation' 'big_two:big_two' 'triple_crown:triple_crown' \
    'guillotine:guillotine' 'the_trick:the_trick' \
    'truf:truf' 'pass_cut_run:pass_cut_run' 'finesse:finesse' 'yaniv:yaniv' 'wuxing_xiangke:wuxing_xiangke' \
    'schmear:schmear' 'briscola_chiamata:briscola_chiamata' 'portland:portland' \
    'go_fish:go_fish' 'old_maid:old_maid' 'gops:gops' 'spite_and_malice:spite_and_malice' \
    'golf:golf' 'sevens:sevens' 'concentration:concentration' 'page_one:page_one' 'rummy_500:rummy_500' \
    'euchre:euchre' 'oh_hell:oh_hell' 'baccarat:baccarat' 'black_lady:black_lady' 'four_tricks:four_tricks' \
    'italian_whist:italian_whist' 'gooseberry_fool:gooseberry_fool' 'briscola_bugiarda:briscola_bugiarda' \
    'sasaki_44a:sasaki_44a' 'toepen:toepen' 'war:war' 'blackjack:blackjack' 'crazy_eights:crazy_eights' \
    'cheat:cheat' 'hearts:hearts' 'spades:spades' 'twenty_four:twenty_four' \
    'piquet:piquet' 'five_hundred:five_hundred' 'skat:skat' 'ulti:ulti' 'doppelkopf:doppelkopf' \
    'schafkopf:schafkopf' 'goninkan:goninkan' 'speed:speed' 'casino:casino' 'seven_bridge:seven_bridge' \
    'canasta:canasta' 'pinochle:pinochle' 'texas_holdem:texas_holdem' 'five_card_draw:five_card_draw'; do
    id="${audit%%:*}"
    document="${audit#*:}"
    test -f "docs/rules/$document.md"
    rg -F '| `Verified` | `'"$id"'` |' docs/rules/candidate-rules.md >/dev/null
done
echo "Migration verification passed."
