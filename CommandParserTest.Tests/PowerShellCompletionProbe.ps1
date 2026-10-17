# Drives the TigerCli PowerShell completer for parser-test inside a real PowerShell host and writes
# what it observed to -ResultPath as JSON. PowerShellCompletionTests owns the assertions.
#
# The session starts with an OEM console code page and a non-zero $LASTEXITCODE so the probe can
# show that a Tab press decodes the app's UTF-8 answer and leaves the session as it found it.
param(
    [Parameter(Mandatory = $true)][string]$BinDir,
    [Parameter(Mandatory = $true)][string]$ResultPath,
    [int]$CodePage = 852,
    [switch]$Constrained
)

$ErrorActionPreference = 'Stop'
# Constrained Language Mode blocks method calls on most types, so values are read through properties
# and string interpolation only.
$env:PATH = $BinDir + ';' + $env:PATH

# The console's real output code page. chcp is no substitute: it reports the input code page.
Add-Type -Namespace TigerCliProbe -Name Kernel32 -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint GetConsoleOutputCP();'

function Get-ConsoleCodePage {
    if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') { return -1 }
    [int][TigerCliProbe.Kernel32]::GetConsoleOutputCP()
}

function Complete-Line([string]$Line) {
    $completion = TabExpansion2 -inputScript $Line -cursorColumn $Line.Length
    @($completion.CompletionMatches | ForEach-Object {
        @{ Text = $_.CompletionText; Type = "$($_.ResultType)"; ToolTip = $_.ToolTip }
    })
}

function Measure-Tab([string]$Line) {
    cmd /c exit 7
    $errorsBefore = $Error.Count
    $matchList = Complete-Line $Line
    $exitCode = $LASTEXITCODE
    $errorsAdded = $Error.Count - $errorsBefore
    @{ Line = $Line; Matches = $matchList; LastExitCode = $exitCode; ErrorsAdded = $errorsAdded; ConsoleCodePage = Get-ConsoleCodePage }
}

$activation = parser-test --completion pwsh | Out-String
[Console]::OutputEncoding = [System.Text.Encoding]::GetEncoding($CodePage)
if ($Constrained) { $ExecutionContext.SessionState.LanguageMode = 'ConstrainedLanguage' }
Invoke-Expression $activation

$tabs = @(
    Measure-Tab 'parser-test ec'
    Measure-Tab 'parser-test --culture pl-PL --h'
)

# After uninstall the registered completer must stay silent: no candidates, no recorded errors.
$env:PATH = $env:SystemRoot + '\System32'
$uninstalled = Measure-Tab 'parser-test ec'

$result = @{
    Edition = $PSVersionTable.PSEdition
    LanguageMode = "$($ExecutionContext.SessionState.LanguageMode)"
    Tabs = $tabs
    Uninstalled = $uninstalled
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
