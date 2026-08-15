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
if ($pendingCatalogueTail -ne "合計 75 件") {
    Write-Error "Unexpected pending catalogue total: $pendingCatalogueTail"
}

$expectedVerified = @(
    "baohuang", "bohemian_schneider", "briscola", "card_capture", "cribbage", "crisp", "daifugo_two",
    "durak", "german_whist", "gin_rummy", "gosankyo", "napoleon", "officer_skat", "scoundrel",
    "sono", "super_trump", "trump_crew"
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
    officer_skat = "officer_skat"
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
