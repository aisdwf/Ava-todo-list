# Install versioned hooks into the shared git dir (all worktrees).
$ErrorActionPreference = "Stop"

$repoRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repoRoot) { throw "not inside a git work tree" }

$common = (git rev-parse --git-common-dir).Trim()
if (-not [System.IO.Path]::IsPathRooted($common)) {
    $common = Join-Path $repoRoot $common
}

$src = Join-Path $repoRoot ".githooks\commit-msg"
if (-not (Test-Path -LiteralPath $src)) {
    throw "missing versioned hook: $src"
}

$destDir = Join-Path $common "hooks"
$dest = Join-Path $destDir "commit-msg"
New-Item -ItemType Directory -Force -Path $destDir | Out-Null

$text = [System.IO.File]::ReadAllText($src) -replace "`r`n", "`n" -replace "`r", "`n"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($dest, $text, $utf8NoBom)

Write-Host "Installed commit-msg hook -> $dest"
Write-Host "Source: $src"
