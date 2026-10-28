<#
.SYNOPSIS
    Records a run-through video of one of the Unity projects.

.DESCRIPTION
    The desktop this repository is built on locks the foreground window, so the
    exported player cannot be filmed from the outside. Instead the game is
    recorded from inside Unity: the camera is rendered into a RenderTexture and
    the frames are written out, then encoded into an MP4.

    The recorder is copied into the project for the recording and removed again
    afterwards, so the deliverable stays clean.

.EXAMPLE
    powershell -File tools/record-demo.ps1 `
        -ProjectPath 'tasks/01-challenge-2-play-fetch' `
        -Scene 'Assets/Challenge 2/Challenge 2.unity' `
        -Output 'Build/01-challenge-2-play-fetch/demo.mp4' `
        -Driver 'Player|SpawnDog|45'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$Scene,
    [string]$Output = '',
    [string]$Driver = '',
    [int]$Frames = 240,
    [int]$Width = 960,
    [int]$Height = 540,
    [int]$Fps = 30,
    [string]$Python = 'D:\anacoda3\python.exe',
    [string]$UnityEditor = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not [System.IO.Path]::IsPathRooted($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot $ProjectPath
}
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$projectSlug = Split-Path -Leaf $ProjectPath

if (-not $Output) {
    $Output = Join-Path $repoRoot ("Build\{0}\demo.mp4" -f $projectSlug)
}
if (-not [System.IO.Path]::IsPathRooted($Output)) {
    $Output = Join-Path $repoRoot $Output
}

if (-not $UnityEditor) {
    $UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
}

if (-not (Test-Path -LiteralPath $Python)) {
    $found = $null
    foreach ($candidate in @('D:\anacoda3\python.exe', 'D:\anaconda3\python.exe')) {
        if (Test-Path -LiteralPath $candidate) {
            $found = $candidate
            break
        }
    }
    if (-not $found) {
        $command = Get-Command python -ErrorAction SilentlyContinue
        if ($command) { $found = $command.Source }
    }
    if (-not $found) {
        throw "Python was not found at '$Python'. Pass -Python <path to python.exe>."
    }
    $Python = $found
}

$framesDir = Join-Path $env:TEMP ("demo-frames-{0}" -f $projectSlug)
if (Test-Path -LiteralPath $framesDir) {
    Remove-Item -LiteralPath $framesDir -Recurse -Force
}
New-Item -ItemType Directory -Path $framesDir -Force | Out-Null

# --- put the recorder into the project -------------------------------------
$editorDir = Join-Path $ProjectPath 'Assets\Editor'
if (-not (Test-Path -LiteralPath $editorDir)) {
    New-Item -ItemType Directory -Path $editorDir -Force | Out-Null
}
$recorderTarget = Join-Path $editorDir 'ScreenRecorder.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ScreenRecorder.cs') -Destination $recorderTarget -Force

try {
    $logPath = Join-Path $env:TEMP ("unity-demo-{0}.log" -f $projectSlug)
    $parts = @('-batchmode', '-projectPath', ('"{0}"' -f $ProjectPath),
               '-executeMethod', 'ScreenRecorder.Run', '-logFile', ('"{0}"' -f $logPath),
               '--', ('"{0}"' -f $Scene), ('"{0}"' -f $framesDir), $Frames, $Width, $Height, ('"{0}"' -f $Driver))

    Write-Host "[demo] recording $projectSlug"
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $parts -PassThru
    if (-not $process.WaitForExit(1800000)) {
        try { $process.Kill() } catch { }
        throw 'The recording timed out'
    }
    Write-Host "[demo] editor exit code: $($process.ExitCode)"

    $compileErrors = Get-Content -LiteralPath $logPath -ErrorAction SilentlyContinue |
        Select-String -Pattern 'error CS\d+' | Select-Object -Unique -First 10
    if ($compileErrors) {
        Write-Host '[demo] compile errors:'
        $compileErrors | ForEach-Object { Write-Host "  $($_.Line)" }
        throw 'The recorder did not compile'
    }

    $frameCount = @(Get-ChildItem -LiteralPath $framesDir -Filter '*.png').Count
    Write-Host "[demo] captured $frameCount frames"
    if ($frameCount -lt 10) {
        throw "Only $frameCount frames were captured"
    }

    # --- encode ------------------------------------------------------------
    $outputDir = Split-Path -Parent $Output
    if (-not (Test-Path -LiteralPath $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    & $Python (Join-Path $PSScriptRoot 'frames-to-video.py') $framesDir $Output --fps $Fps
    if ($LASTEXITCODE -ne 0) {
        throw 'The video encoding failed'
    }
}
finally {
    Remove-Item -LiteralPath $recorderTarget -Force -ErrorAction SilentlyContinue
    $meta = "$recorderTarget.meta"
    Remove-Item -LiteralPath $meta -Force -ErrorAction SilentlyContinue
}

Write-Host "[demo] wrote $Output"
exit 0
