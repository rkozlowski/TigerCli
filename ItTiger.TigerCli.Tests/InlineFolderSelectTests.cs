using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineFolderSelectTests
{
    // ----- In-memory browser for deterministic, cross-platform navigation tests -----

    private sealed class FakeFolderBrowser : IFolderBrowser
    {
        private readonly bool _windows;
        private readonly Dictionary<string, List<string>> _tree;
        private readonly List<string> _drives;
        private readonly Dictionary<string, string?> _parentOf = new(StringComparer.OrdinalIgnoreCase);

        public FakeFolderBrowser(bool windows, Dictionary<string, List<string>> tree, List<string>? drives = null)
        {
            _windows = windows;
            _tree = new Dictionary<string, List<string>>(tree, StringComparer.OrdinalIgnoreCase);
            _drives = drives ?? new List<string>();

            foreach (var (location, children) in _tree)
                foreach (var child in children)
                    _parentOf[child] = location;

            foreach (var drive in _drives)
                _parentOf.TryAdd(drive, null);
        }

        public string? RootLocation => _windows ? null : "/";

        public IReadOnlyList<FolderEntry> GetEntries(string? location)
        {
            IEnumerable<string> childPaths =
                location is null
                    ? (_windows ? _drives : ChildrenOf("/"))
                    : ChildrenOf(location);

            return childPaths
                .Select(p => new FolderEntry(LabelOf(p, location), p, HasChildren(p)))
                .ToList();
        }

        public bool TryGetParent(string? location, out string? parent)
        {
            parent = null;
            if (location is null)
                return false;

            if (_parentOf.TryGetValue(location, out var p))
            {
                if (p is null)
                {
                    if (_windows) { parent = null; return true; } // drive root → drive list
                    return false;
                }

                parent = p;
                return true;
            }

            return false; // e.g. Unix "/"
        }

        public (string? location, string? highlightPath) ResolveInitial(string? initialPath)
        {
            if (string.IsNullOrWhiteSpace(initialPath))
                return (RootLocation, null);

            string? target = null;
            var probe = initialPath;
            while (probe is not null)
            {
                if (IsKnown(probe)) { target = probe; break; }
                probe = StringParent(probe);
            }

            if (target is null)
                return (RootLocation, null);

            if (TryGetParent(target, out var parent))
                return (parent, target);

            return (target, null);
        }

        private List<string> ChildrenOf(string location) =>
            _tree.TryGetValue(location, out var c) ? c : new List<string>();

        private bool HasChildren(string path) => _tree.TryGetValue(path, out var c) && c.Count > 0;

        private bool IsKnown(string path) => _tree.ContainsKey(path) || _parentOf.ContainsKey(path);

        private string LabelOf(string path, string? location)
        {
            if (location is null && _windows)
                return path; // drive label, e.g. "C:\"

            var sep = _windows ? '\\' : '/';
            var trimmed = path.TrimEnd(sep);
            int idx = trimmed.LastIndexOf(sep);
            return idx < 0 ? trimmed : trimmed[(idx + 1)..];
        }

        private string? StringParent(string path)
        {
            var sep = _windows ? '\\' : '/';
            var trimmed = path.TrimEnd(sep);
            int idx = trimmed.LastIndexOf(sep);
            if (idx < 0) return null;
            if (_windows && idx == 2) return trimmed[..3]; // "C:\"
            if (!_windows && idx == 0) return "/";
            return trimmed[..idx];
        }
    }

    private static FakeFolderBrowser WindowsTree() => new(
        windows: true,
        tree: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\"] = new() { @"C:\Users", @"C:\Windows" },
            [@"C:\Users"] = new() { @"C:\Users\Public" },
            [@"C:\Users\Public"] = new(),
            [@"C:\Windows"] = new(),
            [@"D:\"] = new() { @"D:\Media" },
            [@"D:\Media"] = new() { @"D:\Media\Movies", @"D:\Media\Music" },
            [@"D:\Media\Movies"] = new(),
            [@"D:\Media\Music"] = new() { @"D:\Media\Music\Jazz" },
            [@"D:\Media\Music\Jazz"] = new(),
        },
        drives: new List<string> { @"C:\", @"D:\" });

    private static FakeFolderBrowser UnixTree() => new(
        windows: false,
        tree: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/"] = new() { "/etc", "/home" },
            ["/etc"] = new(),
            ["/home"] = new() { "/home/user" },
            ["/home/user"] = new(),
        });

    // ----- Initial path / preselection -----

    [Fact]
    public void InitialPath_StartsInParentAndHighlightsTarget()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");

        Assert.Equal(@"D:\Media\Movies", control.Payload);
        // The path input scrolls horizontally within its assigned span; the highlighted target
        // folder is shown in the in-frame list, and the path input shows the start of the path.
        var text = Render(control);
        Assert.Contains("Movies", text);
        Assert.Contains(@"D:\Media", text);
    }

    [Fact]
    public void NullInitialPath_FallsBackToRoot()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree());

        // Windows root is the drive list; first drive is highlighted.
        Assert.Equal(@"C:\", control.Payload);
        Assert.Contains(@"C:\", Render(control));
    }

    // ----- Highlighted row is the selected value -----

    [Fact]
    public void Highlight_BecomesSelectedValue()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");

        Assert.Equal(@"D:\Media\Movies", control.Payload);
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.DownArrow, ConsoleModifiers.None)).IsHandled);
        Assert.Equal(@"D:\Media\Music", control.Payload);
    }

    // ----- Enter opens from the folder list; OK confirms -----

    [Fact]
    public async Task OkButton_ConfirmsHighlightedFolder()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Tab);   // list -> buttons
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.SelectFolderAsync(
            shell, "Pick folder", @"D:\Media\Music", WindowsTree(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(@"D:\Media\Music", result);
    }

    [Fact]
    public void Enter_OnFolderList_OpensFolder_WithoutOkResult()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Music");
        var dialog = new InlineDialog(shell, "Pick folder", control);

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)));

        Assert.Equal(DialogResultKind.NoResult, dialog.Result);
        Assert.Equal(@"D:\Media\Music\Jazz", dialog.Payload);
    }

    [Fact]
    public async Task Escape_ReturnsNull()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.SelectFolderAsync(
            shell, "Pick folder", @"D:\Media\Movies", WindowsTree(), ct: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // ----- Space / Right open only when subfolders exist -----

    [Theory]
    [InlineData(ConsoleKey.Spacebar)]
    [InlineData(ConsoleKey.RightArrow)]
    public void OpenKey_EntersFolderWithSubfolders(ConsoleKey key)
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Music"); // highlights Music (has Jazz)

        Assert.True(control.HandleKey(new KeyEvent(key, ConsoleModifiers.None)).IsHandled);

        Assert.Equal(@"D:\Media\Music\Jazz", control.Payload);
        // After opening Music, the now-current Jazz folder is the highlighted in-frame list row.
        Assert.Contains("Jazz", Render(control));
    }

    [Theory]
    [InlineData(ConsoleKey.Spacebar)]
    [InlineData(ConsoleKey.RightArrow)]
    public void OpenKey_DoesNothingForFolderWithoutSubfolders(ConsoleKey key)
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies"); // Movies has no children

        Assert.True(control.HandleKey(new KeyEvent(key, ConsoleModifiers.None)).IsHandled);

        Assert.Equal(@"D:\Media\Movies", control.Payload);
    }

    // ----- Backspace / Left navigate up -----

    [Theory]
    [InlineData(ConsoleKey.Backspace)]
    [InlineData(ConsoleKey.LeftArrow)]
    public void UpKey_NavigatesToParentAndHighlightsOrigin(ConsoleKey key)
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Music"); // location D:\Media

        Assert.True(control.HandleKey(new KeyEvent(key, ConsoleModifiers.None)).IsHandled);

        Assert.Equal(@"D:\Media", control.Payload);
        Assert.Contains(@"D:\Media", Render(control));
    }

    [Fact]
    public void UpKey_FromDriveRoot_ReturnsToDriveList()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"C:\Users"); // location C:\

        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.LeftArrow, ConsoleModifiers.None)).IsHandled);

        Assert.Equal(@"C:\", control.Payload);
    }

    [Fact]
    public void UpKey_FromUnixRoot_StaysAtRoot()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, UnixTree(), "/home"); // location "/"

        // First Up moves from "/" highlight; we are already at "/", so Up should do nothing.
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.LeftArrow, ConsoleModifiers.None)).IsHandled);
        Assert.Equal("/home", control.Payload);
    }

    [Fact]
    public void DriveList_ListsDrives()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree()); // null initial → drive list
        var text = string.Join("\n", TigerConsole.RenderGridToLines(control.ToGrid()));

        Assert.Contains(@"C:\", text);
        Assert.Contains(@"D:\", text);
    }

    // ----- Display: hint, current path, openable marker -----

    [Fact]
    public void Hint_ExposesNavigationHelp()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Music");

        Assert.Equal(
            "↑↓ Move   Enter Open   Tab Move   Esc Cancel",
            control.Hint);
    }

    [Fact]
    public void Renders_Hint_CurrentPath_AndOpenMarker()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Music");

        var text = string.Join("\n", TigerConsole.RenderGridToLines(new InlineDialog(shell, "Pick folder", control).ToGrid()));

        Assert.Contains("Music", text);           // highlighted row
        Assert.Contains(@"D:\Media", text);       // editable path input (scrolled to the start)
        Assert.Contains("Enter Open", text);      // hint/status bar
        Assert.Contains(ConsoleSymbol.ChevronRight.ToString(), text); // Music is openable
    }

    // ----- Empty location is not confirmable -----

    [Fact]
    public void EmptyLocation_IsNotConfirmable()
    {
        var shell = new TestShell();
        var emptyBrowser = new FakeFolderBrowser(
            windows: true,
            tree: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            drives: new List<string>());
        var emptyControl = new InlineFolderSelect(shell, emptyBrowser);

        Assert.False(emptyControl.CanConfirm);
        Assert.Null(emptyControl.Payload);
        Assert.Contains("No folders available", string.Join("\n", TigerConsole.RenderGridToLines(emptyControl.ToGrid())));

        // A populated location is confirmable.
        var populated = new InlineFolderSelect(shell, WindowsTree(), @"C:\Users\Public");
        Assert.True(populated.CanConfirm);
    }

    // ----- Real filesystem browser: graceful failures + correct enumeration -----

    [Fact]
    public void FileSystemBrowser_MissingPath_DoesNotThrow()
    {
        var browser = new FileSystemFolderBrowser();
        var bogus = OperatingSystem.IsWindows()
            ? @"Z:\this\does\not\exist\xyz"
            : "/this/does/not/exist/xyz";

        Assert.Empty(browser.GetEntries(bogus));
        Assert.False(browser.TryGetParent(null, out _));

        // ResolveInitial and control construction must not throw on an unreadable path.
        var (_, _) = browser.ResolveInitial(bogus);
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, browser, bogus);
        _ = control.ToGrid();
    }

    [Fact]
    public void FileSystemBrowser_EnumeratesTempTree()
    {
        var root = Directory.CreateTempSubdirectory("tigercli_folder_");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "alpha"));
            Directory.CreateDirectory(Path.Combine(root.FullName, "alpha", "child"));
            Directory.CreateDirectory(Path.Combine(root.FullName, "beta"));

            var browser = new FileSystemFolderBrowser();
            var entries = browser.GetEntries(root.FullName);

            Assert.Equal(2, entries.Count);

            var alpha = entries.Single(e => e.Label == "alpha");
            var beta = entries.Single(e => e.Label == "beta");
            Assert.True(alpha.HasChildren);
            Assert.False(beta.HasChildren);

            var (location, highlight) = browser.ResolveInitial(alpha.Path);
            Assert.Equal(root.FullName, location);
            Assert.Equal(alpha.Path, highlight);

            Assert.True(browser.TryGetParent(root.FullName, out var parent));
            Assert.Equal(root.Parent!.FullName, parent);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task SelectFolderAsync_NullShellThrows()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => TigerTui.SelectFolderAsync(
                null!, "Pick", initialPath: null, browser: null, ct: TestContext.Current.CancellationToken));
    }

    // ----- Composite widget layout / focus -----

    [Fact]
    public void GetWidgets_ExposesPathListAndButtons_InExpectedAreas()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");

        var widgets = control.GetWidgets();

        Assert.Equal(3, widgets.Count);
        Assert.Equal(InlineDialogArea.AboveFrameWithIndicators, widgets[0].Area);
        Assert.Equal(InlineDialogArea.InFrameScrollable, widgets[1].Area);
        Assert.Equal(InlineDialogArea.BelowFrame, widgets[2].Area);
    }

    [Fact]
    public void InitialFocus_IsFolderList()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");

        var widgets = control.GetWidgets();

        Assert.False(widgets[0].IsFocused);
        Assert.True(widgets[1].IsFocused);
        Assert.False(widgets[2].IsFocused);
    }

    [Fact]
    public void TabAndShiftTab_CycleFocus()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");

        control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.None));
        Assert.True(control.GetWidgets()[2].IsFocused);

        control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.None));
        Assert.True(control.GetWidgets()[0].IsFocused);

        control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.Shift));
        Assert.True(control.GetWidgets()[2].IsFocused);
    }

    [Fact]
    public void PathInput_EditsPathText()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");

        FocusPath(control);
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.X, ConsoleModifiers.None, 'X')).IsHandled);

        // The focused path input scrolls to keep the cursor (end of text) in view, so the freshly
        // typed tail is visible even though the field is narrower than the full path.
        Assert.Contains("MoviesX", Render(control));
    }

    [Fact]
    public void EnterOnFocusedValidPath_ReturnsOk()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");
        var dialog = new InlineDialog(shell, "Pick folder", control);

        FocusPath(control);

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)));

        Assert.Equal(DialogResultKind.Ok, dialog.Result);
        Assert.Equal(@"D:\Media\Movies", dialog.Payload);
    }

    [Fact]
    public void EnterOnFocusedInvalidPath_IsHandledAndShowsValidationHint()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");
        var dialog = new InlineDialog(shell, "Pick folder", control);

        FocusPath(control);
        control.HandleKey(new KeyEvent(ConsoleKey.X, ConsoleModifiers.None, 'X'));

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)));

        Assert.Equal(DialogResultKind.NoResult, dialog.Result);
        Assert.Equal("Invalid path entered", control.Hint);
    }

    [Fact]
    public void OkButton_ValidatesPathBeforeReturningOk()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");
        var dialog = new InlineDialog(shell, "Pick folder", control);

        FocusButtons(control);

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)));

        Assert.Equal(DialogResultKind.Ok, dialog.Result);
        Assert.Equal(@"D:\Media\Movies", dialog.Payload);
    }

    [Fact]
    public void OkButtonOnInvalidPath_ShowsValidationHintAndDoesNotComplete()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");
        var dialog = new InlineDialog(shell, "Pick folder", control);

        FocusPath(control);
        control.HandleKey(new KeyEvent(ConsoleKey.X, ConsoleModifiers.None, 'X'));
        FocusButtons(control);

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)));

        Assert.Equal(DialogResultKind.NoResult, dialog.Result);
        Assert.Equal("Invalid path entered", control.Hint);
    }

    [Fact]
    public void CancelButton_ReturnsCancel()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");
        var dialog = new InlineDialog(shell, "Pick folder", control);

        FocusButtons(control);
        control.HandleKey(new KeyEvent(ConsoleKey.RightArrow, ConsoleModifiers.None));

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)));

        Assert.Equal(DialogResultKind.Cancel, dialog.Result);
    }

    [Fact]
    public void ValidationHint_ClearsWhenPathTextChanges()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");

        FocusPath(control);
        control.HandleKey(new KeyEvent(ConsoleKey.X, ConsoleModifiers.None, 'X'));
        control.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None));
        Assert.Equal("Invalid path entered", control.Hint);

        control.HandleKey(new KeyEvent(ConsoleKey.Backspace, ConsoleModifiers.None));

        Assert.NotEqual("Invalid path entered", control.Hint);
    }

    [Fact]
    public void ValidationHint_ClearsAfterSuccessfulNavigation()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Music");

        FocusPath(control);
        control.HandleKey(new KeyEvent(ConsoleKey.X, ConsoleModifiers.None, 'X'));
        control.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None));
        Assert.Equal("Invalid path entered", control.Hint);

        FocusList(control);
        control.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None));

        Assert.NotEqual("Invalid path entered", control.Hint);
        Assert.Equal(@"D:\Media\Music\Jazz", control.Payload);
    }

    [Fact]
    public void EscapeCancelsThroughDialogFallback()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");
        var dialog = new InlineDialog(shell, "Pick folder", control);

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Escape, ConsoleModifiers.None)));

        Assert.Equal(DialogResultKind.Cancel, dialog.Result);
    }

    [Fact]
    public void FocusedDescriptor_DrivesActiveDecorations()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, WindowsTree(), @"D:\Media\Movies");

        var listFocused = control.GetWidgets().Single(w => w.IsFocused);
        Assert.Equal(CliControlDecoration.VerticalScrollBar, listFocused.Decoration);

        FocusPath(control);
        var pathFocused = control.GetWidgets().Single(w => w.IsFocused);
        Assert.Equal(CliControlDecoration.HorizontalIndicators, pathFocused.Decoration);
    }

    private static string Render(InlineFolderSelect control)
    {
        return string.Join("\n", TigerConsole.RenderGridToLines(new InlineDialog(control.Shell, title: null, control).ToGrid()));
    }

    private static void FocusPath(InlineFolderSelect control)
    {
        while (!control.GetWidgets()[0].IsFocused)
            control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.Shift));
    }

    private static void FocusButtons(InlineFolderSelect control)
    {
        while (!control.GetWidgets()[2].IsFocused)
            control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.None));
    }

    private static void FocusList(InlineFolderSelect control)
    {
        while (!control.GetWidgets()[1].IsFocused)
            control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.None));
    }
}
