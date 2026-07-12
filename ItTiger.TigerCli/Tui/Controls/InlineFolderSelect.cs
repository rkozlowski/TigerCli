using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Composite folder picker: editable path input, folder list, and OK/Cancel buttons.
/// </summary>
/// <remarks>
/// The control combines a path input above the frame, a scrollable folder list inside the frame, and
/// an OK/Cancel button row below the frame. During a modal session folder listings are loaded on a
/// background task and a top-frame spinner overlay is shown while loading; the result is applied on
/// the modal-loop thread. The path input and list stay synchronized in both directions: list
/// navigation updates the path text, and path edits can reload the list for the deepest existing
/// folder prefix while preserving the user's typed text.
/// <para>
/// List focus handles Up/Down/PageUp/PageDown/Home/End through the list widget; Enter, Space, or
/// Right opens a highlighted folder with children; Left or Backspace navigates to the parent. Path
/// focus edits text and Enter validates the typed path. Button focus activates OK or Cancel.
/// </para>
/// </remarks>
public sealed class InlineFolderSelect : InlineMultiControl
{
    private static readonly string OpenMarker = $"{ConsoleSymbol.ChevronRight} ";
    private const string NoMarker = "  ";

    private readonly IFolderBrowser _browser;
    private readonly InlineTextInputWidget _pathInput;
    private readonly InlineSelectWidget _folderList;
    private readonly InlineButtonGroupWidget _buttons;
    private readonly int _pathIndex;
    private readonly int _listIndex;
    private readonly int _buttonIndex;

    // Loading spinner — the first consumer of the generic periodic-overlay mechanism. Inactive until a
    // navigation load starts; the overlay renders nothing while the ticker is inactive.
    private readonly SpinnerTicker _spinner;
    private readonly InlineActivityOverlay[] _activityOverlays;

    // Async-load coordination. The background task only mutates the pending fields under _loadSync;
    // results are applied to widgets on the modal-loop thread in AdvanceState.
    private readonly object _loadSync = new();
    private CancellationToken _modalToken;
    private volatile bool _modalActive;
    private bool _loading;
    private int _loadGeneration;
    private bool _closed;
    private bool _hasPending;
    private IReadOnlyList<FolderEntry>? _pendingEntries;
    private string? _pendingHighlight;
    private bool _pendingUpdatePathInput;
    private bool _pendingHasLocation;
    private string? _pendingLocation;

    private string? _location;
    private IReadOnlyList<FolderEntry> _entries;
    private string? _currentPath;
    private string? _validationHint;

    // True when the user has edited the path input since it was last set programmatically (list
    // navigation, load apply, accept). Gates focus-leave path→list sync so merely tabbing through the
    // path input — without editing — never reloads the list.
    private bool _pathEdited;

    /// <summary>
    /// Creates a folder picker using the supplied browser abstraction. The initial path is resolved
    /// through <see cref="IFolderBrowser.ResolveInitial(string?)"/>.
    /// </summary>
    public InlineFolderSelect(ICliAppShell shell, IFolderBrowser browser, string? initialPath = null)
        : base(shell)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        var (location, highlightPath) = _browser.ResolveInitial(initialPath);
        _location = location;
        _entries = Array.Empty<FolderEntry>();

        _spinner = new SpinnerTicker(active: false);
        _activityOverlays =
        [
            new InlineActivityOverlay
            {
                Area = InlineDialogArea.TopFrame,
                ColumnOffset = 1,
                MaxLength = InlineActivityOverlay.SpinnerMaxLength,
                Ticker = _spinner,
                ContentFormatter = static frame => $"[{frame}]",
                Style = Shell.Theme.Resolve(ThemeStyle.Frame).CharStyle ?? default,
            }
        ];

        var inputWidth = shell.Viewport.Width - 10;
        var listMinWidth = 10;
        var listMaxWidth = shell.Viewport.Width - 20;

        // Initial population is synchronous: it runs before the modal loop exists, so there is no loop
        // to keep alive and nothing to animate.
        LoadEntriesSync(highlightPath);

        _pathInput = new InlineTextInputWidget(shell, _currentPath, width: inputWidth);
        _folderList = new InlineSelectWidget(shell, LabelsForEntries(), SelectedIndexForCurrentPath(), 
            minWidth: listMinWidth, maxWidth: listMaxWidth)
        {
            EmptyStateTextOverride = TigerCliResources.Get("Tui_FolderSelect_Empty", Shell.Culture)
        };
        _buttons = new InlineButtonGroupWidget(shell, new[]
        {
            new InlineButtonWidget(shell, TigerCliResources.Get("Tui_Button_Ok", Shell.Culture), DialogResultKind.Ok),
            new InlineButtonWidget(shell, TigerCliResources.Get("Tui_Button_Cancel", Shell.Culture), DialogResultKind.Cancel),
        });

