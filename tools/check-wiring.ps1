<#
.SYNOPSIS
    Seam 1 of the testing strategy: static wiring checks.

.DESCRIPTION
    Asserts the things that break a Unity project before a single frame runs:
    missing tags, the wrong input handler, a build scene list that points at the
    wrong scene, a script that no longer contains the code it is supposed to
    contain, or a scene that no longer references the prefab it should.

    The expectations live in a JSON file next to this script, one per project,
    so the same checker serves all five deliverables.

    Example:
      powershell -File tools/check-wiring.ps1 -ProjectRoot 'tasks/01-challenge-2-play-fetch'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$Config = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not [System.IO.Path]::IsPathRooted($ProjectRoot)) {
    $ProjectRoot = Join-Path $repoRoot $ProjectRoot
}
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path

if (-not $Config) {
    $slug = Split-Path -Leaf $ProjectRoot
    $Config = Join-Path $PSScriptRoot ("wiring\{0}.json" -f $slug)
    if (-not (Test-Path -LiteralPath $Config)) {
        $candidate = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'wiring') -Filter '*.json' -ErrorAction SilentlyContinue |
            Where-Object { $_.BaseName -eq $slug }
        if ($candidate) { $Config = $candidate.FullName }
    }
}
if (-not (Test-Path -LiteralPath $Config)) {
    throw "No wiring configuration found for '$ProjectRoot' (looked for $Config)"
}

$spec = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
$project = Split-Path -Leaf $ProjectRoot
$results = New-Object System.Collections.Generic.List[string]
$failed = 0

function Add-Result($ok, $name, $detail) {
    if ($ok) {
        $script:results.Add("PASS $name")
    }
    else {
        $script:failed++
        $script:results.Add("FAIL $name - $detail")
    }
}

function Read-Text($relativePath) {
    $full = Join-Path $ProjectRoot $relativePath
    if (-not (Test-Path -LiteralPath $full)) { return $null }
    return (Get-Content -LiteralPath $full -Raw)
}

# --- tags -------------------------------------------------------------------
$tagManager = Read-Text 'ProjectSettings/TagManager.asset'
foreach ($tag in @($spec.tags)) {
    if (-not $tag) { continue }
    $present = $tagManager -and ($tagManager -match "(?m)^\s*-\s*$([regex]::Escape($tag))\s*$")
    Add-Result $present "tag/$tag" "tag '$tag' is not in ProjectSettings/TagManager.asset"
}

# --- project settings -------------------------------------------------------
$projectSettings = Read-Text 'ProjectSettings/ProjectSettings.asset'
if ($projectSettings) {
    $inputHandler = [regex]::Match($projectSettings, '(?m)^\s*activeInputHandler:\s*(\d+)').Groups[1].Value
    Add-Result ($inputHandler -eq '0') 'input/legacy-input-manager' "activeInputHandler is '$inputHandler', expected 0 (old Input Manager)"

    if ($spec.productName) {
        $product = [regex]::Match($projectSettings, '(?m)^\s*productName:\s*(.+)$').Groups[1].Value.Trim()
        Add-Result ($product -eq $spec.productName) 'settings/product-name' "productName is '$product', expected '$($spec.productName)'"
    }
}
else {
    Add-Result $false 'settings/project-settings' 'ProjectSettings/ProjectSettings.asset is missing'
}

# --- build scenes -----------------------------------------------------------
$buildSettings = Read-Text 'ProjectSettings/EditorBuildSettings.asset'
foreach ($scene in @($spec.scenes)) {
    if (-not $scene) { continue }
    $present = $buildSettings -and ($buildSettings -match [regex]::Escape("path: $scene"))
    Add-Result $present "scene-in-build/$scene" "'$scene' is not in EditorBuildSettings.asset"
}

# --- script contents --------------------------------------------------------
foreach ($check in @($spec.sourceChecks)) {
    if (-not $check) { continue }
    $text = Read-Text $check.file
    if ($null -eq $text) {
        Add-Result $false "source/$($check.file)" "file not found"
        continue
    }
    foreach ($needle in @($check.contains)) {
        if (-not $needle) { continue }
        Add-Result ($text.Contains($needle)) "source/$($check.file) contains '$needle'" "not found in $($check.file)"
    }
    foreach ($needle in @($check.mustNotContain)) {
        if (-not $needle) { continue }
        Add-Result (-not $text.Contains($needle)) "source/$($check.file) excludes '$needle'" "still present in $($check.file)"
    }
}

# --- scene contents ---------------------------------------------------------
foreach ($check in @($spec.sceneChecks)) {
    if (-not $check) { continue }
    $text = Read-Text $check.scene
    if ($null -eq $text) {
        Add-Result $false "scene/$($check.scene)" "scene file not found"
        continue
    }
    foreach ($needle in @($check.contains)) {
        if (-not $needle) { continue }
        Add-Result ($text.Contains($needle)) "scene/$($check.scene) contains '$needle'" "not found in $($check.scene)"
    }
    foreach ($needle in @($check.mustNotContain)) {
        if (-not $needle) { continue }
        Add-Result (-not $text.Contains($needle)) "scene/$($check.scene) excludes '$needle'" "still present in $($check.scene)"
    }
}

Write-Host "=== wiring check: $project ==="
foreach ($result in $results) { Write-Host $result }
Write-Host ("checks: {0}  failed: {1}" -f $results.Count, $failed)

if ($failed -gt 0) { exit 1 }
exit 0
