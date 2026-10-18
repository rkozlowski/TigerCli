<#
.SYNOPSIS
Runs the real-keypress completion check in Windows PowerShell 5.1 and, when installed, PowerShell 7.

.DESCRIPTION
Builds CommandParserTest (parser-test), then starts windows-real-tab-check.ps1 once per host in a
hidden console of its own, so the key events it writes reach only that console. Prints the
combined PASS/FAIL lines and keeps each host's results and final screen under -OutputRoot.

Exit codes: 0 every check passed, 1 a check failed, 2 the check could not run.
#>
param(
    [string]$OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) 'tigercli-windows-tab-check')
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$project = Join-Path $repoRoot 'CommandParserTest\CommandParserTest.csproj'
& dotnet build $project -nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error 'Building parser-test failed.'; exit 2 }
$appDir = Join-Path $repoRoot 'CommandParserTest\bin\Debug\net10.0'

New-Item -ItemType Directory -Force $OutputRoot | Out-Null
$hosts = @('powershell.exe') + @(if (Get-Command pwsh.exe -ErrorAction Ignore) { 'pwsh.exe' })
$lines = foreach ($shell in $hosts) {
    $resultPath = Join-Path $OutputRoot "$shell.tsv"
    Remove-Item -LiteralPath $resultPath -ErrorAction Ignore
    $arguments = @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$(Join-Path $PSScriptRoot 'windows-real-tab-check.ps1')`"",
        '-Shell', $shell, '-AppDir', "`"$appDir`"", '-ResultPath', "`"$resultPath`"")
    $process = Start-Process $shell -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(180000)) { $process.Kill(); "FAIL`t$shell`tcheck`ttimed out" ; continue }
    if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Encoding UTF8 }
    else { "FAIL`t$shell`tcheck`tno result written (exit $($process.ExitCode))" }
}
$lines | ForEach-Object { Write-Host $_ }
if (@($lines | Where-Object { $_ -like 'FAIL*' }).Count) { exit 1 }
exit 0
