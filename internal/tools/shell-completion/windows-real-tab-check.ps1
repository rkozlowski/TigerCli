<#
.SYNOPSIS
Presses real keys in an interactive Windows PowerShell 5.1 or PowerShell 7 session to check TigerCli
completion and console output.

.DESCRIPTION
Run this script in a console of its own (Invoke-WindowsRealTabCheck.ps1 starts it hidden). It sets an
OEM console code page, starts an interactive -Shell session in the same console with parser-test's
completion activated, and writes key events into the console input buffer: typed text (including
non-ASCII letters), Tab, Ctrl+Space (PSReadLine's MenuComplete, which lists candidates with their
descriptions), Escape, and Enter. PSReadLine reads them as if they were typed.
What the session renders is read back from the screen buffer. Results go to -ResultPath as
tab-separated PASS/FAIL/INFO lines.

PSReadLine itself switches the console to UTF-8 while it edits a line and restores the previous
encoding before a command runs, so the console code page seen at an idle prompt is PSReadLine's.
Each step therefore checks that the idle code page is what it was before the first step, and the
check records the code page a command actually runs under. Strict code-page preservation without
PSReadLine is covered by CommandParserTest.Tests.
#>
param(
    [Parameter(Mandatory = $true)][string]$Shell,
    [Parameter(Mandatory = $true)][string]$AppDir,
    [Parameter(Mandatory = $true)][string]$ResultPath,
    [int]$CodePage = 852
)

$ErrorActionPreference = 'Stop'
Add-Type -Namespace TigerCliCheck -Name Keys -MemberDefinition @'
// CharSet.Unicode: the default (Ansi) would marshal Char as one byte through the ANSI code page.
[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)] public struct InputRecord {
    [FieldOffset(0)] public ushort EventType; [FieldOffset(4)] public int KeyDown; [FieldOffset(8)] public ushort RepeatCount;
    [FieldOffset(10)] public ushort VirtualKey; [FieldOffset(12)] public ushort ScanCode; [FieldOffset(14)] public char Char;
    [FieldOffset(16)] public uint ControlKeys; }