        _pathIndex = AddWidget(
            _pathInput,
            InlineDialogArea.AboveFrameWithIndicators,
            CliControlDecoration.HorizontalIndicators,
            CliScrollMode.Horizontal,
            CliScrollThumbMode.ActivePoint,
            Shell.Theme.Resolve(ThemeStyle.TextInput),
            TigerCliResources.Get("Tui_FolderSelect_PathHint", Shell.Culture));

        _listIndex = AddWidget(
            _folderList,
            InlineDialogArea.InFrameScrollable,
            CliControlDecoration.VerticalScrollBar,
            CliScrollMode.Vertical,
            CliScrollThumbMode.ActivePoint,
            hint: TigerCliResources.Get("Tui_FolderSelect_ListHint", Shell.Culture));

        _buttonIndex = AddWidget(
            _buttons,
            InlineDialogArea.BelowFrame,
            hint: TigerCliResources.Get("Tui_FolderSelect_ButtonHint", Shell.Culture));

        SetFocusedWidgetIndex(_listIndex);
    }

    /// <summary>The currently accepted/selected folder path, or <c>null</c> when there is nothing to select.</summary>
    public override object? Payload => _currentPath;

    /// <summary>True when the current path text resolves to a selectable folder through the browser.</summary>
    public override bool CanConfirm => IsPathValid(_pathInput.Text);

    /// <summary>Validation message when path confirmation fails; otherwise the focused widget hint.</summary>
    public override string? Hint => _validationHint ?? base.Hint;

    /// <summary>Folder-select hints are raw localized text.</summary>
    public override CliFormattingMode HintMode => CliFormattingMode.Raw;

    /// <summary>The scrollbar thumb follows the logical active row or cursor.</summary>
    public override CliScrollThumbMode ThumbMode => CliScrollThumbMode.ActivePoint;

    /// <summary>Legacy single-widget metadata: folder select uses a vertical scrollbar for its list.</summary>
    public override CliControlDecoration ControlDecoration => CliControlDecoration.VerticalScrollBar;

    /// <summary>Legacy single-widget metadata: folder select scrolls vertically for its list.</summary>
    public override CliScrollMode ScrollMode => CliScrollMode.Vertical;

    /// <summary>Legacy single-widget metadata; actual layout is provided by <see cref="InlineMultiControl.GetWidgets"/>.</summary>
    public override InlineDialogArea DialogArea => InlineDialogArea.InFrameScrollable;

    /// <summary>
    /// Returns the structurally stable loading-spinner overlay. The overlay is always present; the
    /// ticker's active state controls whether it renders.
    /// </summary>
    public override IReadOnlyList<InlineActivityOverlay> GetActivityOverlays() => _activityOverlays;

    /// <summary>Enables asynchronous folder loading for the modal session.</summary>
    public override void OnModalOpened(CancellationToken modalToken)
    {
        _modalToken = modalToken;
        _modalActive = true;
    }

    /// <summary>Stops loading/spinner state and prevents pending background results from mutating the closed control.</summary>
    public override void OnModalClosed()
    {
        // Abandon any pending load and stop animating; never touch widgets after this.
        lock (_loadSync)
        {
            _closed = true;
            _hasPending = false;
            _pendingEntries = null;
            _pendingHasLocation = false;
            _pendingLocation = null;
        }

        _modalActive = false;
        _loading = false;
        _spinner.Stop();
    }

    /// <summary>
    /// Applies a completed background folder load on the modal-loop thread. Runs once per loop iteration;
    /// returns <c>true</c> only when a pending result was applied (so the loop re-renders the new list and
    /// the now-stopped spinner disappears).
    /// </summary>
    public override bool AdvanceState(DateTime nowUtc)
    {
        IReadOnlyList<FolderEntry> entries;
        string? highlight;
        bool updatePathInput;
        bool hasLocation;
        string? location;
        lock (_loadSync)
        {
            if (!_hasPending)
                return false;

            entries = _pendingEntries ?? Array.Empty<FolderEntry>();
            highlight = _pendingHighlight;
            updatePathInput = _pendingUpdatePathInput;
            hasLocation = _pendingHasLocation;
            location = _pendingLocation;
            _hasPending = false;
            _pendingEntries = null;
            _pendingHasLocation = false;
            _pendingLocation = null;
        }

        _loading = false;
        _spinner.Stop();
        if (hasLocation)
            _location = location;
        ApplyEntries(entries, highlight, updatePathInput);
        return true;
    }

    /// <inheritdoc/>
    protected override InlineKeyResult HandleFocusedWidgetKey(KeyEvent key)
    {
        if (FocusedWidgetIndex == _pathIndex)
            return HandlePathKey(key);

        if (FocusedWidgetIndex == _listIndex)
            return HandleListKey(key);

        if (FocusedWidgetIndex == _buttonIndex)
            return HandleButtonKey(key);

        return base.HandleFocusedWidgetKey(key);
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(int previousIndex, int currentIndex)
    {
        // Leaving the path input after an edit: sync the list once to the typed path, then let focus
        // move normally (the sync never blocks or gates focus). Tabbing through without editing is a no-op.
        if (previousIndex == _pathIndex && _pathEdited)
            SyncListToPathInput();

        ClearValidationHint();
    }

    private InlineKeyResult HandlePathKey(KeyEvent key)
    {
        if (key.Key == ConsoleKey.Enter && key.Mods == ConsoleModifiers.None)
            return ValidatePathForOk();

        string before = _pathInput.Text;
        var result = _pathInput.HandleKey(key);
        if (result.IsHandled && !string.Equals(before, _pathInput.Text, StringComparison.Ordinal))
        {
            _pathEdited = true;
            ClearValidationHint();

            // A separator boundary can be created either by inserting a separator, or by editing text
            // that already ends with one (for example changing "C:\" to "Z:\" at the drive letter).
            if (IsPathSeparator(key.KeyChar) || EndsWithPathSeparator(_pathInput.Text))
                SyncListToPathInput();
        }

        return result;
    }

    private InlineKeyResult HandleListKey(KeyEvent key)
    {
        // While a folder load is in flight the visible list is stale: swallow the keys that would start
        // another load (open / up), but let everything else bubble so focus changes (Tab) and dialog
        // cancel (Escape) stay responsive.
        if (_loading)
        {
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                case ConsoleKey.Spacebar:
                case ConsoleKey.RightArrow:
                case ConsoleKey.Backspace:
                case ConsoleKey.LeftArrow:
                    return InlineKeyResult.Handled;
            }

            return InlineKeyResult.NotHandled;
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
            case ConsoleKey.Spacebar:
            case ConsoleKey.RightArrow:
                OpenHighlighted();
                return InlineKeyResult.Handled;
            case ConsoleKey.Backspace:
            case ConsoleKey.LeftArrow:
                NavigateUp();
                return InlineKeyResult.Handled;
        }

        int previousSelected = _folderList.SelectedIndex;
        var result = _folderList.HandleKey(key);
        if (result.IsHandled && _folderList.SelectedIndex != previousSelected)
        {
            AcceptSelectedPath();
            ClearValidationHint();
        }

        return result;
    }

    private InlineKeyResult HandleButtonKey(KeyEvent key)
    {
        var result = _buttons.HandleKey(key);
        if (!result.IsHandled)
            return result;

        return result.Result switch
        {
            DialogResultKind.Ok => ValidatePathForOk(),
            DialogResultKind.Cancel => result,
            _ => result,
        };
    }

    private InlineKeyResult ValidatePathForOk()
    {
        if (TryAcceptPath(_pathInput.Text))
            return InlineKeyResult.WithResult(DialogResultKind.Ok);

        _validationHint = TigerCliResources.Get("Tui_FolderSelect_InvalidPathEntered", Shell.Culture);
        return InlineKeyResult.Handled;
    }

    private bool OpenHighlighted()
    {
        int selected = _folderList.SelectedIndex;
        if (selected < 0 || selected >= _entries.Count || !_entries[selected].HasChildren)
            return false;

        _location = _entries[selected].Path;
        BeginLoad(highlightPath: null);
        return true;
    }

    private bool NavigateUp()
    {
        if (!_browser.TryGetParent(_location, out var parent))
            return false;

        var previous = _location;
        _location = parent;
        BeginLoad(highlightPath: previous);
        return true;
    }

    /// <summary>
    /// Loads the entries for the current <see cref="_location"/>. Outside a modal session (construction,
    /// or direct key handling in tests) it loads synchronously, preserving the original behavior. Inside
    /// a modal session it offloads the (potentially slow) listing to a background task and starts the
    /// loading spinner; the result is applied on the loop thread in <see cref="AdvanceState"/>.
    /// </summary>
    private void BeginLoad(string? highlightPath, bool updatePathInput = true)
    {
        ClearValidationHint();

        if (!_modalActive)
        {
            LoadEntriesSync(highlightPath, updatePathInput);
            return;
        }

        int generation;
        lock (_loadSync)
        {
            generation = ++_loadGeneration; // supersede any earlier in-flight load
            _hasPending = false;
            _pendingEntries = null;
        }

        var location = _location;
        _loading = true;
        _spinner.Start();

        _ = Task.Run(() =>
        {
            IReadOnlyList<FolderEntry> entries;
            try
            {
                entries = _browser.GetEntries(location) ?? Array.Empty<FolderEntry>();
            }
            catch
            {
                // IFolderBrowser is contractually exception-safe; treat any stray failure as an empty
                // listing so the dialog shows its existing empty/error state and stays usable.
                entries = Array.Empty<FolderEntry>();
            }

            lock (_loadSync)
            {
                if (_closed || generation != _loadGeneration)
                    return; // modal closed, or a newer load superseded this one

                _pendingEntries = entries;
                _pendingHighlight = highlightPath;
                _pendingUpdatePathInput = updatePathInput;
                _pendingHasLocation = false;
                _pendingLocation = null;
                _hasPending = true;
            }
        }, _modalToken);
    }

    private void BeginPathSyncLoad(string targetLocation, string? fallbackLocation, string? fallbackHighlight)
    {
        ClearValidationHint();

        if (!_modalActive)
        {
            var result = ResolvePathSyncLoad(targetLocation, fallbackLocation, fallbackHighlight);
            _location = result.Location;
            ApplyEntries(result.Entries, result.HighlightPath, updatePathInput: false);
            return;
        }

        int generation;
        lock (_loadSync)
        {
            generation = ++_loadGeneration; // supersede any earlier in-flight load
            _hasPending = false;
            _pendingEntries = null;
            _pendingHasLocation = false;
            _pendingLocation = null;
        }

        _loading = true;
        _spinner.Start();

        _ = Task.Run(() =>
        {
            var result = ResolvePathSyncLoad(targetLocation, fallbackLocation, fallbackHighlight);

            lock (_loadSync)
            {
                if (_closed || generation != _loadGeneration)
                    return; // modal closed, or a newer load superseded this one

                _pendingEntries = result.Entries;
                _pendingHighlight = result.HighlightPath;
                _pendingUpdatePathInput = false;
                _pendingHasLocation = true;
                _pendingLocation = result.Location;
                _hasPending = true;
            }
        }, _modalToken);
    }

    private PathSyncLoadResult ResolvePathSyncLoad(
        string targetLocation,
        string? fallbackLocation,
        string? fallbackHighlight)
    {
        var targetEntries = SafeGetEntries(targetLocation);
        if (targetEntries.Count > 0 || fallbackHighlight is null)
            return new PathSyncLoadResult(targetLocation, targetEntries, HighlightPath: null);

        var fallbackEntries = SafeGetEntries(fallbackLocation);
        return new PathSyncLoadResult(fallbackLocation, fallbackEntries, fallbackHighlight);
    }

    private void LoadEntriesSync(string? highlightPath, bool updatePathInput = true)
    {
        ApplyEntries(SafeGetEntries(_location), highlightPath, updatePathInput);
    }

    private IReadOnlyList<FolderEntry> SafeGetEntries(string? location)
    {
        try
        {
            return _browser.GetEntries(location) ?? Array.Empty<FolderEntry>();
        }
        catch
        {
            return Array.Empty<FolderEntry>();
        }
    }

    // <paramref name="updatePathInput"/> is false for path→list sync, which must preserve exactly what
    // the user typed; it is true for list navigation, which keeps the path input in step with the list.
    private void ApplyEntries(IReadOnlyList<FolderEntry> entries, string? highlightPath, bool updatePathInput = true)
    {
        _entries = entries;

        int selected = _entries.Count == 0 ? -1 : 0;
        if (highlightPath is not null)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Path, highlightPath, StringComparison.OrdinalIgnoreCase))
                {
                    selected = i;
                    break;
                }
            }
        }

        _currentPath = selected >= 0 ? _entries[selected].Path : null;

        if (_folderList is not null)
            _folderList.SetItems(LabelsForEntries(), selected);

        if (updatePathInput && _pathInput is not null)
        {
            _pathInput.SetText(_currentPath ?? string.Empty);
            _pathEdited = false;
        }
    }

    private bool TryAcceptPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (!IsPathValid(path))
            return false;

        var (location, highlightPath) = _browser.ResolveInitial(path);
        _location = location;
        LoadEntriesSync(highlightPath);
        _currentPath = path;
        _pathInput.SetText(path);
        _pathEdited = false;
        ClearValidationHint();
        return true;
    }

    /// <summary>
    /// Path→list synchronization: resolves the current path-input text through <see cref="IFolderBrowser"/>
    /// and then either enters the resolved folder (when it has child folders) or shows its containing
    /// list with that folder highlighted. The typed text is preserved (the list load does not rewrite
    /// the path input). Inside a modal session this uses the async/spinner load path; otherwise it loads
    /// synchronously. The current list is left unchanged when a top-level folder is only the fallback
    /// ancestor for invalid input.
    /// </summary>
    private void SyncListToPathInput()
    {
        // Consume the edit: a single sync per edit, regardless of whether it changes the list.
        _pathEdited = false;

        var text = _pathInput.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (IsSyntacticallyInvalidPathInput(text))
            return;

        var (location, highlight) = _browser.ResolveInitial(text);
        var target = highlight ?? location; // deepest existing directory prefix of the typed path

        if (target is null)
            return; // nothing resolvable → keep the current list
        if (IsTopLevel(target) && !IsTypedPathEquivalentToResolvedPath(text, target))
            return; // root/drive is only a fallback ancestor → keep the current list
        if (string.Equals(target, _location, StringComparison.OrdinalIgnoreCase))
            return; // already showing this folder; preserve the current highlighted row

        BeginPathSyncLoad(target, location, highlight);
    }

    // A location is top-level (a drive root or the filesystem root) when the browser reports no parent
    // above it. Used to keep path→list sync from snapping the list to the root on a fully-invalid path.
    private bool IsTopLevel(string location) =>
        !_browser.TryGetParent(location, out var parent) || parent is null;

    private static bool IsPathSeparator(char c) =>
        c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

    private static bool EndsWithPathSeparator(string text) =>
        text.Length > 0 && IsPathSeparator(text[^1]);

    private static bool IsSyntacticallyInvalidPathInput(string text) =>
        text.Length >= 2 && text[0] == ':' && IsPathSeparator(text[1]);

    private static bool IsTypedPathEquivalentToResolvedPath(string typedPath, string resolvedPath) =>
        string.Equals(
            NormalizePathForComparison(typedPath),
            NormalizePathForComparison(resolvedPath),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizePathForComparison(string path)
    {
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        // Windows-style paths are used by IFolderBrowser implementations and tests even when the host
        // platform's separator is not '\', so normalize both common separator forms for comparison.
        if (Path.DirectorySeparatorChar != '\\')
            normalized = normalized.Replace('\\', Path.DirectorySeparatorChar);
        if (Path.DirectorySeparatorChar != '/')
            normalized = normalized.Replace('/', Path.DirectorySeparatorChar);

        normalized = normalized.TrimEnd(Path.DirectorySeparatorChar);
        if (normalized.Length == 2 && normalized[1] == ':')
            return normalized + Path.DirectorySeparatorChar;

        return normalized;
    }

    private bool IsPathValid(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var (location, highlightPath) = _browser.ResolveInitial(path);
        return string.Equals(highlightPath, path, StringComparison.OrdinalIgnoreCase)
            || (highlightPath is null && string.Equals(location, path, StringComparison.OrdinalIgnoreCase));
    }

    private void AcceptSelectedPath()
    {
        int selected = _folderList.SelectedIndex;
        _currentPath = selected >= 0 && selected < _entries.Count ? _entries[selected].Path : null;
        _pathInput.SetText(_currentPath ?? string.Empty);
        _pathEdited = false;
    }

    private int? SelectedIndexForCurrentPath()
    {
        if (_currentPath is null)
            return null;

        for (int i = 0; i < _entries.Count; i++)
            if (string.Equals(_entries[i].Path, _currentPath, StringComparison.OrdinalIgnoreCase))
                return i;

        return null;
    }

    private IReadOnlyList<string?> LabelsForEntries()
    {
        if (_entries.Count == 0)
            return Array.Empty<string?>();

        var labels = new string?[_entries.Count];
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            labels[i] = $"{(entry.HasChildren ? OpenMarker : NoMarker)}{entry.Label}";
        }

        return labels;
    }

    private void ClearValidationHint()
    {
        _validationHint = null;
    }

    private readonly record struct PathSyncLoadResult(
        string? Location,
        IReadOnlyList<FolderEntry> Entries,
        string? HighlightPath);
}
