<#
.SYNOPSIS
    Runs the Unity editor head-less and waits for it to finish.

.DESCRIPTION
    Unity.exe is a Windows GUI application, so PowerShell does NOT wait for it
    when it is started with the call operator. This helper starts it with
    Start-Process -Wait so the caller gets the real exit code and the log file
    is complete before the script continues.

    Do not pass -quit for play mode probes: the editor has to stay alive for
    the play mode callback to run. The probe exits the editor itself.

.EXAMPLE
    powershell -File tools/run-unity.ps1 `
        -ProjectPath 'tasks/01-challenge-2-play-fetch' `
        -Method Challenge2Probe.Run `
        -MethodArgs 'Assets/Challenge 2/Challenge 2.unity', "$env:TEMP\probe.txt" `
        -LogPath "$env:TEMP\probe.log"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$Method,
    [string]$MethodArgs = '',
    [string]$LogPath,
    [switch]$Quit,
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

if (-not $UnityEditor) {
    $UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
}
if (-not (Test-Path -LiteralPath $UnityEditor)) {
    throw "Unity editor not found at $UnityEditor"
}

if (-not $LogPath) {
    $LogPath = Join-Path $env:TEMP ("unity-" + $Method + ".log")
}
if (Test-Path -LiteralPath $LogPath) {
    Remove-Item -LiteralPath $LogPath -Force
}

# Build the command line as one string so that paths containing spaces survive.
$parts = @('-batchmode')
if (-not $Graphics) { $parts += '-nographics' }
$parts += '-projectPath'
$parts += ('"{0}"' -f $ProjectPath)
$parts += '-executeMethod'
$parts += $Method
$parts += '-logFile'
$parts += ('"{0}"' -f $LogPath)
if ($Quit) { $parts += '-quit' }
if ($MethodArgs) {
    $parts += '--'
    foreach ($a in ($MethodArgs -split '\|')) {
        if ($a -ne '') { $parts += ('"{0}"' -f $a) }
    }
}

Write-Host "[unity] $Method -> $ProjectPath"
Write-Host "[unity] log: $LogPath"

$process = Start-Process -FilePath $UnityEditor -ArgumentList $parts -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    try { $process.Kill() } catch { }
    throw "Unity timed out after $TimeoutSeconds seconds"
}

$exitCode = $process.ExitCode
Write-Host "[unity] exit code: $exitCode"

if (Test-Path -LiteralPath $LogPath) {
    $content = Get-Content -LiteralPath $LogPath
    $compileErrors = $content | Select-String -Pattern 'error CS\d+' | Select-Object -Unique -First 20
    if ($compileErrors) {
        Write-Host '[unity] compile errors:'
        $compileErrors | ForEach-Object { Write-Host "  $($_.Line)" }
    }
}

exit $exitCode
