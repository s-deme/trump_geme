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
test "$(dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue --pending | tail -1)" = "合計 26 件"

expected_verified=$'agony_aunt\nbaccarat\nbaohuang\nbig_two\nblack_lady\nbohemian_schneider\nbriscola\nbriscola_chiamata\ncard_capture\ncollusion\nconcentration\nconfirmation\ncorpo\ncribbage\ncrisp\ndaifugo_two\ndubito\ndurak\neuchre\nfarbwechsel\nfinesse\nfour_tricks\ngerman_whist\ngin_rummy\ngo_fish\ngoldmine\ngolf\ngops\ngosankyo\nguillotine\nhamlet\nkaedama_trick\nklaberjass\nknave\nmini_misere\nminimo\nmizerka\nmulti_stack\nnapoleon\nninety_nine\nnorwegian_whist\nofficer_skat\noh_hell\nold_maid\npage_one\npass_cut_run\nportland\nrummy_500\nschmear\nschnapsen\nscoundrel\nsevens\nsheriff\nsono\nspite_and_malice\nsuper_trump\ntanuki\nthe_trick\nthree_tricks\ntrick_of_the_dead\ntriple_crown\ntruf\ntrump_crew\nwhos_who\nwuxing_xiangke\nyaniv'
actual_verified="$(dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue |
    awk '$2 == "Verified" { sub(/^implemented:/, "", $1); print $1 }' | sort)"
test "$actual_verified" = "$expected_verified"

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
    'euchre:euchre' 'oh_hell:oh_hell' 'baccarat:baccarat' 'black_lady:black_lady' 'four_tricks:four_tricks'; do
    id="${audit%%:*}"
    document="${audit#*:}"
    test -f "docs/rules/$document.md"
    rg -F '| `Verified` | `'"$id"'` |' docs/rules/candidate-rules.md >/dev/null
done
echo "Migration verification passed."
