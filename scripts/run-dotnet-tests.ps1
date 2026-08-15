[CmdletBinding()]
param(
    [ValidateSet("Fast", "Standard", "Full")]
    [string]$Mode = "Fast",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$testProject = Join-Path $PSScriptRoot "..\tests\TrumpLab.Tests"
$arguments = @(
    "test",
    $testProject,
    "--configuration", $Configuration
)

if ($NoBuild) {
    $arguments += "--no-build"
}

switch ($Mode) {
    "Fast" {
        $arguments += @("--filter", "TestCategory!=BroadSimulation")
    }
    "Standard" {
        $arguments += @("--filter", "TestCategory!=Exhaustive")
    }
}

Write-Output "Running .NET tests in $Mode mode."
& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
