# Run commit-msg fixture tests (locates Git Bash on Windows).
$ErrorActionPreference = "Stop"

function Find-GitBash {
    $candidates = New-Object System.Collections.Generic.List[string]
    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if ($gitCmd) {
        $gitRoot = Split-Path (Split-Path $gitCmd.Source)
        $candidates.Add((Join-Path $gitRoot "bin\bash.exe"))
        $candidates.Add((Join-Path $gitRoot "usr\bin\bash.exe"))
    }
    $candidates.Add("${env:ProgramFiles}\Git\bin\bash.exe")
    $candidates.Add("${env:ProgramFiles}\Git\usr\bin\bash.exe")
    $candidates.Add("C:\Envir\Git\bin\bash.exe")
    foreach ($p in $candidates) {
        if ($p -and (Test-Path -LiteralPath $p)) { return $p }
    }
    throw "Git Bash not found. Install Git for Windows or run: sh scripts/commit-msg-hook/test-commit-msg.sh"
}

$repoRoot = (git rev-parse --show-toplevel).Trim()
$script = Join-Path $repoRoot "scripts/commit-msg-hook/test-commit-msg.sh"
$bash = Find-GitBash
& $bash $script
exit $LASTEXITCODE
