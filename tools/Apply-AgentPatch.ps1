param(
  [string]$RepoRoot = ".",
  [switch]$AutoCommit = $false,
  [string]$CommitMessage = "feat(agent): apply patch"
)

$ErrorActionPreference = 'Stop'
function Fail($m){ Write-Host "ERROR: $m"; exit 1 }
function Info($m){ Write-Host "INFO: $m" }

# Ensure git
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail "git not found in PATH" }

# Repo root
Push-Location $RepoRoot
$top = (& git rev-parse --show-toplevel 2>$null)
if (-not $top) { Pop-Location; Fail "Not inside a git repo: $RepoRoot" }
Set-Location $top

# Read clipboard
try { $raw = Get-Clipboard -Raw } catch { Fail "Could not read clipboard." }
if ([string]::IsNullOrWhiteSpace($raw)) { Fail "Clipboard is empty." }

# Normalize
$raw = $raw -replace "`r`n","`n"
# strip control-format chars (includes ZERO-WIDTH & BOM if present in the *clipboard*)
$raw = [System.Text.RegularExpressions.Regex]::Replace($raw, '\p{Cf}', '')

# Extract fenced or raw diff
$patchText = $null
$rxFence = [regex]'```(?:patch|diff)?\s*\n([\s\S]*?)\n```'
if ($rxFence.IsMatch($raw)) { $patchText = $rxFence.Match($raw).Groups[1].Value }
if (-not $patchText) {
  $m = [regex]::Match($raw, 'diff --git[\s\S]+')
  if ($m.Success) { $patchText = $m.Value }
}
if (-not $patchText) {
  $m = [regex]::Match($raw, '---\s+[^\n]+\n\+\+\+\s+[^\n]+[\s\S]+')
  if ($m.Success) { $patchText = $m.Value }
}
if (-not $patchText) { Fail "Clipboard does not contain a unified diff. Copy the agent's patch code block." }

# Sanity check & debug
$first = ($patchText -split "`n") | Select-Object -First 3
Info ("First lines of patch:`n" + ($first -join "`n"))
if ($patchText -notmatch '^diff --git') {
  Info "Note: patch does not start with 'diff --git' (may still be OK if it's a pure '---/+++')."
}

# Write *without BOM*
$patchFile = Join-Path $env:TEMP "agent.patch"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($patchFile, $patchText, $utf8NoBom)

# Pre-check before applying
$check = & git apply --check "$patchFile" 2>&1
if ($LASTEXITCODE -ne 0) {
  Write-Host $check
  Fail "git apply --check failed (invalid/empty/context mismatch)."
}

# Apply and stage
Info "Applying patch..."
$apply = & git apply --3way --whitespace=fix --index "$patchFile" 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host $apply; Fail "git apply failed (conflict/context mismatch)." }

# Show files
$staged = & git diff --cached --name-only
if (-not $staged) { Fail "No files staged." }
Info "Patch applied and staged:"; $staged | ForEach-Object { " - $_" }

# Open in VS
$devenv = (Get-Command devenv.exe -ErrorAction SilentlyContinue)
if ($devenv) { & $devenv.Path /Edit @($staged | Select-Object -First 12) | Out-Null }

if ($AutoCommit) { & git commit -m $CommitMessage | Out-Null; if ($LASTEXITCODE -eq 0) { Info "Committed." } }

Pop-Location
Info "Done."