[StructLayout(LayoutKind.Sequential)] public struct Coord { public short X; public short Y; }
[StructLayout(LayoutKind.Sequential)] public struct ScreenBufferInfo {
    public Coord Size; public Coord Cursor; public short Attributes; public short Left, Top, Right, Bottom; public Coord Max; }

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
[DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
[DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool WriteConsoleInputW(IntPtr handle, InputRecord[] records, uint count, out uint written);
[DllImport("kernel32.dll")] static extern bool GetConsoleScreenBufferInfo(IntPtr handle, out ScreenBufferInfo info);
[DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool ReadConsoleOutputCharacterW(IntPtr handle, [Out] char[] buffer, uint length, Coord start, out uint read);
[DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool FillConsoleOutputCharacterW(IntPtr handle, char c, uint length, Coord start, out uint written);
[DllImport("kernel32.dll")] public static extern uint GetConsoleOutputCP();
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScanW(char c);

static IntPtr Open(string name) { return CreateFileW(name, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); }

static InputRecord Key(bool down, ushort vk, char c, uint control) {
    InputRecord r = new InputRecord(); r.EventType = 1; r.KeyDown = down ? 1 : 0; r.RepeatCount = 1;
    r.VirtualKey = vk; r.Char = c; r.ControlKeys = control; return r; }

public static void Press(ushort vk, char c, uint control) {
    IntPtr h = Open("CONIN$"); uint written;
    WriteConsoleInputW(h, new InputRecord[] { Key(true, vk, c, control), Key(false, vk, c, control) }, 2, out written);
    CloseHandle(h); }

public static void Type(string text) {
    foreach (char c in text) {
        short scan = VkKeyScanW(c);
        // Characters the keyboard layout cannot produce arrive as VK_PACKET, as Windows delivers them.
        if (scan == -1) Press(0xE7, c, 0);
        else Press((ushort)(scan & 0xFF), c, (uint)((scan & 0x100) != 0 ? 0x10 : 0)); } }

public static string Screen() {
    IntPtr h = Open("CONOUT$"); ScreenBufferInfo info; GetConsoleScreenBufferInfo(h, out info);
    // Through the bottom of the visible window: completion menus are drawn below the cursor row.
    int width = info.Size.X; int rows = Math.Max(info.Cursor.Y, info.Bottom) + 1; char[] cells = new char[width * rows]; uint read;
    ReadConsoleOutputCharacterW(h, cells, (uint)cells.Length, new Coord(), out read); CloseHandle(h);
    var lines = new System.Collections.Generic.List<string>();
    for (int row = 0; row < rows; row++) lines.Add(new string(cells, row * width, width).TrimEnd());
    return string.Join("\n", lines); }
'@

$k = [TigerCliCheck.Keys]
function Settle { Start-Sleep -Milliseconds 300; $previous = ''; for ($i = 0; $i -lt 40; $i++) { $now = $k::Screen(); if ($now -eq $previous) { return $now }; $previous = $now; Start-Sleep -Milliseconds 250 }; $previous }
function Clear-Line { $k::Press(0x1B, [char]0x1B, 0); $k::Press(0x1B, [char]0x1B, 0); Settle | Out-Null }
$results = [System.Collections.Generic.List[string]]::new()
function Check([string]$Scenario, [string]$Screen, [string[]]$Expected) {
    $missing = @($Expected | Where-Object { -not $Screen.Contains($_) })
    $line = if ($missing.Count) { "FAIL`t$Shell`t$Scenario`tmissing: $($missing -join ', ')" } else { "PASS`t$Shell`t$Scenario`t$($Expected -join ' ')" }
    $Screen | Set-Content -LiteralPath "$ResultPath.$Scenario.txt" -Encoding UTF8
    $cp = [int]$k::GetConsoleOutputCP()
    if ($cp -ne $script:idleCodePage) { $line = "FAIL`t$Shell`t$Scenario`tidle console code page changed from $script:idleCodePage to $cp" }
    $results.Add($line)
}

# Non-ASCII literals are built from code points: Windows PowerShell 5.1 reads a BOM-less script as ANSI.
$L = [string][char]0x141
$lodz = -join [char[]](0x141, 0xF3, 0x64, 0x17A)
$greeting = 'Hello, ' + $lodz + '!'
$polishHelp = 'Wy' + [char]0x15B + 'wietl pomoc'

[Console]::OutputEncoding = [System.Text.Encoding]::GetEncoding($CodePage)
$activate = "`$env:PATH = '$AppDir;' + `$env:PATH; parser-test --completion pwsh | Out-String | Invoke-Expression; " +
    "Set-PSReadLineOption -PredictionSource None -ErrorAction Ignore; function prompt { 'PS> ' }"
$start = New-Object System.Diagnostics.ProcessStartInfo $Shell
$start.Arguments = "-NoLogo -NoProfile -NoExit -Command `"$activate`""
$start.UseShellExecute = $false
$session = [System.Diagnostics.Process]::Start($start)
for ($i = 0; $i -lt 60 -and -not $k::Screen().Contains('PS> '); $i++) { Start-Sleep -Milliseconds 250 }
Settle | Out-Null
$script:idleCodePage = [int]$k::GetConsoleOutputCP()
$results.Add("INFO`t$Shell`tcode-page`tset to $CodePage before the session; $script:idleCodePage at the idle prompt")

$k::Type("parser-test --city $L"); $k::Press(0x09, "`t", 0)
Check 'unicode-prefix-tab' (Settle) @("parser-test --city $lodz")
Clear-Line

$k::Type('parser-test --city '); $k::Press(0x20, [char]0, 0x08)            # Ctrl+Space (MenuComplete) lists every candidate
Check 'unicode-candidates-menu' (Settle) @(('Krak' + [char]0xF3 + 'w'), $lodz, ('Gda' + [char]0x144 + 'sk'))
Clear-Line

$k::Type('parser-test --culture pl-PL --help'); $k::Press(0x20, [char]0, 0x08)   # the menu shows the selected tooltip
Check 'localized-descriptions-menu' (Settle) @('--help-errors', $polishHelp)
Clear-Line

$k::Type("parser-test --city $lodz"); $k::Press(0x0D, "`r", 0)
Check 'terminal-output' (Settle) @($greeting)

# The code page a command runs under: PSReadLine has restored the pre-session encoding by then.
$k::Press(0x0C, [char]0x0C, 0x08); Settle | Out-Null
$k::Type('"running-cp=" + [Console]::OutputEncoding.CodePage'); $k::Press(0x0D, "`r", 0)
$running = [regex]::Match((Settle), '(?m)^running-cp=(\d+)').Groups[1].Value
$results.Add("INFO`t$Shell`tcode-page`tcommands run under $running")

$k::Type('exit'); $k::Press(0x0D, "`r", 0)
if (-not $session.WaitForExit(15000)) { $session.Kill(); $results.Add("FAIL`t$Shell`tsession-exit`tdid not exit") }
$k::Screen() | Set-Content -LiteralPath ($ResultPath + '.screen.txt') -Encoding UTF8
$results | Set-Content -LiteralPath $ResultPath -Encoding UTF8
