<#
.SYNOPSIS
    Builds, smoke tests, records and packs the five phase-1 deliverables.

.DESCRIPTION
    Runs the whole delivery chain for every project:

      1. export a Windows 64 bit player build
      2. smoke test the exported player
      3. record a run-through video
      4. copy the source, the builds, the documents and the videos into one
         hand-in folder and zip it

    The hand-in folder name follows the naming scheme the group uses:
    "grade + major + name + direction + phase".

    NOTE: this file is deliberately ASCII only. Windows PowerShell 5.1 reads a
    .ps1 file without a byte order mark as ANSI, which mangles non ASCII
    literals and can turn a quoted string into an unterminated one.

.EXAMPLE
    powershell -File tools/package-delivery.ps1 -PackageName 'AI frontend phase 1'
#>
[CmdletBinding()]
param(
    [string]$PackageName = 'AI frontend phase 1',
    [string]$OutputRoot = 'delivery',
    [switch]$SkipBuild,
    [switch]$SkipVideo,
    [switch]$SkipSmoke,
    [int]$Frames = 240,
    [string]$Python = '',
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not $Python) {
    foreach ($candidate in @('D:\anacoda3\python.exe', 'D:\anaconda3\python.exe')) {
        if (Test-Path -LiteralPath $candidate) {
            $Python = $candidate
            break
        }
    }
}
if (-not $Python) {
    $command = Get-Command python -ErrorAction SilentlyContinue
    if ($command) {
        $Python = $command.Source
    }
}
if (-not $Python) {
    throw 'No Python interpreter was found. Pass -Python <path to python.exe>.'
}

$deliverables = @(
    @{ Dir = '01-challenge-2-play-fetch'
       Scene = 'Assets/Challenge 2/Challenge 2.unity'
       Driver = 'Player|SpawnDog|40' }
    @{ Dir = '02-challenge-3-balloons-bombs-booleans'
       Scene = 'Assets/Challenge 3/Challenge 3.unity'
       Driver = 'Player|Float|40' }
    @{ Dir = '03-challenge-4-soccer-scripting'
       Scene = 'Assets/Challenge 4/Challenge 4.unity'
       Driver = 'Player|Dash|40' }
    @{ Dir = '04-challenge-5-whack-a-food'
       Scene = 'Assets/Challenge 5/Challenge 5.unity'
       Driver = 'Game Manager|StartGame|60|3' }
    @{ Dir = '05-raining-apocalypse'
       Scene = 'Assets/Scenes/MainMenu.unity'
       Driver = 'Game Manager|PlayGame|45;Player|SetInputAxis|60|1' }
)

$packageRoot = Join-Path $repoRoot (Join-Path $OutputRoot $PackageName)
if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

$sourceDir = Join-Path $packageRoot '01-source'
$buildDir = Join-Path $packageRoot '02-players'
$docDir = Join-Path $packageRoot '03-documents'
$videoDir = Join-Path $packageRoot '04-videos'
foreach ($dir in @($sourceDir, $buildDir, $docDir, $videoDir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

# --- 1. source ---------------------------------------------------------------
Write-Host '=== copying the source ==='
foreach ($project in (Get-ChildItem -Path (Join-Path $repoRoot 'tasks') -Directory | Sort-Object Name)) {
    $target = Join-Path $sourceDir $project.Name
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
        $from = Join-Path $project.FullName $folder
        if (Test-Path -LiteralPath $from) {
            Copy-Item -LiteralPath $from -Destination $target -Recurse -Force
        }
    }
}

# --- 2. documents ------------------------------------------------------------
Write-Host '=== copying the documents ==='
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $docDir -Force
$specDir = Join-Path $docDir 'acceptance-checklists'
New-Item -ItemType Directory -Path $specDir -Force | Out-Null
Copy-Item -Path (Join-Path $repoRoot 'docs\specs\*.md') -Destination $specDir -Force
if (Test-Path -LiteralPath (Join-Path $repoRoot 'docs\delivery')) {
    Copy-Item -Path (Join-Path $repoRoot 'docs\delivery\*.md') -Destination $docDir -Force
}

# --- 3. builds, smoke tests and videos --------------------------------------
$failed = @()
foreach ($item in $deliverables) {
    $projectPath = Join-Path $repoRoot (Join-Path 'tasks' $item.Dir)
    $buildOut = Join-Path $repoRoot (Join-Path 'Build' $item.Dir)

    Write-Host "=== $($item.Dir) ==="

    if (-not $SkipBuild) {
        & (Join-Path $PSScriptRoot 'build-project.ps1') -ProjectPath $projectPath -OutputPath $buildOut -UnityEditor $UnityEditor
        if ($LASTEXITCODE -ne 0) {
            $failed += "$($item.Dir): the build failed"
            continue
        }
    }

    $exeFile = Get-ChildItem -LiteralPath $buildOut -Filter '*.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike 'UnityCrashHandler*' } | Select-Object -First 1
    if (-not $exeFile) {
        $failed += "$($item.Dir): no player executable was produced"
        continue
    }

    if (-not $SkipSmoke) {
        & (Join-Path $PSScriptRoot 'smoke-test.ps1') -Executable $exeFile.FullName -Screenshot (Join-Path $buildOut 'smoke.png')
        if ($LASTEXITCODE -ne 0) {
            $failed += "$($item.Dir): the smoke test failed"
        }
    }

    if (-not $SkipVideo) {
        & (Join-Path $PSScriptRoot 'record-demo.ps1') -ProjectPath $projectPath -Scene $item.Scene `
            -Output (Join-Path $videoDir ("{0}.mp4" -f $item.Dir)) -Driver $item.Driver -Frames $Frames -Python $Python
        if ($LASTEXITCODE -ne 0) {
            $failed += "$($item.Dir): the recording failed"
        }
    }
    else {
        # The recording may exist from an earlier run.
        $existing = Join-Path $buildOut 'demo.mp4'
        if (Test-Path -LiteralPath $existing) {
            Copy-Item -LiteralPath $existing -Destination (Join-Path $videoDir ("{0}.mp4" -f $item.Dir)) -Force
        }
    }

    $target = Join-Path $buildDir $item.Dir
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $buildOut '*') -Destination $target -Recurse -Force
}

if ($failed.Count -gt 0) {
    Write-Host '=== problems ==='
    $failed | ForEach-Object { Write-Host "  $_" }
}

# --- 4. zip ------------------------------------------------------------------
$zipPath = Join-Path $repoRoot (Join-Path $OutputRoot ("{0}.zip" -f $PackageName))
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Write-Host '=== zipping ==='
Compress-Archive -Path $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
$sizeMb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
Write-Host ("[package] wrote {0} ({1} MB)" -f $zipPath, $sizeMb)
Write-Host ("[package] the folder is {0}" -f $packageRoot)

if ($failed.Count -gt 0) {
    exit 1
}
exit 0
