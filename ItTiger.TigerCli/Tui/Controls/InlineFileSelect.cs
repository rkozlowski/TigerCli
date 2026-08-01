using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Internal single-file picker used by command-option file-open and file-save prompts.
/// </summary>
/// <remarks>
/// Initial population is synchronous because construction precedes the modal loop. During a modal
/// session, directory navigation loads entries on a background task and shows the same top-frame
/// spinner used by <see cref="InlineFolderSelect"/> until the modal loop applies the completed list.
/// </remarks>
internal sealed class InlineFileSelect : InlineMultiControl
{
    private static readonly string FolderMarker = $"{ConsoleSymbol.ChevronRight} ";
    private const string FileMarker = "  ";

    private readonly IFolderBrowser _browser;
    private readonly bool _save;
    private readonly string? _defaultExtension;
    private readonly string? _defaultFileName;
    private readonly string? _filter;
    private readonly InlineTextInputWidget _pathInput;
    private readonly InlineSelectWidget _entryList;
    private readonly InlineButtonGroupWidget _buttons;
    private readonly int _pathIndex;
    private readonly int _listIndex;
    private readonly int _buttonIndex;

    private readonly SpinnerTicker _spinner;
    private readonly InlineActivityOverlay[] _activityOverlays;

    private readonly object _loadSync = new();
    private CancellationToken _modalToken;
    private volatile bool _modalActive;
    private bool _loading;
    private int _loadGeneration;
    private bool _closed;
    private bool _hasPending;
    private IReadOnlyList<FilePickerEntry>? _pendingEntries;
    private string? _pendingHighlightPath;
    private string? _pendingPathText;

    private IReadOnlyList<FilePickerEntry> _entries = [];
    private string? _location;
    private string? _acceptedPath;
    private string? _validationHint;

    public InlineFileSelect(
        ICliAppShell shell,
        IFolderBrowser browser,
        bool save,
        string? initialPath,
        string? defaultExtension = null,
        string? defaultFileName = null,
        string? filter = null)
        : base(shell)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _save = save;
        _defaultExtension = NormalizeDefaultExtension(defaultExtension);
        _defaultFileName = string.IsNullOrWhiteSpace(defaultFileName) ? null : defaultFileName;
        _filter = string.IsNullOrWhiteSpace(filter) ? null : filter;

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

        var resolved = ResolveInitial(initialPath);
        _location = resolved.Location;
        _entries = ReadEntries(_location);

        var initialText = resolved.InputPath;
        _pathInput = new InlineTextInputWidget(shell, initialText, width: shell.Viewport.Width - 10);
        _entryList = new InlineSelectWidget(
            shell,
            LabelsForEntries(),
            SelectedIndexForPath(resolved.HighlightPath),
            minWidth: 10,
            maxWidth: shell.Viewport.Width - 20)
        {
            EmptyStateTextOverride = TigerCliResources.Get("Tui_FileSelect_Empty", Shell.Culture)
        };
        _buttons = new InlineButtonGroupWidget(shell,
        [
            new InlineButtonWidget(shell, TigerCliResources.Get("Tui_Button_Ok", Shell.Culture), DialogResultKind.Ok),
            new InlineButtonWidget(shell, TigerCliResources.Get("Tui_Button_Cancel", Shell.Culture), DialogResultKind.Cancel),
        ]);

