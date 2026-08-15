$ErrorActionPreference = "Stop"

$repoDir = Split-Path -Parent $PSScriptRoot
Set-Location $repoDir

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

& {
    $script:legacyMatches = rg -n -i "python|pytest|unittest|trump_lab|\.py\b" `
        README.md AGENTS.md docs Packages tools tests
}
if ($LASTEXITCODE -eq 0) {
    Write-Error "Legacy Python reference detected.`n$legacyMatches"
}
if ($LASTEXITCODE -gt 1) {
    throw "Legacy reference scan failed."
}

& {
    $script:unityEngineMatches = rg -n "\bUnityEngine\b" Packages/com.trump-game-lab.rules/Runtime
}
if ($LASTEXITCODE -eq 0) {
    Write-Error "Runtime must remain independent of UnityEngine.`n$unityEngineMatches"
}
if ($LASTEXITCODE -gt 1) {
    throw "UnityEngine scan failed."
}

Invoke-Step {
    $script:catalogueTail = dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue | Select-Object -Last 1
} "dotnet run catalogue failed."
if ($catalogueTail -ne "合計 92 件") {
    Write-Error "Unexpected catalogue total: $catalogueTail"
}

Invoke-Step {
    $script:pendingCatalogueTail = dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue --pending |
        Select-Object -Last 1
} "dotnet run catalogue --pending failed."
if ($pendingCatalogueTail -ne "合計 0 件") {
    Write-Error "Unexpected pending catalogue total: $pendingCatalogueTail"
}

$expectedVerified = @(
    "baohuang", "bohemian_schneider", "briscola", "card_capture", "cribbage", "crisp", "daifugo_two",
    "corpo", "durak", "farbwechsel", "german_whist", "gin_rummy", "goldmine", "gosankyo", "hamlet", "kaedama_trick", "klaberjass", "knave", "minimo", "mizerka", "napoleon", "ninety_nine", "norwegian_whist",
    "officer_skat", "scoundrel", "schnapsen", "sheriff", "sono", "super_trump", "tanuki", "three_tricks", "trick_of_the_dead", "trump_crew", "whos_who",
    "dubito", "mini_misere", "multi_stack", "agony_aunt", "collusion", "confirmation", "big_two", "triple_crown", "guillotine", "the_trick",
    "truf", "pass_cut_run", "finesse", "yaniv", "wuxing_xiangke", "schmear", "briscola_chiamata", "portland", "go_fish", "old_maid", "gops", "spite_and_malice",
    "golf", "sevens", "concentration", "page_one", "rummy_500", "euchre", "oh_hell",
    "baccarat", "black_lady", "four_tricks",
    "italian_whist", "gooseberry_fool", "briscola_bugiarda",
    "sasaki_44a", "toepen", "war", "blackjack", "crazy_eights", "cheat", "hearts", "spades", "twenty_four",
    "piquet", "five_hundred", "skat", "ulti", "doppelkopf", "schafkopf", "goninkan", "speed", "casino",
    "seven_bridge", "canasta", "pinochle", "texas_holdem", "five_card_draw"
)
$catalogueRows = dotnet run --project tools/TrumpLab.Cli --no-build -- catalogue
if ($LASTEXITCODE -ne 0) {
    throw "dotnet run catalogue for audit verification failed."
}
$actualVerified = @($catalogueRows | ForEach-Object {
    if ($_ -match '^implemented:([^\s]+)\s+Verified\s+') { $matches[1] }
} | Sort-Object)
if (Compare-Object -ReferenceObject $expectedVerified -DifferenceObject $actualVerified) {
    Write-Error "Unexpected Verified IDs: $($actualVerified -join ', ')"
}

$verifiedDocuments = @{
    trump_crew = "trump-crew"; baohuang = "baohuang"; napoleon = "napoleon";
    card_capture = "card_capture"; scoundrel = "scoundrel"; gosankyo = "gosankyo";
    german_whist = "german_whist"; gin_rummy = "gin_rummy"; sono = "sono"; crisp = "crisp";
    cribbage = "cribbage"; super_trump = "super_trump"; daifugo_two = "daifugo_two";
    briscola = "briscola"; bohemian_schneider = "bohemian_schneider"; durak = "durak";
    officer_skat = "officer_skat"; klaberjass = "klaberjass"; knave = "knave"; norwegian_whist = "norwegian_whist"; schnapsen = "schnapsen"; goldmine = "goldmine";
    hamlet = "hamlet"; whos_who = "whos_who"; mizerka = "mizerka"; sheriff = "sheriff"; farbwechsel = "farbwechsel"; kaedama_trick = "kaedama_trick";
    ninety_nine = "ninety_nine"; minimo = "minimo"; trick_of_the_dead = "trick_of_the_dead"; corpo = "corpo";
    tanuki = "tanuki"; multi_stack = "multi_stack"; dubito = "dubito"; three_tricks = "three_tricks"; mini_misere = "mini_misere";
    agony_aunt = "agony_aunt"; collusion = "collusion"; confirmation = "confirmation"; big_two = "big_two"; triple_crown = "triple_crown";
    guillotine = "guillotine"; the_trick = "the_trick";
    truf = "truf"; pass_cut_run = "pass_cut_run"; finesse = "finesse"; yaniv = "yaniv"; wuxing_xiangke = "wuxing_xiangke";
    schmear = "schmear"; briscola_chiamata = "briscola_chiamata"; portland = "portland";
    go_fish = "go_fish"; old_maid = "old_maid"; gops = "gops"; spite_and_malice = "spite_and_malice";
    golf = "golf"; sevens = "sevens"; concentration = "concentration"; page_one = "page_one"; rummy_500 = "rummy_500";
    euchre = "euchre"; oh_hell = "oh_hell"; baccarat = "baccarat"; black_lady = "black_lady"; four_tricks = "four_tricks";
    italian_whist = "italian_whist"; gooseberry_fool = "gooseberry_fool"; briscola_bugiarda = "briscola_bugiarda";
    sasaki_44a = "sasaki_44a"; toepen = "toepen"; war = "war"; blackjack = "blackjack"; crazy_eights = "crazy_eights";
    cheat = "cheat"; hearts = "hearts"; spades = "spades"; twenty_four = "twenty_four";
    piquet = "piquet"; five_hundred = "five_hundred"; skat = "skat"; ulti = "ulti"; doppelkopf = "doppelkopf";
    schafkopf = "schafkopf"; goninkan = "goninkan"; speed = "speed"; casino = "casino"; seven_bridge = "seven_bridge";
    canasta = "canasta"; pinochle = "pinochle"; texas_holdem = "texas_holdem"; five_card_draw = "five_card_draw"
}
foreach ($id in $expectedVerified) {
    $document = Join-Path $repoDir ("docs/rules/" + $verifiedDocuments[$id] + ".md")
    if (-not (Test-Path -LiteralPath $document)) {
        Write-Error "Missing Verified audit document: $document"
    }
    $record = "| ``Verified`` | ``$id`` |"
    if (-not (Select-String -LiteralPath "docs/rules/candidate-rules.md" -SimpleMatch $record -Quiet)) {
        Write-Error "Verified audit record is missing for $id."
    }
}

Write-Output "Migration verification passed."
