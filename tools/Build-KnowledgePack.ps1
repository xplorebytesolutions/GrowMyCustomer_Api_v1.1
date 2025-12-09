param(
  [string]$RepoRoot = ".",
  [string]$OutDir   = ".\docs\pack",
  [string]$IndexOut = ".\docs\pack\index.json",
  [string]$TinyRoot = ".\docs\xbc-knowledge-pack.json",

  # options
  [switch]$IncludeSecrets = $false,
  [int]$MaxFileKB = 256,   # truncate individual big files
  [int]$MaxPartKB = 550    # target size per part (~0.55 MB) to satisfy Actions limits
)

function Get-FileHashHex($p){ (Get-FileHash -Algorithm SHA256 -Path $p).Hash.ToLower() }
function Slug($s){ ($s -replace '[^a-zA-Z0-9]+','-').ToLower() }
function Lang($e){
  switch ($e.ToLower()) {
    ".cs"{"csharp"}; ".ts"{"typescript"}; ".tsx"{"tsx"}; ".jsx"{"jsx"}
    ".json"{"json"}; ".sql"{"sql"}; ".md"{"markdown"}; ".css"{"css"}; ".scss"{"scss"}
    ".razor"{"razor"}; ".cshtml"{"cshtml"}; ".xml"{"xml"}; ".config"{"xml"}
    ".ps1"{"powershell"}; ".sh"{"bash"}; ".bat"{"bat"}; ".yml"{"yaml"}; ".yaml"{"yaml"}
    default{"text"}
  }
}

$includeExt = @(
  ".cs",".csproj",".sln",".props",".targets",".editorconfig",
  ".json",".yml",".yaml",".md",".sql",".ts",".tsx",".jsx",
  ".css",".scss",".razor",".cshtml",".http",".xml",".config",
  ".ps1",".sh",".bat",".dockerfile",".gitattributes",".gitignore"
)
$excludeDir  = '(?i)(/|\\)(\.git|bin|obj|node_modules|\.vs|logs|docs/pack)(/|\\)'
$excludeFile = '(?i)\.key$|\.pem$|\.pfx$|\.cer$|\.env|secrets'
if (-not $IncludeSecrets) { $excludeFile += '|appsettings\.Production\.json' }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $OutDir ".nojekyll") | Out-Null

$generatedAt = Get-Date -Format 'yyyy-MM-dd HH:mm:ss K'
$index = [ordered]@{ version="2.3-sliced"; generatedAt=$generatedAt; modules=@() }

# -------- Build fine-grained module roots --------
$modules = @()

# 1) Every top-level project as a coarse module (except infra)
$top = Get-ChildItem -Path $RepoRoot -Directory |
  Where-Object { $_.Name -notin @(".git",".github","docs","tools",".vs","obj","bin") }
foreach ($d in $top) {
  $modules += [ordered]@{ name = $d.Name; root = $d.FullName }
}

# 2) Inside xbytechat-api, make *each folder* its own module
$apiRoot = Join-Path $RepoRoot "xbytechat-api"
if (Test-Path $apiRoot) {
  $apiSub = Get-ChildItem -Path $apiRoot -Directory |
    Where-Object { $_.Name -notin @("bin","obj",".vs","logs") }
  foreach ($d in $apiSub) {
    $modules += [ordered]@{
      name = "xbytechat-api/$($d.Name)"
      root = $d.FullName
    }
  }

  # 3) Inside xbytechat-api/Features, each feature becomes a module
  $featRoot = Join-Path $apiRoot "Features"
  if (Test-Path $featRoot) {
    $features = Get-ChildItem -Path $featRoot -Directory
    foreach ($f in $features) {
      $modules += [ordered]@{
        name = "xbytechat-api/Features/$($f.Name)"
        root = $f.FullName
      }
    }
  }
}

$MaxBytes   = $MaxFileKB * 1024
$TargetPart = $MaxPartKB * 1024