        _pathIndex = AddWidget(
            _pathInput,
            InlineDialogArea.AboveFrameWithIndicators,
            CliControlDecoration.HorizontalIndicators,
            CliScrollMode.Horizontal,
            CliScrollThumbMode.ActivePoint,
            Shell.Theme.Resolve(ThemeStyle.TextInput),
            TigerCliResources.Get("Tui_FileSelect_PathHint", Shell.Culture));
        _listIndex = AddWidget(
            _entryList,
            InlineDialogArea.InFrameScrollable,
            CliControlDecoration.VerticalScrollBar,
            CliScrollMode.Vertical,
            CliScrollThumbMode.ActivePoint,
            hint: TigerCliResources.Get("Tui_FileSelect_ListHint", Shell.Culture));
        _buttonIndex = AddWidget(
            _buttons,
            InlineDialogArea.BelowFrame,
            hint: TigerCliResources.Get("Tui_FileSelect_ButtonHint", Shell.Culture));
        SetFocusedWidgetIndex(_listIndex);
    }

    public override object? Payload => _acceptedPath ?? NormalizeAcceptedPath(_pathInput.Text);

    public override bool CanConfirm => TryValidatePath(_pathInput.Text, out _);

    public override string? Hint => _validationHint ?? base.Hint;

    public override CliFormattingMode HintMode => CliFormattingMode.Raw;

    public override CliScrollThumbMode ThumbMode => CliScrollThumbMode.ActivePoint;

    public override CliControlDecoration ControlDecoration => CliControlDecoration.VerticalScrollBar;

    public override CliScrollMode ScrollMode => CliScrollMode.Vertical;

    public override InlineDialogArea DialogArea => InlineDialogArea.InFrameScrollable;

    /// <inheritdoc/>
    public override IReadOnlyList<InlineActivityOverlay> GetActivityOverlays() => _activityOverlays;

    /// <inheritdoc/>
    public override void OnModalOpened(CancellationToken modalToken)
    {
        _modalToken = modalToken;
        _modalActive = true;
    }

    /// <inheritdoc/>
    public override void OnModalClosed()
    {
        lock (_loadSync)
        {
            _closed = true;
            _hasPending = false;
            _pendingEntries = null;
            _pendingHighlightPath = null;
            _pendingPathText = null;
        }

        _modalActive = false;
        _loading = false;
        _spinner.Stop();
    }

    /// <inheritdoc/>
    public override bool AdvanceState(DateTime nowUtc)
    {
        IReadOnlyList<FilePickerEntry> entries;
        string? highlightPath;
        string? pathText;
        lock (_loadSync)
        {
            if (!_hasPending)
                return false;

            entries = _pendingEntries ?? [];
            highlightPath = _pendingHighlightPath;
            pathText = _pendingPathText;
            _hasPending = false;
            _pendingEntries = null;
            _pendingHighlightPath = null;
            _pendingPathText = null;
        }

        _loading = false;
        _spinner.Stop();
        ApplyEntries(entries, highlightPath, pathText);
        return true;
    }

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

    protected override void OnFocusChanged(int previousIndex, int currentIndex) => ClearValidationHint();

    private InlineKeyResult HandlePathKey(KeyEvent key)
    {
        if (key.Key == ConsoleKey.Enter && key.Mods == ConsoleModifiers.None)
            return ValidatePathForOk();

        var result = _pathInput.HandleKey(key);
        if (result.IsHandled)
            ClearValidationHint();
        return result;
    }

    private InlineKeyResult HandleListKey(KeyEvent key)
    {
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
                return ActivateHighlighted();
            case ConsoleKey.Spacebar:
            case ConsoleKey.RightArrow:
                if (HighlightedEntry is { IsDirectory: true })
                    OpenHighlightedFolder();
                return InlineKeyResult.Handled;
            case ConsoleKey.Backspace:
            case ConsoleKey.LeftArrow:
                NavigateUp();
                return InlineKeyResult.Handled;
        }

        var previous = _entryList.SelectedIndex;
        var result = _entryList.HandleKey(key);
        if (result.IsHandled && previous != _entryList.SelectedIndex)
        {
            UpdatePathFromHighlight();
            ClearValidationHint();
        }
        return result;
    }

    private InlineKeyResult HandleButtonKey(KeyEvent key)
    {
        var result = _buttons.HandleKey(key);
        return result.Result == DialogResultKind.Ok ? ValidatePathForOk() : result;
    }

    private InlineKeyResult ActivateHighlighted()
    {
        var entry = HighlightedEntry;
        if (entry == null)
            return InlineKeyResult.Handled;
        if (entry.Value.IsDirectory)
        {
            OpenHighlightedFolder();
            return InlineKeyResult.Handled;
        }

        _pathInput.SetText(entry.Value.Path);
        return ValidatePathForOk();
    }

    private void OpenHighlightedFolder()
    {
        var entry = HighlightedEntry;
        if (entry is not { IsDirectory: true })
            return;

        var fileName = _save ? CurrentSaveFileName() : null;
        _location = entry.Value.Path;
        BeginLoad(
            highlightPath: null,
            pathText: _save ? SavePathForLocation(_location, fileName) : _location);
    }

    private void NavigateUp()
    {
        if (!_browser.TryGetParent(_location, out var parent))
            return;

        var previous = _location;
        var fileName = _save ? CurrentSaveFileName() : null;
        _location = parent;
        BeginLoad(
            highlightPath: previous,
            pathText: _save ? SavePathForLocation(_location, fileName) : previous);
    }

    private InlineKeyResult ValidatePathForOk()
    {
        if (!TryValidatePath(_pathInput.Text, out var normalized))
        {
            _validationHint = TigerCliResources.Get(
                _save ? "Tui_FileSave_InvalidPathEntered" : "Tui_FileOpen_InvalidPathEntered",
                Shell.Culture);
            return InlineKeyResult.Handled;
        }

        _acceptedPath = normalized;
        _pathInput.SetText(normalized);
        ClearValidationHint();
        return InlineKeyResult.WithResult(DialogResultKind.Ok);
    }

    private void UpdatePathFromHighlight()
    {
        var entry = HighlightedEntry;
        if (entry == null)
            return;

        if (!_save || !entry.Value.IsDirectory)
            _pathInput.SetText(entry.Value.Path);
    }

    private FilePickerEntry? HighlightedEntry
    {
        get
        {
            var selected = _entryList.SelectedIndex;
            return selected >= 0 && selected < _entries.Count ? _entries[selected] : null;
        }
    }

    private void BeginLoad(string? highlightPath, string? pathText)
    {
        ClearValidationHint();

        if (!_modalActive)
        {
            ApplyEntries(ReadEntries(_location), highlightPath, pathText);
            return;
        }

        int generation;
        lock (_loadSync)
        {
            generation = ++_loadGeneration;
            _hasPending = false;
            _pendingEntries = null;
            _pendingHighlightPath = null;
            _pendingPathText = null;
        }

        var location = _location;
        _loading = true;
        _spinner.Start();

        _ = Task.Run(() =>
        {
            var entries = ReadEntries(location);

            lock (_loadSync)
            {
                if (_closed || generation != _loadGeneration)
                    return;

                _pendingEntries = entries;
                _pendingHighlightPath = highlightPath;
                _pendingPathText = pathText;
                _hasPending = true;
            }
        }, _modalToken);
    }

    private void ApplyEntries(
        IReadOnlyList<FilePickerEntry> entries,
        string? highlightPath,
        string? pathText)
    {
        _entries = entries;
        _entryList.SetItems(LabelsForEntries(), SelectedIndexForPath(highlightPath));
        _pathInput.SetText(pathText ?? string.Empty);
        ClearValidationHint();
    }

    private IReadOnlyList<FilePickerEntry> ReadEntries(string? location)
    {
        var entries = new List<FilePickerEntry>();
        try
        {
            entries.AddRange((_browser.GetEntries(location) ?? [])
                .Select(entry => new FilePickerEntry(entry.Label, entry.Path, IsDirectory: true)));
        }
        catch
        {
            // Browser implementations are contractually exception-safe; keep the picker usable if a
            // custom implementation violates that contract.
        }

        if (location != null)
        {
            try
            {
                var pattern = _filter ?? "*";
                entries.AddRange(Directory.EnumerateFiles(location, pattern, SearchOption.TopDirectoryOnly)
                    .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(path => new FilePickerEntry(Path.GetFileName(path), path, IsDirectory: false)));
            }
            catch (Exception ex) when (IsFilesystemException(ex) || ex is ArgumentException)
            {
                // Missing, inaccessible, or invalid-filter listings appear empty.
            }
        }

        return entries;
    }

    private InitialFileState ResolveInitial(string? initialPath)
    {
        var inputPath = string.IsNullOrWhiteSpace(initialPath) ? null : initialPath;
        if (inputPath == null && _save && _defaultFileName != null)
            inputPath = SafeCombine(Environment.CurrentDirectory, _defaultFileName);

        if (inputPath != null)
        {
            try
            {
                if (Directory.Exists(inputPath))
                {
                    var path = _save && _defaultFileName != null
                        ? SafeCombine(inputPath, _defaultFileName)
                        : inputPath;
                    return new InitialFileState(inputPath, null, path);
                }

                var fullPath = Path.GetFullPath(inputPath);
                var parent = Path.GetDirectoryName(fullPath);
                if (parent != null && Directory.Exists(parent))
                    return new InitialFileState(parent, File.Exists(fullPath) ? fullPath : null, fullPath);
            }
            catch (Exception ex) when (IsFilesystemException(ex) || ex is ArgumentException)
            {
                // Fall through to the browser's closest-readable resolution.
            }

            var (location, highlight) = _browser.ResolveInitial(inputPath);
            var target = highlight ?? location;
            if (target != null && Directory.Exists(target))
                return new InitialFileState(target, null, inputPath);
            return new InitialFileState(location, highlight, inputPath);
        }

        var current = Environment.CurrentDirectory;
        return Directory.Exists(current)
            ? new InitialFileState(current, null, null)
            : new InitialFileState(_browser.RootLocation, null, null);
    }

    private bool TryValidatePath(string? path, out string normalized)
    {
        normalized = NormalizeAcceptedPath(path) ?? string.Empty;
        if (normalized.Length == 0)
            return false;

        try
        {
            if (!_save)
                return File.Exists(normalized);

            var fullPath = Path.GetFullPath(normalized);
            var parent = Path.GetDirectoryName(fullPath);
            if (parent == null || !Directory.Exists(parent) || Directory.Exists(fullPath))
                return false;
            normalized = fullPath;
            return true;
        }
        catch (Exception ex) when (IsFilesystemException(ex) || ex is ArgumentException)
        {
            return false;
        }
    }

    private string? NormalizeAcceptedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (!_save || _defaultExtension == null || Path.HasExtension(path) || path.EndsWith('.'))
            return path;
        return path + _defaultExtension;
    }

    private string? CurrentSaveFileName()
    {
        try
        {
            var text = _pathInput.Text;
            if (!string.IsNullOrWhiteSpace(text) && !Directory.Exists(text))
            {
                var name = Path.GetFileName(text);
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
        }
        catch (Exception ex) when (IsFilesystemException(ex) || ex is ArgumentException)
        {
        }
        return _defaultFileName;
    }

    private int? SelectedIndexForPath(string? path)
    {
        if (path == null || _entries.Count == 0)
            return null;
        for (var i = 0; i < _entries.Count; i++)
            if (string.Equals(_entries[i].Path, path, StringComparison.OrdinalIgnoreCase))
                return i;
        return null;
    }

    private IReadOnlyList<string?> LabelsForEntries() => _entries
        .Select(entry => $"{(entry.IsDirectory ? FolderMarker : FileMarker)}{entry.Label}")
        .Cast<string?>()
        .ToArray();

    private static string? NormalizeDefaultExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;
        return extension.StartsWith('.') ? extension : "." + extension;
    }

    private static string SafeCombine(string? directory, string fileName)
    {
        if (string.IsNullOrEmpty(directory))
            return fileName;
        try
        {
            return Path.Combine(directory, fileName);
        }
        catch (ArgumentException)
        {
            return fileName;
        }
    }

    private static string? SavePathForLocation(string? location, string? fileName)
    {
        if (fileName != null)
            return SafeCombine(location, fileName);
        if (string.IsNullOrEmpty(location))
            return null;
        return location.EndsWith(Path.DirectorySeparatorChar)
            || location.EndsWith(Path.AltDirectorySeparatorChar)
            ? location
            : location + Path.DirectorySeparatorChar;
    }

    private static bool IsFilesystemException(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or NotSupportedException;

    private void ClearValidationHint() => _validationHint = null;

    private readonly record struct FilePickerEntry(string Label, string Path, bool IsDirectory);

    private readonly record struct InitialFileState(string? Location, string? HighlightPath, string? InputPath);
}
