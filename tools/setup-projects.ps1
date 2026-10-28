param(
  [string]$RepoRoot = ''
)

# Configures the five Unity projects in tasks/ so they open correctly:
#   - custom tags required by the challenge scripts and prefabs
#   - build scenes pointing at the challenge / game scenes
#   - product name and legacy Input Manager as the active input handler
#
# Unity project settings are plain YAML, so they can be prepared without
# launching the editor (which would require an activated license).

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if (-not $RepoRoot) {
  $RepoRoot = Split-Path -Parent $PSScriptRoot
}

function Read-Text([string]$path) {
  return [System.IO.File]::ReadAllText($path)
}

function Write-Text([string]$path, [string]$text) {
  $dir = Split-Path $path -Parent
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
  [System.IO.File]::WriteAllText($path, $text, $utf8NoBom)
}

$projects = @(
  @{ Dir = '01-challenge-2-play-fetch'
     Product = 'Challenge 2 Play Fetch'
     Tags = @('Dog')
     Scenes = @(@{ Path = 'Assets/Challenge 2/Challenge 2.unity'; Guid = 'f695fe0c51d8449d2b49648dad174255' }) }
  @{ Dir = '02-challenge-3-balloons-bombs-booleans'
     Product = 'Challenge 3 Balloons Bombs Booleans'
     Tags = @('Background', 'Bomb', 'Money')
     Scenes = @(@{ Path = 'Assets/Challenge 3/Challenge 3.unity'; Guid = '8a1fabfaac7e04af29dc3664bfd4f100' }) }
  @{ Dir = '03-challenge-4-soccer-scripting'
     Product = 'Challenge 4 Soccer Scripting'
     Tags = @('Enemy', 'Powerup')
     Scenes = @(@{ Path = 'Assets/Challenge 4/Challenge 4.unity'; Guid = 'bafab91dee6434fbcb28a5a16e78982f' }) }
  @{ Dir = '04-challenge-5-whack-a-food'
     Product = 'Challenge 5 Whack a Food'
     Tags = @('Bad', 'Sensor')
     Scenes = @(@{ Path = 'Assets/Challenge 5/Challenge 5.unity'; Guid = '2c85e4f6638394ea8b8154a3b832357b' }) }
  @{ Dir = '05-raining-apocalypse'
     Product = 'Raining Apocalypse'
     Tags = @('Ground')
     Scenes = @(@{ Path = 'Assets/Scenes/MainMenu.unity'; Guid = '164f051645cda4c3daadd30ebcaf8695' }
                @{ Path = 'Assets/Scenes/Game.unity';    Guid = '2cda990e2423bbf4892e6590ba056729' }) }
)

foreach ($p in $projects) {
  $proj = Join-Path $RepoRoot ('tasks\' + $p.Dir)
  Write-Host "=== $($p.Dir) ==="

  # ---- TagManager: add the custom tags the scripts and prefabs rely on ----
  $tagPath = Join-Path $proj 'ProjectSettings\TagManager.asset'
  $tag = Read-Text $tagPath
  $nl = if ($tag -match "`r`n") { "`r`n" } else { "`n" }

  # Unity ships the built-in tags implicitly; the file may hold them either as
  # an inline empty list (`tags: []`) or as an explicit list. Keep whichever
  # already exists and append the tags this project needs.
  # The list block must not run past the tags: the layers list below it uses
  # the same indentation, so the pattern is line based and not greedy.
  $existing = @()
  $listBlock = [regex]::Match($tag, '(?m)^  tags:\r?\n((?:  - [^\r\n]*\r?\n?)*)').Groups[1].Value
  if ($listBlock) {
    $existing = @($listBlock -split "`r?`n" | Where-Object { $_ -match '^\s+- \S' } | ForEach-Object { ($_ -replace '^\s*- ', '').Trim() })
  }
  $allTags = @($existing + $p.Tags | Select-Object -Unique)
  $tagBody = '  tags:' + $nl + (($allTags | ForEach-Object { "  - $_" }) -join $nl)
  # The whole block is replaced, not just the "tags:" line: otherwise the list
  # that was already in the file stays behind and the tags get duplicated.
  $tagPattern = '(?m)^  tags:.*(?:\r?\n  - [^\r\n]*)*'
  $tag = [regex]::Replace($tag, $tagPattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m2) $tagBody })
  Write-Text $tagPath $tag
  Write-Host "  tags: $($allTags -join ', ')"

  # ---- ProjectSettings: product name + legacy Input Manager ----
  $psPath = Join-Path $proj 'ProjectSettings\ProjectSettings.asset'
  $ps = Read-Text $psPath
  $ps = [regex]::Replace($ps, '(?m)^  productName: .*$', "  productName: $($p.Product)")
  $ps = [regex]::Replace($ps, '(?m)^  companyName: .*$', '  companyName: Lingjing')
  if ($ps -notmatch '(?m)^  activeInputHandler:') {
    $ps = [regex]::Replace($ps, '(?m)^  companyName: Lingjing\r?$', "  companyName: Lingjing`r`n  activeInputHandler: 0")
  }
  Write-Text $psPath $ps
  Write-Host "  product: $($p.Product), activeInputHandler: 0"

  # ---- EditorBuildSettings: the scenes that ship in the build ----
  # The repository stores LF line endings, so the file is written with LF as
  # well. Otherwise every run of this script shows up as a change.
  $sceneLines = ($p.Scenes | ForEach-Object {
    "  - enabled: 1`n    path: $($_.Path)`n    guid: $($_.Guid)"
  }) -join "`n"
  $build = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
$sceneLines
  m_configObjects: {}
"@
  Write-Text (Join-Path $proj 'ProjectSettings\EditorBuildSettings.asset') ($build -replace "`r`n", "`n")
  Write-Host "  scenes: $($p.Scenes.Count)"
}
