<#
.SYNOPSIS
    Exports a Windows 64 bit player build of one of the Unity projects.

.DESCRIPTION
    Seam 3 of the testing strategy: the deliverable layer. Everything that only
    shows up in an exported player (platform specific code, stripped resources,
    missing scenes in the build list) is caught here.

    Uses Unity's own command line build switch, so no editor script is needed.

.EXAMPLE
    powershell -File tools/build-project.ps1 `
        -ProjectPath 'tasks/01-challenge-2-play-fetch' `
        -OutputPath 'Build/01-challenge-2-play-fetch'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$OutputPath = '',
    [string]$ExecutableName = '',
    [switch]$Graphics,
    [int]$TimeoutSeconds = 2400,
    [string]$UnityEditor = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not [System.IO.Path]::IsPathRooted($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot $ProjectPath
}
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$projectSlug = Split-Path -Leaf $ProjectPath

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot ("Build\{0}" -f $projectSlug)
}
if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repoRoot $OutputPath
}

if (-not $UnityEditor) {
    $UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
}
if (-not (Test-Path -LiteralPath $UnityEditor)) {
    throw "Unity editor not found at $UnityEditor"
}

if (-not (Test-Path -LiteralPath $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

if (-not $ExecutableName) {
    $settings = Join-Path $ProjectPath 'ProjectSettings\ProjectSettings.asset'
    $product = [regex]::Match((Get-Content -LiteralPath $settings -Raw), '(?m)^\s*productName:\s*(.+)$').Groups[1].Value.Trim()
    $ExecutableName = "$product.exe"
}
$exePath = Join-Path $OutputPath $ExecutableName

Write-Host "[build] $projectSlug -> $exePath"

$parts = @('-batchmode')
if (-not $Graphics) { $parts += '-nographics' }
$parts += '-quit'
$parts += '-projectPath'
$parts += ('"{0}"' -f $ProjectPath)
$parts += '-buildTarget'
$parts += 'Win64'
$parts += '-buildWindows64Player'
$parts += ('"{0}"' -f $exePath)
$logPath = Join-Path $env:TEMP ("unity-build-{0}.log" -f $projectSlug)
$parts += '-logFile'
$parts += ('"{0}"' -f $logPath)

$process = Start-Process -FilePath $UnityEditor -ArgumentList $parts -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    try { $process.Kill() } catch { }
    throw "Unity build timed out after $TimeoutSeconds seconds"
}

Write-Host "[build] exit code: $($process.ExitCode)"
Write-Host "[build] log: $logPath"

$compileErrors = Get-Content -LiteralPath $logPath -ErrorAction SilentlyContinue |
    Select-String -Pattern 'error CS\d+' | Select-Object -Unique -First 20
if ($compileErrors) {
    Write-Host '[build] compile errors:'
    $compileErrors | ForEach-Object { Write-Host "  $($_.Line)" }
}

if (-not (Test-Path -LiteralPath $exePath)) {
    Write-Host '[build] FAILED: the player executable was not produced'
    Get-Content -LiteralPath $logPath -Tail 40 | ForEach-Object { Write-Host "  $_" }
    exit 1
}

$sizeMb = [math]::Round((Get-Item -LiteralPath $exePath).Length / 1MB, 1)
Write-Host ("[build] OK: {0} ({1} MB)" -f $exePath, $sizeMb)
exit 0
