[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UnityPath,

    [string]$ResultsDirectory = (Join-Path $PSScriptRoot "..\TestResults"),

    [ValidateRange(60, 7200)]
    [int]$TimeoutSeconds = 2400,

    [ValidateSet("Fast", "Standard", "Full")]
    [string]$Mode = "Fast",

    [string]$TestFilter = ""
)

$ErrorActionPreference = "Stop"

$editorPath = (Resolve-Path -LiteralPath $UnityPath).Path
$templatePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\tests\TrumpLab.UnityTests")).Path
$packagePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\Packages\com.trump-game-lab.rules")).Path
$resultsPath = [System.IO.Path]::GetFullPath($ResultsDirectory)
$workingProject = Join-Path $resultsPath ("UnityProject-" + $PID)
$workingProjectName = Split-Path -Leaf $workingProject
$testResults = Join-Path $resultsPath "unity-editmode-results.xml"
$logFile = Join-Path $resultsPath "unity-editmode.log"
$createLogFile = Join-Path $resultsPath "unity-project-create.log"

function Invoke-UnityEditor {
    param(
        [string[]]$Arguments,
        [int]$ProcessTimeoutSeconds
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $editorPath
    $startInfo.WorkingDirectory = $resultsPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        if (-not $process.WaitForExit($ProcessTimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Unity exceeded the $ProcessTimeoutSeconds second process timeout. Log: $logFile"
        }
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
if (Test-Path -LiteralPath $workingProject) {
    throw "Temporary Unity project already exists: $workingProject"
}

try {
    $createExitCode = Invoke-UnityEditor -ProcessTimeoutSeconds $TimeoutSeconds -Arguments @(
        "-batchmode",
        "-nographics",
        "-createProject", $workingProjectName,
        "-quit",
        "-logFile", $createLogFile
    )

    if ($createExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $workingProject "Assets"))) {
        throw "Unity could not create the temporary project (exit=$createExitCode). Log: $createLogFile"
    }

    Copy-Item `
        -LiteralPath (Join-Path $templatePath "Packages\manifest.json") `
        -Destination (Join-Path $workingProject "Packages\manifest.json") `
        -Force
    Copy-Item `
        -LiteralPath $packagePath `
        -Destination (Join-Path $workingProject "Packages\com.trump-game-lab.rules") `
        -Recurse

    $testArguments = @(
        "-batchmode",
        "-nographics",
        "-accept-apiupdate",
        "-projectPath", $workingProjectName,
        "-runTests",
        "-testPlatform", "EditMode",
        "-testResults", $testResults,
        "-logFile", $logFile
    )
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $testArguments += @("-testFilter", $TestFilter)
    }
    if ($Mode -eq "Fast") {
        $testArguments += @("-testCategory", "!BroadSimulation")
    }
    elseif ($Mode -eq "Standard") {
        $testArguments += @("-testCategory", "!Exhaustive")
    }
    $unityExitCode = Invoke-UnityEditor -ProcessTimeoutSeconds $TimeoutSeconds -Arguments $testArguments

    if (-not (Test-Path -LiteralPath $testResults)) {
        throw "Unity did not produce a test result. Exit code: $unityExitCode. Log: $logFile"
    }

    [xml]$document = Get-Content -LiteralPath $testResults -Raw
    $testRun = $document.SelectSingleNode("/test-run")
    if ($null -eq $testRun) {
        throw "Unity produced an unsupported test result format: $testResults"
    }

    $total = [int]$testRun.GetAttribute("total")
    $passed = [int]$testRun.GetAttribute("passed")
    $failed = [int]$testRun.GetAttribute("failed")

    if ($total -eq 0) {
        throw "Unity reported zero Edit Mode tests. Log: $logFile"
    }
    if ($unityExitCode -ne 0 -or $failed -ne 0) {
        throw "Unity Edit Mode tests failed (exit=$unityExitCode, total=$total, passed=$passed, failed=$failed). Results: $testResults"
    }

    Write-Output "Unity Edit Mode tests passed in $Mode mode: $passed/$total"
    Write-Output "Results: $testResults"
    Write-Output "Log: $logFile"
}
finally {
    $expectedPrefix = $resultsPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $resolvedWorkingProject = [System.IO.Path]::GetFullPath($workingProject)
    if ($resolvedWorkingProject.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedWorkingProject) -eq ("UnityProject-" + $PID)) {
        Remove-Item -LiteralPath $resolvedWorkingProject -Recurse -Force -ErrorAction SilentlyContinue
    }
}
