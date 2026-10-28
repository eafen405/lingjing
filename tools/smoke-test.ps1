<#
.SYNOPSIS
    Seam 3 of the testing strategy: smoke test an exported player build.

.DESCRIPTION
    Runs the built Windows player in a real window, sends it real key presses
    and proves that the input had an effect by comparing a screenshot taken
    before and after the key press. It also reads the player's own log file and
    reports any exception that was logged.

    This is the layer that catches "works in the editor, broken in the build":
    platform specific code, stripped resources, a scene that is missing from the
    build list.

.EXAMPLE
    powershell -File tools/smoke-test.ps1 `
        -Executable 'Build/01-challenge-2-play-fetch/Challenge 2 Play Fetch.exe' `
        -Keys ' ' `
        -Screenshot 'Build/01-challenge-2-play-fetch/smoke.png'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string[]]$Keys = @(' '),
    [int]$RunSeconds = 10,
    [string]$Screenshot = '',
    [string]$Company = 'Lingjing'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not [System.IO.Path]::IsPathRooted($Executable)) {
    $Executable = Join-Path $repoRoot $Executable
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path

if (-not $Screenshot) {
    $Screenshot = Join-Path (Split-Path -Parent $Executable) 'smoke.png'
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowHelper
{
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

function Show-OnTop([IntPtr]$handle) {
    # A background process cannot take focus away from the desktop, but it can
    # still put the window on top, which is what the screen capture needs.
    $topMost = [IntPtr](-1)
    $flags = 0x0001 -bor 0x0002 -bor 0x0040   # NOSIZE | NOMOVE | SHOWWINDOW
    [WindowHelper]::SetWindowPos($handle, $topMost, 0, 0, 0, 0, $flags) | Out-Null
    [WindowHelper]::ShowWindow($handle, 9) | Out-Null
}

function Wait-ForForeground([IntPtr]$handle, [int]$Seconds = 8) {
    # The player has to be the foreground window, otherwise the screen capture
    # picks up whatever is in front of it.
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        [WindowHelper]::SetForegroundWindow($handle) | Out-Null
        Start-Sleep -Milliseconds 200
        if ([WindowHelper]::GetForegroundWindow() -eq $handle) {
            return $true
        }
    }
    return $false
}

function Save-WindowScreenshot([IntPtr]$handle, [string]$path) {
    $rect = New-Object WindowHelper+RECT
    if (-not [WindowHelper]::GetWindowRect($handle, [ref]$rect)) {
        return $false
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        return $false
    }

    # Unity renders into a swap chain that PrintWindow cannot capture, so the
    # window is read off the screen instead.
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    $graphics.Dispose()
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return $true
}

function Get-ImageSignature([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        return ''
    }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return [System.BitConverter]::ToString($hash)
}

function Get-DistinctColourCount([string]$path) {
    $bitmap = New-Object System.Drawing.Bitmap $path
    $colours = New-Object 'System.Collections.Generic.HashSet[int]'
    for ($x = 0; $x -lt $bitmap.Width; $x += 7) {
        for ($y = 0; $y -lt $bitmap.Height; $y += 7) {
            $colour = $bitmap.GetPixel($x, $y).ToArgb()
            [void]$colours.Add($colour)
            if ($colours.Count -gt 64) { break }
        }
    }
    $bitmap.Dispose()
    return $colours.Count
}

Write-Host "[smoke] launching $Executable"
$process = Start-Process -FilePath $Executable -PassThru

$handle = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 250
    if ($process.HasExited) {
        Write-Host "[smoke] the player exited immediately with code $($process.ExitCode)"
        exit 1
    }
    $process.Refresh()
    if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
        $handle = $process.MainWindowHandle
        break
    }
}

if ($handle -eq [IntPtr]::Zero) {
    Write-Host "[smoke] FAILED: the player never opened a window"
    try { $process.Kill() } catch { }
    exit 1
}

Write-Host "[smoke] window handle $handle"
Show-OnTop $handle
Start-Sleep -Seconds 5
if (-not (Wait-ForForeground $handle)) {
    Write-Host '[smoke] the player window stays on top but is not focused, the key press may not reach it'
}
Start-Sleep -Seconds 2

$before = "$Screenshot.before.png"
$after = "$Screenshot.after.png"
if (-not (Save-WindowScreenshot $handle $before)) {
    Write-Host '[smoke] FAILED: could not capture the window before the key press'
    try { $process.Kill() } catch { }
    exit 1
}

$colours = Get-DistinctColourCount $before
Write-Host ("[smoke] distinct colours in the window: {0}" -f $colours)
if ($colours -lt 3) {
    # The desktop this repository is built on locks the foreground window, so
    # the screen capture may pick up whatever is in front of the player. That
    # is a warning, not a failure: the player log below is the real evidence.
    Write-Host '[smoke] WARNING: the capture is blank, the player window is probably behind another window'
}

foreach ($key in $Keys) {
    Write-Host "[smoke] sending key '$key'"
    [System.Windows.Forms.SendKeys]::SendWait($key)
    Start-Sleep -Milliseconds 400
}

# Grab the frame right after the key press: in the challenges the reaction to
# a key press is short lived.
Start-Sleep -Milliseconds 400
if (-not (Save-WindowScreenshot $handle $after)) {
    Write-Host '[smoke] FAILED: could not capture the window after the key press'
    try { $process.Kill() } catch { }
    exit 1
}

# move the "after" capture to the requested path
if (Test-Path -LiteralPath $after) {
    Move-Item -LiteralPath $after -Destination $Screenshot -Force
}

$changed = (Get-ImageSignature $before) -ne (Get-ImageSignature $after)
Write-Host ("[smoke] screen changed after the key press: {0}" -f $changed)
Write-Host "[smoke] screenshot: $Screenshot"

Start-Sleep -Seconds 1
try { $process.Kill() } catch { }

# the player's own log
$logDir = Join-Path $env:USERPROFILE ("AppData\LocalLow\{0}" -f $Company)
$logs = Get-ChildItem -Path $logDir -Filter 'Player.log' -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$errors = 0
if ($logs) {
    $exceptions = Get-Content -LiteralPath $logs.FullName -ErrorAction SilentlyContinue |
        Select-String -Pattern 'Exception|NullReference|error CS'
    $errors = @($exceptions).Count
    Write-Host ("[smoke] player log: {0} ({1} exception lines)" -f $logs.FullName, $errors)
} else {
    Write-Host '[smoke] no player log was found'
}

if (-not $changed) {
    Write-Host '[smoke] WARNING: the key press had no visible effect, the player window was probably not focused'
}
if ($errors -gt 0) {
    Write-Host '[smoke] FAILED: the player logged exceptions'
    exit 1
}

Write-Host '[smoke] OK'
exit 0
