[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UnityPath,

    [string]$ResultsDirectory = (Join-Path $PSScriptRoot "..\TestResults"),

    [ValidateRange(60, 3600)]
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = "Stop"

$editorPath = (Resolve-Path -LiteralPath $UnityPath).Path
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\Unity\TrumpGameLab")).Path
$resultsPath = [System.IO.Path]::GetFullPath($ResultsDirectory)

function Invoke-ProductTests {
    param([ValidateSet("EditMode", "PlayMode")][string]$Platform)

    $platformName = $Platform.ToLowerInvariant()
    $testResults = Join-Path $resultsPath ("product-" + $platformName + "-results.xml")
    $logFile = Join-Path $resultsPath ("product-" + $platformName + ".log")
    Remove-Item -LiteralPath $testResults -Force -ErrorAction SilentlyContinue

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $editorPath
    $startInfo.WorkingDirectory = $projectPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in @(
        "-batchmode", "-nographics", "-accept-apiupdate",
        "-projectPath", $projectPath,
        "-runTests", "-testPlatform", $Platform,
        "-testResults", $testResults,
        "-logFile", $logFile
    )) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Unity $Platform tests exceeded the $TimeoutSeconds second timeout. Log: $logFile"
        }
        $exitCode = $process.ExitCode
    }
    finally {
        $process.Dispose()
    }

    if (-not (Test-Path -LiteralPath $testResults)) {
        throw "Unity did not produce $Platform results (exit=$exitCode). Log: $logFile"
    }
    [xml]$document = Get-Content -LiteralPath $testResults -Raw
    $testRun = $document.SelectSingleNode("/test-run")
    if ($null -eq $testRun) {
        throw "Unity produced unsupported $Platform results: $testResults"
    }
    $total = [int]$testRun.GetAttribute("total")
    $passed = [int]$testRun.GetAttribute("passed")
    $failed = [int]$testRun.GetAttribute("failed")
    if ($exitCode -ne 0 -or $total -eq 0 -or $failed -ne 0) {
        throw "Unity $Platform tests failed (exit=$exitCode, total=$total, passed=$passed, failed=$failed). Results: $testResults"
    }
    Write-Output "Unity product $Platform tests passed: $passed/$total"
    Write-Output "Results: $testResults"
    Write-Output "Log: $logFile"
}

New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
Invoke-ProductTests -Platform "EditMode"
Invoke-ProductTests -Platform "PlayMode"
