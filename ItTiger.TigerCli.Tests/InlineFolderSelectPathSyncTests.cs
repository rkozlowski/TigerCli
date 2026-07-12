using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;
using System.Reflection;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Path-input → folder-list synchronization for <see cref="InlineFolderSelect"/>: editing the path and
/// crossing a separator (or leaving focus) loads the deepest valid folder prefix. Windows-style trees,
/// matching the rest of the folder-select suite. The modal test uses a gated browser to prove the
/// async/spinner path is used; non-modal tests prove synchronous behavior.
/// </summary>
public sealed class InlineFolderSelectPathSyncTests : TestBase
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Windows-style in-memory tree with proper longest-prefix ResolveInitial and an optional one-shot
    // gate (to hold a background load open for the modal spinner test).
    private sealed class PathSyncBrowser : IFolderBrowser
    {
        private readonly bool _invalidRootFallbackToZArchive;
        private readonly Dictionary<string, string[]> _tree = new(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\"] = [@"C:\Users", @"C:\Program Files"],
            [@"C:\Users"] = [@"C:\Users\Alice", @"C:\Users\Bob"],
            [@"C:\Users\Alice"] = [@"C:\Users\Alice\Docs"],
            [@"C:\Users\Alice\Docs"] = [],
            [@"C:\Users\Bob"] = [],
            [@"C:\Program Files"] = [],
            [@"Z:\"] = [@"Z:\Archive", @"Z:\Builds"],
            [@"Z:\Archive"] = [],
            [@"Z:\Builds"] = [],
            [@"Y:\"] = [],
        };

        private readonly string[] _drives = [@"C:\", @"Y:\", @"Z:\"];
        private readonly Dictionary<string, string?> _parentOf = new(StringComparer.OrdinalIgnoreCase);
        private volatile TaskCompletionSource? _gate;

        public PathSyncBrowser(bool invalidRootFallbackToZArchive = false)
        {
            _invalidRootFallbackToZArchive = invalidRootFallbackToZArchive;

            foreach (var (location, children) in _tree)
                foreach (var child in children)
                    _parentOf[child] = location;
            _parentOf[@"C:\"] = null; // drive root → drive list
            _parentOf[@"Z:\"] = null; // drive root → drive list
            _parentOf[@"Y:\"] = null; // drive root → drive list
        }

        public TaskCompletionSource ArmNextLoad()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _gate = gate;
            return gate;
        }

        public string? RootLocation => null;

        public IReadOnlyList<FolderEntry> GetEntries(string? location)
        {
            var gate = _gate;
            if (gate is not null)
            {
                _gate = null;
                gate.Task.GetAwaiter().GetResult();
            }

            if (location is null)
            {
                return _drives
                    .Select(p => new FolderEntry(p, p, _tree.TryGetValue(p, out var c) && c.Length > 0))
                    .ToList();
            }

            var loc = location;
            if (!_tree.TryGetValue(loc, out var children))
                return Array.Empty<FolderEntry>();

            return children.Select(p => new FolderEntry(Leaf(p), p, _tree.TryGetValue(p, out var c) && c.Length > 0)).ToList();
        }

        public bool TryGetParent(string? location, out string? parent)
        {
            parent = null;
            if (location is null)
                return false;
            if (_parentOf.TryGetValue(location, out var p))
            {
                parent = p;     // may be null for a drive root (→ drive list)
                return true;
            }
            return false;
        }

        public (string? location, string? highlightPath) ResolveInitial(string? initialPath)
        {
            if (string.IsNullOrWhiteSpace(initialPath))
                return (RootLocation, null);

            if (_invalidRootFallbackToZArchive && IsInvalidRootedLooking(initialPath))
                return (@"Z:\", @"Z:\Archive");

            // Walk up to the deepest existing ancestor (the longest valid folder prefix).
            string? probe = Normalize(initialPath);
            string? target = null;
            while (probe is not null)
            {
                if (_tree.ContainsKey(probe) || _parentOf.ContainsKey(probe))
                {
                    target = probe;
                    break;
                }
                probe = StringParent(probe);
            }

            if (target is null)
                return (RootLocation, null);

            return TryGetParent(target, out var parent) ? (parent, target) : (target, null);
        }

        private static string Normalize(string p)
        {
            p = p.Replace('/', '\\');
            if (p.Length == 3 && p[1] == ':' && p[2] == '\\')
                return p; // "C:\"
            var t = p.TrimEnd('\\');
            if (t.Length == 2 && t[1] == ':')
                return t + "\\"; // "C:" → "C:\"
            return t;
        }

        private static bool IsInvalidRootedLooking(string p) =>
            p.Length >= 2 && p[0] == ':' && (p[1] == '\\' || p[1] == '/');

        private static string? StringParent(string p)
        {
            var t = p.TrimEnd('\\');
            int idx = t.LastIndexOf('\\');
            if (idx < 0)
                return null;
            if (idx == 2 && t[1] == ':')
                return t[..3]; // "C:\X" → "C:\"
            return t[..idx];
        }

        private static string Leaf(string p)
        {
            var t = p.TrimEnd('\\');
            int i = t.LastIndexOf('\\');
            return i < 0 ? t : t[(i + 1)..];
        }
    }

    private static string Render(InlineFolderSelect control) =>
        string.Join("\n", TigerConsole.RenderGridToLines(new InlineDialog(control.Shell, title: null, control).ToGrid()));

    // The path-input widget renders as the first line (above the frame). Used to assert the typed text
    // is preserved (not rewritten to the loaded selection) while the input is focused.
    private static string PathLine(InlineFolderSelect control) => Render(control).Split('\n')[0];

    private static string ListText(InlineFolderSelect control) =>
        string.Join("\n", TigerConsole.RenderGridToLines(control.GetWidgets()[1].Grid));

    private static void Type(InlineFolderSelect control, string text)
    {
        foreach (var ch in text)
            control.HandleKey(new KeyEvent((ConsoleKey)0, ConsoleModifiers.None, ch));
    }

    private static void SetPathInputTextAsEdited(InlineFolderSelect control, string text)
    {
        var pathInputField = typeof(InlineFolderSelect).GetField("_pathInput", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("InlineFolderSelect._pathInput field not found.");
        var pathEditedField = typeof(InlineFolderSelect).GetField("_pathEdited", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("InlineFolderSelect._pathEdited field not found.");

        var pathInput = Assert.IsType<InlineTextInputWidget>(pathInputField.GetValue(control));
        pathInput.SetText(text);
        pathEditedField.SetValue(control, true);
    }

    private static void FocusPath(InlineFolderSelect control)
    {
        while (!control.GetWidgets()[0].IsFocused)
            control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.Shift));
    }

    private static void FocusList(InlineFolderSelect control)
    {
        while (!control.GetWidgets()[1].IsFocused)
            control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.None));
    }

    // ------------------------------------------------------------------
    // 1. List navigation still updates the path input (existing direction unchanged).
    // ------------------------------------------------------------------

    [Fact]
    public void ListNavigation_StillUpdatesPathInput()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users\Alice");

        Assert.Equal(@"C:\Users\Alice", control.Payload);
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.DownArrow, ConsoleModifiers.None)).IsHandled);

        Assert.Equal(@"C:\Users\Bob", control.Payload);     // selection moved
        Assert.Contains(@"C:\Users\Bob", Render(control));  // path input followed the list
    }

    // ------------------------------------------------------------------
    // 2 + 6. Typing a valid path then leaving focus loads that folder; typed text is preserved.
    // ------------------------------------------------------------------

    [Fact]
    public void TypingValidPath_ThenLeavingFocus_LoadsFolder_PreservesText()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");

        FocusPath(control);
        Type(control, @"\Alice");   // path becomes C:\Users\Alice
        FocusList(control);         // leaving the path input triggers the sync

        // List synced to C:\Users\Alice contents; its first entry (Docs) is the selected value.
        Assert.Equal(@"C:\Users\Alice\Docs", control.Payload);
    }

    [Fact]
    public void TypingLeafFolder_ThenLeavingPathInput_ShowsParentAndSelectsLeaf()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");

        FocusPath(control);
        SetPathInputTextAsEdited(control, @"C:\Users\Bob");
        FocusList(control);

        Assert.Equal(@"C:\Users\Bob", control.Payload);
        Assert.Contains("Alice", ListText(control)); // parent list is still shown
        Assert.Contains("Bob", ListText(control));
        Assert.DoesNotContain("Docs", ListText(control));
        Assert.Contains(@"C:\Users\Bob", PathLine(control));
    }

    // ------------------------------------------------------------------
    // 3 + 8. Typing a separator after a valid segment updates the list immediately (synchronous, non-modal).
    // ------------------------------------------------------------------

    [Fact]
    public void TypingSeparator_AfterValidSegment_UpdatesListSynchronously()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");

        FocusPath(control);
        Type(control, @"\");        // C:\Users\ — separator marks a folder boundary

        // No modal loop / pump: the list updated synchronously on the keystroke (C:\Users contents,
        // first entry selected).
        Assert.Equal(@"C:\Users\Alice", control.Payload);

        // #6: the typed text is preserved — the path input still shows what was typed, not the loaded
        // selection (C:\Users\Alice would appear if the list-load had rewritten the input).
        Assert.Contains(@"C:\Users\", PathLine(control));
        Assert.DoesNotContain("Alice", PathLine(control));
    }

    [Fact]
    public void TypingDriveRoot_ThenLeavingPathInput_LoadsRoot()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");

        FocusPath(control);
        SetPathInputTextAsEdited(control, @"Z:\");
        FocusList(control);

        Assert.Equal(@"Z:\Archive", control.Payload);
        Assert.Contains(@"Z:\", PathLine(control));
        Assert.DoesNotContain(@"Z:\Archive", PathLine(control));
    }

    [Fact]
    public void TypingDriveRootWithoutChildFolders_SelectsRootInTopLevelList()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");

        FocusPath(control);
        SetPathInputTextAsEdited(control, @"Y:\");
        FocusList(control);

        Assert.Equal(@"Y:\", control.Payload);
        Assert.Contains(@"C:\", ListText(control));
        Assert.Contains(@"Z:\", ListText(control));
        Assert.Contains(@"Y:\", PathLine(control));
    }

    [Fact]
    public void TypingDriveRootSeparator_LoadsRoot()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");

        FocusPath(control);
        SetPathInputTextAsEdited(control, @"Z:");
        Type(control, @"\");

        Assert.Equal(@"Z:\Archive", control.Payload);
        Assert.Contains(@"Z:\", PathLine(control));
        Assert.DoesNotContain(@"Z:\Archive", PathLine(control));
    }

    [Fact]
    public void EditingDriveLetter_WhenTextStillEndsWithSeparator_LoadsRoot()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\");

        FocusPath(control);
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.Home, ConsoleModifiers.None)).IsHandled);
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.Delete, ConsoleModifiers.None)).IsHandled);
        Assert.True(control.HandleKey(new KeyEvent((ConsoleKey)0, ConsoleModifiers.None, 'Z')).IsHandled);

        Assert.Equal(@"Z:\Archive", control.Payload);
        Assert.Contains(@"Z:\", PathLine(control));
        Assert.DoesNotContain(@"Z:\Archive", PathLine(control));
    }

    [Fact]
    public void TypingInvalidPathBelowDriveRoot_DoesNotSnapToRoot()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");

        Assert.Equal(@"C:\Users", control.Payload);

        FocusPath(control);
        SetPathInputTextAsEdited(control, @"Z:\NoSuchFolder\");
        FocusList(control);

        Assert.Equal(@"C:\Users", control.Payload);
        Assert.Contains(@"Z:\NoSuchFolder\", PathLine(control));
    }

    [Fact]
    public void TypingInvalidRootedPath_ThenLeavingPathInput_KeepsCurrentList()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(
            shell,
            new PathSyncBrowser(invalidRootFallbackToZArchive: true),
            @"C:\Users");

        Assert.Equal(@"C:\Users", control.Payload);

        FocusPath(control);
        SetPathInputTextAsEdited(control, @":\");
        FocusList(control);

        Assert.Equal(@"C:\Users", control.Payload);
        Assert.Contains("Users", ListText(control));
        Assert.Contains("Program Files", ListText(control));
        Assert.DoesNotContain("Archive", ListText(control));
        Assert.Contains(@":\", PathLine(control));
    }

    [Fact]
    public void TypingInvalidRootedPathSeparator_KeepsCurrentList()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(
            shell,
            new PathSyncBrowser(invalidRootFallbackToZArchive: true),
            @"C:\Users");

        FocusPath(control);
        SetPathInputTextAsEdited(control, ":");
        Type(control, @"\");

        Assert.Equal(@"C:\Users", control.Payload);
        Assert.Contains("Users", ListText(control));
        Assert.Contains("Program Files", ListText(control));
        Assert.DoesNotContain("Archive", ListText(control));
        Assert.Contains(@":\", PathLine(control));
    }

    [Fact]
    public void TypingInvalidRootedPathWithTail_KeepsCurrentList()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(
            shell,
            new PathSyncBrowser(invalidRootFallbackToZArchive: true),
            @"C:\Users");

        FocusPath(control);
        SetPathInputTextAsEdited(control, @":\anything");
        FocusList(control);

        Assert.Equal(@"C:\Users", control.Payload);
        Assert.Contains("Users", ListText(control));
        Assert.Contains("Program Files", ListText(control));
        Assert.DoesNotContain("Archive", ListText(control));
        Assert.Contains(@":\anything", PathLine(control));
    }

    // ------------------------------------------------------------------
    // 4. An invalid full path falls back to the longest valid folder prefix.
    // ------------------------------------------------------------------

    [Fact]
    public void InvalidFullPath_FallsBackToLongestValidPrefix()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");

        FocusPath(control);
        Type(control, @"\Zzz");     // C:\Users\Zzz — Zzz does not exist

        // Longest valid prefix is C:\Users: its contents are listed (first entry selected); the invalid
        // tail is ignored, not navigated into.
        Assert.Equal(@"C:\Users\Alice", control.Payload);
        Assert.Contains("Zzz", PathLine(control)); // typed text preserved
    }

    // ------------------------------------------------------------------
    // 5. A fully invalid path (only the drive/root resolves) does not replace the current list.
    // ------------------------------------------------------------------

    [Fact]
    public void FullyInvalidPath_KeepsCurrentList()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, new PathSyncBrowser(), @"C:\Users");
        // Init resolves C:\Users → location C:\, highlight Users: the current list is the C:\ contents
        // with Users selected.
        Assert.Equal(@"C:\Users", control.Payload);

        FocusPath(control);
        Type(control, @"Zqq\");     // C:\UsersZqq\ — only C:\ (root) resolves → keep current list

        Assert.Equal(@"C:\Users", control.Payload);  // current list unchanged (no snap to root)
        Assert.Contains("Zqq", PathLine(control));    // typed text preserved
    }

    // ------------------------------------------------------------------
    // 7. Inside a modal session the path→list sync uses the async load + spinner path.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ModalPathSync_UsesAsyncLoadAndSpinner()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(useManualClock: true);
        var browser = new PathSyncBrowser();
        var control = new InlineFolderSelect(shell, browser, @"C:\Users"); // initial (ungated) load
        var dialog = new InlineDialog(shell, "Pick folder", control);

        var modal = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);

        // Focus the path input (list → path).
        shell.Terminal.EnqueueKey(ConsoleKey.Tab, ConsoleModifiers.Shift);
        await shell.Terminal.WaitForRenderCountAsync(2, Timeout, ct);

        // Type a separator; the resulting C:\Users load blocks in the background and shows the spinner.
        var gate = browser.ArmNextLoad();
        shell.Terminal.EnqueueKey((ConsoleKey)0, ConsoleModifiers.None, '\\');
        await shell.Terminal.WaitForRenderCountAsync(3, Timeout, ct);
        Assert.Contains("╔[", shell.Terminal.LastRenderedText); // spinner on the top frame while loading

        int before = shell.Terminal.RenderCount;
        gate.TrySetResult();
        await shell.Terminal.WaitForRenderCountAsync(before + 1, Timeout, ct);

        Assert.Equal(@"C:\Users\Alice", control.Payload);              // list synced to C:\Users
        Assert.DoesNotContain("╔[", shell.Terminal.LastRenderedText);  // spinner gone

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await modal.WaitAsync(Timeout, ct);
    }
}
