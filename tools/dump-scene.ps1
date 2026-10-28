param(
  [Parameter(Mandatory=$true)][string]$ProjectRoot,
  [Parameter(Mandatory=$true)][string]$ScenePath
)

$ErrorActionPreference = 'Continue'
$raw = [System.IO.File]::ReadAllText($ScenePath)

# guid -> script name, from .cs.meta files
$guidMap = @{}
Get-ChildItem $ProjectRoot -Recurse -Filter '*.cs.meta' -File -ErrorAction SilentlyContinue | ForEach-Object {
  $g = (Select-String -Path $_.FullName -Pattern '^guid: (\w+)' | Select-Object -First 1)
  if ($g) { $guidMap[$g.Matches[0].Groups[1].Value] = $_.BaseName }
}

$docs = [regex]::Split($raw, '(?m)^--- !u!') | Where-Object { $_ -ne '' }
$goNames = @{}; $goTags = @{}
$pos = @{}; $rot = @{}
$monos = @{}
$colls = @{}

foreach ($d in $docs) {
  $head = [regex]::Match($d, '^(\d+) &(-?\d+)')
  $type = $head.Groups[1].Value
  $selfId = $head.Groups[2].Value

  if ($type -eq '1') {
    $nm = [regex]::Match($d, '(?m)^  m_Name: (.*)$').Groups[1].Value.Trim()
    $tg = [regex]::Match($d, '(?m)^  m_TagString: (.*)$').Groups[1].Value.Trim()
    $goNames[$selfId] = $nm
    if ($tg -and $tg -ne 'Untagged') { $goTags[$selfId] = $tg }
  }
  elseif ($type -eq '4') {
    $gofid = [regex]::Match($d, '(?m)^  m_GameObject: \{fileID: (-?\d+)\}').Groups[1].Value
    $p = [regex]::Match($d, '(?m)^  m_LocalPosition: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}')
    if ($p.Success) { $pos[$gofid] = "({0}, {1}, {2})" -f $p.Groups[1].Value, $p.Groups[2].Value, $p.Groups[3].Value }
    $r = [regex]::Match($d, '(?m)^  m_LocalRotation: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+), w: ([-\d.eE]+)\}')
    if ($r.Success) {
      $x = [double]$r.Groups[1].Value; $y = [double]$r.Groups[2].Value; $z = [double]$r.Groups[3].Value; $w = [double]$r.Groups[4].Value
      if ([Math]::Abs($w) -gt 1e-9) {
        $ang = 2 * [Math]::Atan2([Math]::Sqrt($x*$x + $y*$y + $z*$z), $w) * 180 / [Math]::PI
        $axis = ''
        if ($x -ne 0) { $axis = "x=$([Math]::Round([Math]::Sin([Math]::Atan2($x,$w)*2)*180/[Math]::PI,1))" }
        elseif ($y -ne 0) { $axis = "y=$([Math]::Round([Math]::Sin([Math]::Atan2($y,$w)*2)*180/[Math]::PI,1))" }
        elseif ($z -ne 0) { $axis = "z=$([Math]::Round([Math]::Sin([Math]::Atan2($z,$w)*2)*180/[Math]::PI,1))" }
        $rot[$gofid] = "rot $axis ($([Math]::Round($ang,1)) deg)"
      }
    }
  }
  elseif ($type -eq '114') {
    $gofid = [regex]::Match($d, '(?m)^  m_GameObject: \{fileID: (-?\d+)\}').Groups[1].Value
    $g = [regex]::Match($d, 'm_Script: \{fileID: \d+, guid: (\w+)').Groups[1].Value
    $nm = if ($guidMap.ContainsKey($g)) { $guidMap[$g] } else { "guid:$g" }
    $fields = [regex]::Matches($d, '(?m)^  (m_[A-Za-z0-9_]+): (.+)$') | ForEach-Object { "$($_.Groups[1].Value)=$($_.Groups[2].Value.Trim())" }
    if (-not $monos.ContainsKey($gofid)) { $monos[$gofid] = @() }
    $monos[$gofid] += ,@($nm, ($fields -join ' | '))
  }
  elseif ($type -eq '65') {
    $gofid = [regex]::Match($d, '(?m)^  m_GameObject: \{fileID: (-?\d+)\}').Groups[1].Value
    $s = [regex]::Match($d, '(?m)^  m_Size: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}').Groups
    $trig = [regex]::Match($d, '(?m)^  m_IsTrigger: (\d+)').Groups[1].Value
    if ($s.Count -ge 4) { $colls[$gofid] = "box size=($($s[1].Value), $($s[2].Value), $($s[3].Value)) trigger=$trig" }
  }
  elseif ($type -eq '135') { # SphereCollider
    $gofid = [regex]::Match($d, '(?m)^  m_GameObject: \{fileID: (-?\d+)\}').Groups[1].Value
    $r = [regex]::Match($d, '(?m)^  m_Radius: ([-\d.eE]+)').Groups[1].Value
    $trig = [regex]::Match($d, '(?m)^  m_IsTrigger: (\d+)').Groups[1].Value
    $colls[$gofid] = "sphere r=$r trigger=$trig"
  }
}

Write-Output "=== $ScenePath ==="
foreach ($fid in $goNames.Keys) {
  $n = $goNames[$fid]
  if ($n -match '^SM_|^GoatRig|_jnt') { continue }
  $line = "  {0,-28} {1,-22}" -f $n, $(if ($pos.ContainsKey($fid)) { $pos[$fid] } else { '' })
  if ($rot.ContainsKey($fid)) { $line += " " + $rot[$fid] }
  if ($goTags.ContainsKey($fid)) { $line += " [" + $goTags[$fid] + "]" }
  if ($colls.ContainsKey($fid)) { $line += " {" + $colls[$fid] + "}" }
  Write-Output $line
  if ($monos.ContainsKey($fid)) {
    foreach ($m in $monos[$fid]) {
      Write-Output ("        > " + $m[0])
      if ($m[1]) { Write-Output ("          " + $m[1]) }
    }
  }
}
