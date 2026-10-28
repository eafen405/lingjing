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

.EXAMPLE
    powershell -File tools/package-delivery.ps1 -PackageName '2025级物联网工程张三-AI前端第一阶段'
#>
[CmdletBinding()]
param(
    [string]$PackageName = 'AI前端第一阶段',
    [string]$OutputRoot = 'delivery',
    [switch]$SkipBuild,
    [switch]$SkipVideo,
    [int]$Frames = 240,
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$deliverables = @(
    @{ Dir = '01-challenge-2-play-fetch'
       Scene = 'Assets/Challenge 2/Challenge 2.unity'
       Driver = 'Player|SpawnDog|40'
       Title = '挑战二 Play Fetch'
       Spec = 'challenge-2-play-fetch.md' }
    @{ Dir = '02-challenge-3-balloons-bombs-booleans'
       Scene = 'Assets/Challenge 3/Challenge 3.unity'
       Driver = ''
       Title = '挑战三 气球 炸弹 布尔值'
       Spec = 'challenge-3-balloons-bombs-booleans.md' }
    @{ Dir = '03-challenge-4-soccer-scripting'
       Scene = 'Assets/Challenge 4/Challenge 4.unity'
       Driver = ''
       Title = '挑战四 足球脚本'
       Spec = 'challenge-4-soccer-scripting.md' }
    @{ Dir = '04-challenge-5-whack-a-food'
       Scene = 'Assets/Challenge 5/Challenge 5.unity'
       Driver = ''
       Title = '挑战五 打地鼠'
       Spec = 'challenge-5-whack-a-food.md' }
    @{ Dir = '05-raining-apocalypse'
       Scene = 'Assets/Scenes/Game.unity'
       Driver = ''
       Title = '完整游戏 Raining Apocalypse'
       Spec = 'raining-apocalypse.md' }
)

$packageRoot = Join-Path $repoRoot (Join-Path $OutputRoot $PackageName)
if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

$sourceDir = Join-Path $packageRoot '01-源码'
$buildDir = Join-Path $packageRoot '02-玩家构建'
$docDir = Join-Path $packageRoot '03-说明文档'
$videoDir = Join-Path $packageRoot '04-运行视频'
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
    # The generated folders are not part of the source.
    foreach ($junk in @('Library', 'Temp', 'obj', 'Logs', 'UserSettings')) {
        $path = Join-Path $target $junk
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

# --- 2. documents ------------------------------------------------------------
Write-Host '=== copying the documents ==='
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $docDir -Force
$specDir = Join-Path $docDir '验收清单'
New-Item -ItemType Directory -Path $specDir -Force | Out-Null
Copy-Item -Path (Join-Path $repoRoot 'docs\specs\*.md') -Destination $specDir -Force
if (Test-Path -LiteralPath (Join-Path $repoRoot 'docs\delivery')) {
    Copy-Item -Path (Join-Path $repoRoot 'docs\delivery\*.md') -Destination $docDir -Force
}

# --- 3. builds, smoke tests and videos --------------------------------------
foreach ($item in $deliverables) {
    $projectPath = Join-Path $repoRoot (Join-Path 'tasks' $item.Dir)
    $buildOut = Join-Path $repoRoot (Join-Path 'Build' $item.Dir)
    $exe = Join-Path $buildOut ($item.Title -replace '[\\/:*?"<>|]', '_')

    Write-Host "=== $($item.Dir) ==="

    if (-not $SkipBuild) {
        & (Join-Path $PSScriptRoot 'build-project.ps1') -ProjectPath $projectPath -OutputPath $buildOut -UnityEditor $UnityEditor
        if ($LASTEXITCODE -ne 0) { throw "the build of $($item.Dir) failed" }
    }

    $exeFile = Get-ChildItem -LiteralPath $buildOut -Filter '*.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike 'UnityCrashHandler*' } | Select-Object -First 1
    if (-not $exeFile) { throw "no player executable was produced for $($item.Dir)" }

    & (Join-Path $PSScriptRoot 'smoke-test.ps1') -Executable $exeFile.FullName -Screenshot (Join-Path $buildOut 'smoke.png')
    Write-Host "[package] smoke test exit code: $LASTEXITCODE"

    if (-not $SkipVideo) {
        & (Join-Path $PSScriptRoot 'record-demo.ps1') -ProjectPath $projectPath -Scene $item.Scene `
            -Output (Join-Path $videoDir ("{0}.mp4" -f $item.Dir)) -Driver $item.Driver -Frames $Frames
        if ($LASTEXITCODE -ne 0) { Write-Host "[package] the recording of $($item.Dir) failed" }
    }

    $target = Join-Path $buildDir $item.Dir
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $buildOut '*') -Destination $target -Recurse -Force
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
exit 0
