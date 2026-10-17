# Runs the real parser-test executable inside a real PowerShell host on Windows and writes what it
# observed to -ResultPath as JSON. The tests that start this probe own the assertions.
#
# The probe's console starts with an OEM code page, and every scenario records the console's output
# code page afterwards, so the tests can show that TigerCli renders non-ASCII text correctly without
# changing state the console shares with the shell:
#   Interactive - the app writes straight to the console; the text is read back from the screen.
#   Redirected  - PowerShell captures the app's stdout and decodes it the way it decodes any program.
#   Tabs        - Tab presses through TabExpansion2 against the generated PowerShell completer.
param(
    [Parameter(Mandatory = $true)][string]$BinDir,
    [Parameter(Mandatory = $true)][string]$ResultPath,
    [switch]$Constrained
)

$ErrorActionPreference = 'Stop'
$env:PATH = $BinDir + ';' + $env:PATH

# The test keeps this host's output on a hidden console nobody reads, so failures go to the result file.
trap {
    @{ ProbeError = "$_ $($_.ScriptStackTrace)" } | ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding UTF8
    exit 1
}

# Constrained Language Mode blocks method calls on most types, so everything that needs them runs
# before the probe switches modes, and values are read through properties and interpolation only.
Add-Type -Namespace TigerCliProbe -Name Console -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)] public struct Coord { public short X; public short Y; }
[StructLayout(LayoutKind.Sequential)] public struct ScreenBufferInfo {
    public Coord Size; public Coord Cursor; public short Attributes;
    public short Left, Top, Right, Bottom; public Coord MaximumWindowSize; }

[DllImport("kernel32.dll")] public static extern uint GetConsoleOutputCP();
[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
[DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
[DllImport("kernel32.dll")] static extern bool GetConsoleScreenBufferInfo(IntPtr handle, out ScreenBufferInfo info);
[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
static extern bool ReadConsoleOutputCharacterW(IntPtr handle, [Out] char[] buffer, uint length, Coord start, out uint read);
[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
static extern bool FillConsoleOutputCharacterW(IntPtr handle, char c, uint length, Coord start, out uint written);
[DllImport("kernel32.dll")] static extern bool SetConsoleCursorPosition(IntPtr handle, Coord position);

// The screen buffer itself, whatever this process's own stdout is.
static IntPtr OpenScreen() { return CreateFileW("CONOUT$", 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); }

public static void ClearScreen() {
    IntPtr h = OpenScreen(); ScreenBufferInfo info; GetConsoleScreenBufferInfo(h, out info); uint written;
    FillConsoleOutputCharacterW(h, ' ', (uint)(info.Size.X * info.Size.Y), new Coord(), out written);
    SetConsoleCursorPosition(h, new Coord()); CloseHandle(h);
}

public static string ReadScreen() {
    IntPtr h = OpenScreen(); ScreenBufferInfo info; GetConsoleScreenBufferInfo(h, out info);
    int width = info.Size.X; int rows = info.Cursor.Y + 1; char[] cells = new char[width * rows]; uint read;
    ReadConsoleOutputCharacterW(h, cells, (uint)cells.Length, new Coord(), out read); CloseHandle(h);
    var lines = new System.Collections.Generic.List<string>();
    for (int row = 0; row < rows; row++) lines.Add(new string(cells, row * width, width).TrimEnd());
    return string.Join("\n", lines).Trim();
}
'@

# Non-ASCII literals are built from code points: Windows PowerShell 5.1 reads a BOM-less script as ANSI.
$lodz = -join [char[]](0x141, 0xF3, 0x64, 0x17A)   # Łódź

function Set-ProbeCodePage([int]$CodePage) { [Console]::OutputEncoding = [System.Text.Encoding]::GetEncoding($CodePage) }

function Get-ConsoleCodePage {
    # chcp is no substitute: it reports the input code page.
    if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') { return -1 }
    [int][TigerCliProbe.Console]::GetConsoleOutputCP()
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

# Interactive: code page 437 has no Polish letters, so only text written as Unicode reaches the screen
# intact. The app inherits this probe's standard handles, which the test leaves on the console. (A
# cmd redirection to CONOUT$ is no substitute: that handle is write-only, so it reads as redirected.)
function Invoke-OnConsole([string]$Arguments) {
    [TigerCliProbe.Console]::ClearScreen()
    $start = New-Object System.Diagnostics.ProcessStartInfo (Get-Command parser-test).Source
    $start.Arguments = $Arguments
    $start.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::Start($start)
    $process.WaitForExit()
    @{ ExitCode = $process.ExitCode; Screen = [TigerCliProbe.Console]::ReadScreen(); ConsoleCodePage = Get-ConsoleCodePage }
}

Set-ProbeCodePage 437
$interactive = @{
    ProbeOnConsole = (-not [Console]::IsOutputRedirected) -and (-not [Console]::IsErrorRedirected)
    Output = Invoke-OnConsole '--culture pl-PL'
    Error = Invoke-OnConsole '--culture pl-PL --bogus'
    Help = Invoke-OnConsole '--culture pl-PL --help'
    UnicodeArgument = Invoke-OnConsole "--city $lodz"
}

# Redirected: code page 852 has the Polish letters, so text written in the console's code page decodes
# exactly. PowerShell decodes a program's output with [Console]::OutputEncoding, which matches it.
Set-ProbeCodePage 852
$captured = @(parser-test --culture pl-PL)
$output = @{ ExitCode = $LASTEXITCODE; Text = ($captured -join "`n"); ConsoleCodePage = Get-ConsoleCodePage }
$capturedCity = @(parser-test --city $lodz)
$unicodeArgument = @{ ExitCode = $LASTEXITCODE; Text = ($capturedCity -join "`n"); ConsoleCodePage = Get-ConsoleCodePage }
# Windows PowerShell turns redirected stderr lines into error records, which 'Stop' would make fatal.
$ErrorActionPreference = 'Continue'
$capturedError = @(parser-test --culture pl-PL --bogus 2>&1 | ForEach-Object { "$_" })
$errorOutput = @{ ExitCode = $LASTEXITCODE; Text = ($capturedError -join "`n"); ConsoleCodePage = Get-ConsoleCodePage }
$ErrorActionPreference = 'Stop'
$redirected = @{ Output = $output; Error = $errorOutput; UnicodeArgument = $unicodeArgument }

# A raw completion request as a completer sends it, without the generated script's encoding switch:
# the app must answer in the console code page and leave it alone.
$requestLines = @(parser-test --tigercli-complete pwsh _--culture _pl-PL _--h)
$completionRequest = @{ ExitCode = $LASTEXITCODE; Lines = $requestLines; ConsoleCodePage = Get-ConsoleCodePage }

$activation = parser-test --completion pwsh | Out-String
if ($Constrained) { $ExecutionContext.SessionState.LanguageMode = 'ConstrainedLanguage' }
Invoke-Expression $activation

$tabs = @(
    Measure-Tab 'parser-test ec'
    Measure-Tab 'parser-test --culture pl-PL --h'
    Measure-Tab 'parser-test --city '
    Measure-Tab ('parser-test --city ' + [char]0x141)
)

# After uninstall the registered completer must stay silent: no candidates, no recorded errors.
$env:PATH = $env:SystemRoot + '\System32'
$uninstalled = Measure-Tab 'parser-test ec'

$result = @{
    Edition = $PSVersionTable.PSEdition
    LanguageMode = "$($ExecutionContext.SessionState.LanguageMode)"
    Interactive = $interactive
    Redirected = $redirected
    CompletionRequest = $completionRequest
    Tabs = $tabs
    Uninstalled = $uninstalled
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
