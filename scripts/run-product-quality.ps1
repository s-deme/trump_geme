[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UnityPath,

    [ValidateSet("Quick", "Full")]
    [string]$Mode = "Full",

    [string]$ResultsDirectory = (Join-Path $PSScriptRoot "..\TestResults\ProductQuality"),

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$editorPath = (Resolve-Path -LiteralPath $UnityPath).Path
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\Unity\TrumpGameLab")).Path
$resultsPath = [System.IO.Path]::GetFullPath($ResultsDirectory)
$releaseDirectory = Join-Path $resultsPath "WindowsRelease"
$developmentDirectory = Join-Path $resultsPath "WindowsDevelopment"
$releasePlayerPath = Join-Path $releaseDirectory "TrumpGameLab.exe"
$developmentPlayerPath = Join-Path $developmentDirectory "TrumpGameLab.exe"
$releaseBuildLog = Join-Path $resultsPath "quality-release-build.log"
$developmentBuildLog = Join-Path $resultsPath "quality-development-build.log"
$summaryPath = Join-Path $resultsPath "quality-summary.json"
$screenSeconds = if ($Mode -eq "Full") { 60 } else { 2 }
$soakSeconds = if ($Mode -eq "Full") { 3600 } else { 10 }

New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null

function Invoke-Process {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds,
        [long]$ProcessorAffinity = 0
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    foreach ($argument in $Arguments) { [void]$startInfo.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        if ($ProcessorAffinity -gt 0) {
            $process.ProcessorAffinity = [IntPtr]$ProcessorAffinity
        }
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Process timed out after $TimeoutSeconds seconds: $FileName"
        }
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

function Invoke-QualityBuild {
    param(
        [Parameter(Mandatory = $true)][string]$PlayerPath,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][bool]$Development
    )
    Remove-Item -LiteralPath $LogPath -Force -ErrorAction SilentlyContinue
    $buildExit = Invoke-Process -FileName $editorPath -WorkingDirectory $projectPath `
        -TimeoutSeconds 1800 -Arguments @(
            "-batchmode", "-nographics", "-accept-apiupdate",
            "-projectPath", $projectPath,
            "-executeMethod", "TrumpLab.Product.Editor.ProductQualityBuild.BuildCommandLine",
            "-qualityBuildPath", $PlayerPath,
            "-qualityDevelopment", $Development.ToString().ToLowerInvariant(),
            "-logFile", $LogPath,
            "-quit"
        )
    if ($buildExit -ne 0 -or -not (Test-Path -LiteralPath $PlayerPath)) {
        throw "Product quality build failed (exit=$buildExit). Log: $LogPath"
    }
}

if (-not $SkipBuild) {
    Remove-Item -LiteralPath $releaseDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $developmentDirectory -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $developmentDirectory -Force | Out-Null
    Invoke-QualityBuild -PlayerPath $releasePlayerPath -LogPath $releaseBuildLog `
        -Development $false
    Invoke-QualityBuild -PlayerPath $developmentPlayerPath -LogPath $developmentBuildLog `
        -Development $true
}
elseif (-not (Test-Path -LiteralPath $releasePlayerPath) -or
        -not (Test-Path -LiteralPath $developmentPlayerPath)) {
    throw "-SkipBuild requires existing release and development quality Players."
}

function Invoke-Probe {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$PlayerPath,
        [Parameter(Mandatory = $true)][ValidateSet("startup", "full", "allocation")]
        [string]$ProbeMode,
        [switch]$AllowFailed
    )

    $reportPath = Join-Path $resultsPath ($Name + ".json")
    $logPath = Join-Path $resultsPath ($Name + ".log")
    Remove-Item -LiteralPath $reportPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
    $launchTicks = [DateTime]::UtcNow.Ticks.ToString(
        [Globalization.CultureInfo]::InvariantCulture)
    $timeout = if ($ProbeMode -eq "full") {
        $soakSeconds + ($screenSeconds * 5) + 900
    } elseif ($ProbeMode -eq "allocation") {
        $screenSeconds + 180
    } else { 60 }
    $exitCode = Invoke-Process -FileName $PlayerPath `
        -WorkingDirectory (Split-Path -Parent $PlayerPath) `
        -TimeoutSeconds $timeout -ProcessorAffinity 3 -Arguments @(
            "-screen-fullscreen", "0",
            "-screen-width", "1280",
            "-screen-height", "720",
            "-force-d3d11",
            "-logFile", $logPath,
            "-trumplab-quality-probe",
            "-trumplab-quality-report", $reportPath,
            "-trumplab-quality-mode", $ProbeMode,
            "-trumplab-quality-launch-ticks", $launchTicks,
            "-trumplab-quality-screen-seconds", $screenSeconds.ToString(
                [Globalization.CultureInfo]::InvariantCulture),
            "-trumplab-quality-soak-seconds", $soakSeconds.ToString(
                [Globalization.CultureInfo]::InvariantCulture)
        )
    if (-not (Test-Path -LiteralPath $reportPath)) {
        throw "Quality probe produced no report (exit=$exitCode). Log: $logPath"
    }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $AllowFailed -and ($exitCode -ne 0 -or $report.Status -ne "passed")) {
        $failureText = ($report.Failures -join "; ")
        throw "Quality probe $Name failed (exit=$exitCode): $failureText. Log: $logPath"
    }
    return $report
}

function Get-BuildTreeHash {
    param([Parameter(Mandatory = $true)][string]$Directory)

    $root = [System.IO.Path]::GetFullPath($Directory).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $manifest = Get-ChildItem -LiteralPath $root -Recurse -File |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($root.Length + 1).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "{0}`t{1}`t{2}" -f $relative, $_.Length, $hash
        }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($manifest -join "`n"))
    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

$warmup = Invoke-Probe -Name "startup-warmup" -PlayerPath $releasePlayerPath `
    -ProbeMode startup -AllowFailed
$startupRuns = @()
foreach ($index in 1..3) {
    $startupRuns += Invoke-Probe -Name ("startup-" + $index) `
        -PlayerPath $releasePlayerPath -ProbeMode startup
}
$full = Invoke-Probe -Name "quality-full" -PlayerPath $releasePlayerPath -ProbeMode full
$allocation = Invoke-Probe -Name "quality-allocation" `
    -PlayerPath $developmentPlayerPath -ProbeMode allocation

$summary = [ordered]@{
    Status = "passed"
    Mode = $Mode
    CompletedUtc = [DateTime]::UtcNow.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
    UnityPath = $editorPath
    ReleasePlayerPath = $releasePlayerPath
    ReleasePlayerSha256 = (Get-FileHash -LiteralPath $releasePlayerPath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    ReleaseBuildSha256 = Get-BuildTreeHash -Directory $releaseDirectory
    DevelopmentPlayerPath = $developmentPlayerPath
    DevelopmentPlayerSha256 = (Get-FileHash -LiteralPath $developmentPlayerPath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    DevelopmentBuildSha256 = Get-BuildTreeHash -Directory $developmentDirectory
    ScreenSecondsPerScreen = $screenSeconds
    SoakSeconds = $soakSeconds
    WarmupStartupSeconds = $warmup.StartupSeconds
    StartupSeconds = @($startupRuns | ForEach-Object { $_.StartupSeconds })
    Full = $full
    Allocation = $allocation
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding utf8NoBOM

Write-Output ("Product quality probe passed in {0} mode." -f $Mode)
Write-Output ("Startup: {0}" -f (($summary.StartupSeconds | ForEach-Object {
    ([double]$_).ToString("0.000", [Globalization.CultureInfo]::InvariantCulture)
}) -join ", "))
Write-Output ("Automated games: {0}; soak games: {1}; soak actions: {2}" -f `
    $full.AutomatedGames, $full.SoakGames, $full.SoakActions)
Write-Output ("Report: {0}" -f $summaryPath)
