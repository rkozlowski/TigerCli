<#
.SYNOPSIS
Runs the real-shell completion and console-output check for Bash, Zsh, and Fish in WSL.

.DESCRIPTION
Publishes CommandParserTest (parser-test) as a self-contained linux-x64 app under -OutputRoot, then
runs linux-real-shell-check.sh in the given WSL distribution. That script drives each shell in tmux
with real Tab presses and writes results.tsv plus every captured screen to <OutputRoot>\results.
Nothing is written into the repository's build folders.

Exit codes: 0 every check passed, 1 a check failed, 2 the environment could not run the checks.

.PARAMETER FetchMissing
Unpacks zsh, fish, and ICU from the distribution's packages into a private prefix when they are not
installed (no root needed). See linux-real-shell-check.sh.
#>
param(
    [string]$Distribution = 'Ubuntu',
    [string]$OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) 'tigercli-linux-shell-check'),
    [switch]$FetchMissing
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$appDir = Join-Path $OutputRoot 'parser-test'
$resultDir = Join-Path $OutputRoot 'results'

& dotnet publish (Join-Path $repoRoot 'CommandParserTest\CommandParserTest.csproj') `
    -c Release -r linux-x64 --self-contained -o $appDir --artifacts-path (Join-Path $OutputRoot 'artifacts') -nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error 'Publishing parser-test for linux-x64 failed.'; exit 2 }

function ConvertTo-WslPath([string]$Path) {
    (& wsl.exe -d $Distribution -- wslpath -a ($Path -replace '\\', '/')).Trim()
}

$arguments = @()
if ($FetchMissing) { $arguments += '--fetch-missing' }
$arguments += (ConvertTo-WslPath $appDir), (ConvertTo-WslPath $resultDir)
& wsl.exe -d $Distribution -- bash (ConvertTo-WslPath (Join-Path $PSScriptRoot 'linux-real-shell-check.sh')) @arguments
$exitCode = $LASTEXITCODE
Write-Host "Results: $(Join-Path $resultDir 'results.tsv')"
exit $exitCode
