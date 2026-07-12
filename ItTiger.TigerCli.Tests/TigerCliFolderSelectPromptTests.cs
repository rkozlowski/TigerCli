using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliFolderSelectPromptTests
{
    // ── Deterministic in-memory filesystem (Unix-style to keep paths backslash-free) ──
    private sealed class FakeFolderBrowser : IFolderBrowser
    {
        private readonly Dictionary<string, List<string>> _tree = new(StringComparer.Ordinal)
        {
            ["/"] = new() { "/projects", "/tmp" },
            ["/projects"] = new() { "/projects/app", "/projects/lib" },
            ["/projects/app"] = new(),
            ["/projects/lib"] = new(),
            ["/tmp"] = new(),
        };
        private readonly Dictionary<string, string?> _parentOf = new(StringComparer.Ordinal);

        public FakeFolderBrowser()
        {
            foreach (var (location, children) in _tree)
                foreach (var child in children)
                    _parentOf[child] = location;
        }

        public string? RootLocation => "/";

        public IReadOnlyList<FolderEntry> GetEntries(string? location)
        {
            var key = location ?? "/";
            if (!_tree.TryGetValue(key, out var children))
                return Array.Empty<FolderEntry>();

            return children
                .Select(p => new FolderEntry(p[(p.LastIndexOf('/') + 1)..], p, _tree.TryGetValue(p, out var c) && c.Count > 0))
                .ToList();
        }

        public bool TryGetParent(string? location, out string? parent)
        {
            parent = null;
            if (location is null || location == "/")
                return false;
            if (_parentOf.TryGetValue(location, out var p))
            {
                parent = p;
                return true;
            }
            return false;
        }

        public (string? location, string? highlightPath) ResolveInitial(string? initialPath)
        {
            if (string.IsNullOrWhiteSpace(initialPath))
                return (RootLocation, null);
            if (_parentOf.TryGetValue(initialPath, out var parent))
                return (parent, initialPath);
            if (_tree.ContainsKey(initialPath))
                return TryGetParent(initialPath, out var p) ? (p, initialPath) : (initialPath, null);
            return (RootLocation, null);
        }
    }

    // ── Settings / commands ──
    private sealed class FolderSettings : TigerCliSettings
    {
        [TigerCliOption("-d|--destination", Required = true, Description = "Destination folder.")]
        [TigerCliFolderSelect]
        public string? DestinationFolder { get; set; }
    }

    private sealed class FolderCommand : TigerCliAsyncCommandHandler<FolderSettings>
    {
        public override Task<int> ExecuteAsync(FolderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.DestinationFolder ?? "<null>"));
            return Task.FromResult(0);
        }
    }

    private sealed class DefaultSeededFolderSettings : TigerCliSettings
    {
        [TigerCliOption("-d|--destination", Promptable = TigerCliPromptable.Normal, Description = "Destination folder.")]
        [TigerCliFolderSelect]
        public string? DestinationFolder { get; set; } = "/projects/lib";
    }

    private sealed class DefaultSeededFolderCommand : TigerCliAsyncCommandHandler<DefaultSeededFolderSettings>
    {
        public override Task<int> ExecuteAsync(DefaultSeededFolderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.DestinationFolder ?? "<null>"));
            return Task.FromResult(0);
        }
    }

    private sealed class BadTypeSettings : TigerCliSettings
    {
        [TigerCliOption("-d|--destination", Required = true, Description = "Destination folder.")]
        [TigerCliFolderSelect]
        public int DestinationFolder { get; set; }
    }

    private sealed class BadTypeCommand : TigerCliAsyncCommandHandler<BadTypeSettings>
    {
        public override Task<int> ExecuteAsync(BadTypeSettings settings) => Task.FromResult(0);
    }

    // ── Tests ──

    [Fact]
    public async Task MissingValue_InvokesFolderPicker_AndAssignsHighlightedPath()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Tab);   // list -> buttons
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // OK confirms initial highlight ("/projects")
        var app = App<FolderCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("/projects", result.Stdout);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task MissingValue_OpenThenSelect_UsesFolderNavigation()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow); // open "/projects"
        shell.Terminal.EnqueueKey(ConsoleKey.Tab);        // list -> buttons
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);      // OK confirms "/projects/app"
        var app = App<FolderCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        // Reaching a child path is only possible by opening the folder — proving the picker
        // (not a text prompt) handled the missing value.
        Assert.Contains("/projects/app", result.Stdout);
    }

    [Fact]
    public async Task CommandLineValue_BypassesFolderPicker()
    {
        var shell = new TestShell();
        var app = App<FolderCommand>();

        var result = await RunCapturedAsync(app, ["-d", "/explicit/path"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("/explicit/path", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task DefaultValue_SeedsInitialFolder()
    {
        var shell = new TestShell();
        // The default "/projects/lib" makes the picker start in "/projects" with "lib" (index 1)
        // highlighted. Confirming without moving proves the default seeded the initial folder;
        // an unseeded picker would highlight "/projects/app" (index 0) instead.
        shell.Terminal.EnqueueKey(ConsoleKey.Tab);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<DefaultSeededFolderCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("/projects/lib", result.Stdout);
    }

    [Fact]
    public async Task NonInteractive_DoesNotInvokeFolderPicker()
    {
        var shell = new TestShell();
        var app = App<FolderCommand>(builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --destination", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptCancel_MapsToCancelledKind()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var app = App<FolderCommand>(builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 46));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
    }

    [Fact]
    public async Task UnsupportedPropertyType_IsRejectedClearly()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("folder-test")
            .SetDefaultCommand<BadTypeCommand>()
            .UseFolderBrowser(new FakeFolderBrowser())
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app.RunAsync(["-d", "5"], new TestShell(), ct: TestContext.Current.CancellationToken));

        Assert.Contains("[TigerCliFolderSelect]", ex.Message);
        Assert.Contains("string", ex.Message);
    }

    // ── Helpers ──
    private static TigerCliApp App<TCommand>(Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("folder-test")
            .SetDefaultCommand<TCommand>()
            .UseFolderBrowser(new FakeFolderBrowser());

        configure?.Invoke(builder);
        return builder.Build();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        TestShell shell,
        TimeSpan? promptTimeout = null)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args, shell, promptTimeout, TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