foreach ($m in $modules) {
  $name = $m.name; $slug = Slug $name; $root = $m.root
  if (-not (Test-Path $root)) { continue }

  $files = Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $includeExt -contains $_.Extension -or $_.Name -ieq "Dockerfile" } |
    Where-Object { $_.FullName -notmatch $excludeDir } |
    Where-Object { $_.FullName -notmatch $excludeFile } |
    Sort-Object FullName -Unique

  $prepared = @(); $totalBytes = 0
  foreach ($f in $files) {
    $rel = $f.FullName.Replace((Resolve-Path $RepoRoot),"").TrimStart("\","/")
    $text = $null; try { $text = Get-Content -LiteralPath $f.FullName -Raw -ErrorAction Stop } catch { continue }
    if ($null -eq $text) { $text = "" }
    $size = [Text.Encoding]::UTF8.GetByteCount([string]$text)
    if ($size -gt $MaxBytes) {
      $text = $text.Substring(0,[Math]::Min($text.Length,$MaxBytes)) + "`n/* TRUNCATED for size */"
      $size = [Text.Encoding]::UTF8.GetByteCount($text)
    }
    $prepared += [ordered]@{
      path=$rel; sha256=(Get-FileHashHex $f.FullName); language=(Lang $f.Extension); size=$size; content=$text
    }
    $totalBytes += $size
  }

  if ($prepared.Count -eq 0) { continue }

  if ($totalBytes -le $TargetPart) {
    $mod = [ordered]@{ name=$name; generatedAt=$generatedAt; files=$prepared }
    ($mod | ConvertTo-Json -Depth 14) | Out-File -Encoding utf8 (Join-Path $OutDir "$slug.json")
    $index.modules += [ordered]@{
      name=$name; slug=$slug; mode="single"; fileCount=$prepared.Count; bytes=$totalBytes;
      href="/busiorbit-api/pack/$slug.json"
    }
    continue
  }

  # split into small parts
  $partsDir = Join-Path $OutDir "$slug/parts"
  New-Item -ItemType Directory -Force -Path $partsDir | Out-Null
  New-Item -ItemType File -Force -Path (Join-Path $partsDir ".nojekyll") | Out-Null

  $buf = New-Object System.Collections.Generic.List[object]
  $bufBytes = 0; $partNum = 1; $partsMeta = @()

  foreach ($pf in $prepared) {
    $thisSize = [int]$pf.size
    if ($bufBytes -gt 0 -and ($bufBytes + $thisSize) -gt $TargetPart) {
      $out = [ordered]@{ name=$name; part=$partNum; of=0; generatedAt=$generatedAt; files=$buf }
      ($out | ConvertTo-Json -Depth 14) | Out-File -Encoding utf8 (Join-Path $partsDir "$partNum.json")
      $partsMeta += [ordered]@{ n=$partNum; href="/busiorbit-api/pack/$slug/parts/$partNum.json"; bytes=$bufBytes; fileCount=$buf.Count }
      $partNum += 1; $buf = New-Object System.Collections.Generic.List[object]; $bufBytes = 0
    }
    $buf.Add($pf); $bufBytes += $thisSize
  }
  if ($buf.Count -gt 0) {
    $out = [ordered]@{ name=$name; part=$partNum; of=0; generatedAt=$generatedAt; files=$buf }
    ($out | ConvertTo-Json -Depth 14) | Out-File -Encoding utf8 (Join-Path $partsDir "$partNum.json")
    $partsMeta += [ordered]@{ n=$partNum; href="/busiorbit-api/pack/$slug/parts/$partNum.json"; bytes=$bufBytes; fileCount=$buf.Count }
  }

  # set total parts
  $totalParts = $partsMeta.Count
  for ($i=1; $i -le $totalParts; $i++) {
    $p = Get-Content (Join-Path $partsDir "$i.json") -Raw | ConvertFrom-Json
    $p.of = $totalParts
    ($p | ConvertTo-Json -Depth 14) | Out-File -Encoding utf8 (Join-Path $partsDir "$i.json")
  }

  $index.modules += [ordered]@{
    name=$name; slug=$slug; mode="parts"; totalBytes=$totalBytes; partCount=$totalParts; parts=$partsMeta
  }
}

# write index + tiny pointer
($index | ConvertTo-Json -Depth 8) | Out-File -Encoding utf8 $IndexOut
($index | Select-Object version, generatedAt | ConvertTo-Json) | Out-File -Encoding utf8 $TinyRoot
Write-Host "DONE. modules=$($index.modules.Count)"
