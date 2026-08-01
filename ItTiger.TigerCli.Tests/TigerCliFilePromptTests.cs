using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliFilePromptTests
{
    private static string? interactiveOpenPath;

    private sealed class OpenSettings : TigerCliSettings
    {
        [TigerCliOption("--input")]
        [TigerCliFileOpen]
        public string InputPath { get; set; } = string.Empty;
    }

    private sealed class OpenCommand : TigerCliAsyncCommandHandler<OpenSettings>
    {
        public override Task<int> ExecuteAsync(OpenSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.InputPath));
            return Task.FromResult(0);
        }
    }

    private sealed class InteractiveOpenSettings : TigerCliSettings
    {
        [TigerCliOption("--input")]
        [TigerCliFileOpen(Filter = "*.json")]
        public string InputPath { get; set; } = interactiveOpenPath ?? string.Empty;
    }

    private sealed class InteractiveOpenCommand : TigerCliAsyncCommandHandler<InteractiveOpenSettings>
    {
        public override Task<int> ExecuteAsync(InteractiveOpenSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.InputPath));
            return Task.FromResult(0);
        }
    }

    private sealed class SaveDenySettings : TigerCliSettings
    {
        [TigerCliOption("--output")]
        [TigerCliFileSave(DefaultExtension = ".json", Overwrite = TigerCliFileOverwrite.Deny, OverwriteWhenOption = nameof(Force))]
        public string OutputPath { get; set; } = string.Empty;

        [TigerCliOption("--force")]
        public bool Force { get; set; }
    }

    private sealed class SaveDenyCommand : TigerCliAsyncCommandHandler<SaveDenySettings>
    {
        public override Task<int> ExecuteAsync(SaveDenySettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.OutputPath));
            return Task.FromResult(0);
        }
    }

    private sealed class SavePromptSettings : TigerCliSettings
    {
        [TigerCliOption("--output")]
        [TigerCliFileSave(Overwrite = TigerCliFileOverwrite.Prompt, OverwriteWhenOption = nameof(Force))]
        public string OutputPath { get; set; } = string.Empty;

        [TigerCliOption("--force")]
        public bool Force { get; set; }
    }

    private sealed class SavePromptCommand : TigerCliAsyncCommandHandler<SavePromptSettings>
    {
        public override Task<int> ExecuteAsync(SavePromptSettings settings) => Task.FromResult(0);
    }

    private sealed class SaveAllowSettings : TigerCliSettings
    {
        [TigerCliOption("--output")]
        [TigerCliFileSave(Overwrite = TigerCliFileOverwrite.Allow, OverwriteWhenOption = nameof(Force))]
        public string OutputPath { get; set; } = string.Empty;

        [TigerCliOption("--force")]
        public bool Force { get; set; }
    }

    private sealed class SaveAllowCommand : TigerCliAsyncCommandHandler<SaveAllowSettings>
    {
        public override Task<int> ExecuteAsync(SaveAllowSettings settings) => Task.FromResult(0);
    }

    private sealed class SaveNullableSettings : TigerCliSettings
    {
        [TigerCliOption("--output")]
        [TigerCliFileSave(Overwrite = TigerCliFileOverwrite.Prompt, OverwriteWhenOption = nameof(Force))]
        public string OutputPath { get; set; } = string.Empty;

        [TigerCliOption("--force")]
        public bool? Force { get; set; }
    }

    private sealed class SaveNullableCommand : TigerCliAsyncCommandHandler<SaveNullableSettings>
    {
        public override Task<int> ExecuteAsync(SaveNullableSettings settings) => Task.FromResult(0);
    }

    private sealed class BadOverwriteNameSettings : TigerCliSettings
    {
        [TigerCliOption("--output")]
        [TigerCliFileSave(OverwriteWhenOption = "Missing")]
        public string OutputPath { get; set; } = string.Empty;
    }

    private sealed class BadOverwriteNameCommand : TigerCliAsyncCommandHandler<BadOverwriteNameSettings>
    {
        public override Task<int> ExecuteAsync(BadOverwriteNameSettings settings) => Task.FromResult(0);
    }

    private sealed class BadOverwriteTypeSettings : TigerCliSettings
    {
        [TigerCliOption("--output")]
        [TigerCliFileSave(OverwriteWhenOption = nameof(Force))]
        public string OutputPath { get; set; } = string.Empty;

        [TigerCliOption("--force")]
        public string Force { get; set; } = string.Empty;
    }

    private sealed class BadOverwriteTypeCommand : TigerCliAsyncCommandHandler<BadOverwriteTypeSettings>
    {
        public override Task<int> ExecuteAsync(BadOverwriteTypeSettings settings) => Task.FromResult(0);
    }

    [Fact]
    public async Task FileOpen_InteractiveFilteredSelection_ReturnsExistingFile()
    {
        using var temp = new TempDirectory();
        var json = temp.File("project.json");
        _ = temp.File("notes.txt");
        interactiveOpenPath = json;
        try
        {
            var shell = new TestShell();
            shell.Terminal.EnqueueKey(ConsoleKey.Enter);

            var result = await RunCapturedAsync(App<InteractiveOpenCommand>(), [], shell);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(json, result.Stdout);
            Assert.DoesNotContain("notes.txt", shell.Terminal.LastRenderedText);
        }
        finally
        {
            interactiveOpenPath = null;
        }
    }

    [Fact]
    public async Task FileOpen_TypedFullPath_ReturnsExistingFile()
    {
        using var temp = new TempDirectory();
        var path = temp.File("typed.json");
        var shell = new TestShell();
        shell.Terminal.EnqueueKeys(ConsoleKey.Tab, ConsoleKey.Tab); // list -> buttons -> path
        foreach (var character in path)
            shell.Terminal.EnqueueKey(new ConsoleKeyInfo(character, ConsoleKey.A, false, false, false));
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunCapturedAsync(App<OpenCommand>(), [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(path, result.Stdout);
    }

    [Fact]
    public async Task FileOpen_NonInteractiveExistingFile_Succeeds()
    {
        using var temp = new TempDirectory();
        var path = temp.File("input.json");

        var result = await RunCapturedAsync(App<OpenCommand>(), ["--non-interactive", "--input", path]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(path, result.Stdout);
    }

    [Fact]
    public async Task FileOpen_NonInteractiveMissingFile_Fails()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "missing.json");

        var result = await RunCapturedAsync(App<OpenCommand>(), ["--non-interactive", "--input", path]);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("existing file", result.Stderr);
    }

    [Fact]
    public async Task FileOpen_DirectoryPath_IsRejected()
    {
        using var temp = new TempDirectory();

        var result = await RunCapturedAsync(App<OpenCommand>(), ["--non-interactive", "--input", temp.Path]);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("existing file", result.Stderr);
    }

    [Fact]
    public async Task FileSave_NewPathWithExistingParent_SucceedsAndAddsDefaultExtension()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "export");

        var result = await RunCapturedAsync(App<SaveDenyCommand>(), ["--non-interactive", "--output", path]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Path.GetFullPath(path + ".json"), result.Stdout);
    }

    [Fact]
    public async Task FileSave_InteractiveTypedFullPath_Succeeds()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "typed-export");
        var shell = new TestShell();
        shell.Terminal.EnqueueKeys(ConsoleKey.Tab, ConsoleKey.Tab); // list -> buttons -> path
        foreach (var character in path)
            shell.Terminal.EnqueueKey(new ConsoleKeyInfo(character, ConsoleKey.A, false, false, false));
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunCapturedAsync(App<SaveDenyCommand>(), [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Path.GetFullPath(path + ".json"), result.Stdout);
    }

    [Fact]
    public void FileSave_DefaultFileName_SeedsSelectedDirectory()
    {
        using var temp = new TempDirectory();
        var control = new InlineFileSelect(
            new TestShell(),
            new FileSystemFolderBrowser(),
            save: true,
            initialPath: temp.Path,
            defaultExtension: ".json",
            defaultFileName: "project.json");

        Assert.True(control.CanConfirm);
        Assert.Equal(Path.Combine(temp.Path, "project.json"), control.Payload);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FilePathOption_NonInteractiveMissingValue_Fails(bool open)
    {
        var app = open ? App<OpenCommand>() : App<SaveDenyCommand>();

        var result = await RunCapturedAsync(app, ["--non-interactive"]);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option", result.Stderr);
    }

    [Fact]
    public async Task FileSave_MissingParent_Fails()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "missing", "export.json");

        var result = await RunCapturedAsync(App<SaveDenyCommand>(), ["--non-interactive", "--output", path]);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("parent folder exists", result.Stderr);
    }

    [Fact]
    public async Task FileSave_DenyExisting_Fails()
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");

        var result = await RunCapturedAsync(App<SaveDenyCommand>(), ["--non-interactive", "--output", path]);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("cannot overwrite", result.Stderr);
    }

    [Fact]
    public async Task FileSave_DenyExistingWithForce_Succeeds()
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");

        var result = await RunCapturedAsync(App<SaveDenyCommand>(), ["--non-interactive", "--output", path, "--force"]);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task FileSave_PromptExistingInteractiveYes_Succeeds()
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");
        var shell = new TestShell();
        shell.Terminal.EnqueueKeys(ConsoleKey.LeftArrow, ConsoleKey.Enter); // preselected No -> Yes

        var result = await RunCapturedAsync(App<SavePromptCommand>(), ["--output", path], shell);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task FileSave_PromptExistingInteractiveNo_Fails()
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // preselected No

        var result = await RunCapturedAsync(App<SavePromptCommand>(), ["--output", path], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Overwrite was declined", result.Stderr);
    }

    [Fact]
    public async Task FileSave_PromptExistingInteractiveEscape_Cancels()
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await RunCapturedAsync(App<SavePromptCommand>(), ["--output", path], shell);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
    }

    [Fact]
    public async Task FileSave_PromptExistingNonInteractiveWithoutOverride_Fails()
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");

        var result = await RunCapturedAsync(App<SavePromptCommand>(), ["--non-interactive", "--output", path]);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("unavailable in non-interactive mode", result.Stderr);
    }

    [Fact]
    public async Task FileSave_PromptExistingNonInteractiveWithOverride_Succeeds()
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");

        var result = await RunCapturedAsync(App<SavePromptCommand>(), ["--non-interactive", "--output", path, "--force"]);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task FileSave_AllowExisting_SucceedsWithoutReadingInput()
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");
        var shell = new TestShell();

        var result = await RunCapturedAsync(App<SaveAllowCommand>(), ["--output", path], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileSave_NullableFalseOrNull_AppliesNormalPromptPolicy(bool supplyFalse)
    {
        using var temp = new TempDirectory();
        var path = temp.File("export.json");
        var args = new List<string> { "--non-interactive", "--output", path };
        if (supplyFalse)
        {
            args.Add("--force");
            args.Add("false");
        }

        var result = await RunCapturedAsync(App<SaveNullableCommand>(), args.ToArray());

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("unavailable in non-interactive mode", result.Stderr);
    }

    [Fact]
    public void FileSave_InvalidOverwritePropertyName_FailsDuringBuild()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => App<BadOverwriteNameCommand>());

        Assert.Contains("OverwriteWhenOption", ex.Message);
        Assert.Contains("Missing", ex.Message);
    }

    [Fact]
    public void FileSave_InvalidOverwritePropertyType_FailsDuringBuild()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => App<BadOverwriteTypeCommand>());

        Assert.Contains("bool or bool?", ex.Message);
        Assert.Contains("Force", ex.Message);
    }

    private static TigerCliApp App<TCommand>() where TCommand : class, new() =>
        TigerCliApp.CreateBuilder()
            .SetApplicationName("file-test")
            .SetDefaultCommand<TCommand>()
            .UseExitCodes(0, -1)
            .ExitKind(TigerCliExitKind.ValidationError, 45)
            .ExitKind(TigerCliExitKind.Cancelled, 46)
            .Build();

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        TestShell? shell = null)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(
                args,
                shell ?? new TestShell(),
                ct: TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"TigerCli.FilePromptTests.{Guid.NewGuid():N}");

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string File(string name)
        {
            var path = System.IO.Path.Combine(Path, name);
            System.IO.File.WriteAllText(path, "test");
            return path;
        }

        public void Dispose()
        {
            var fullPath = System.IO.Path.GetFullPath(Path);
            var tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                || !System.IO.Path.GetFileName(fullPath).StartsWith("TigerCli.FilePromptTests.", StringComparison.Ordinal))
                throw new InvalidOperationException($"Refusing to remove unexpected test path '{fullPath}'.");
            Directory.Delete(fullPath, recursive: true);
        }
    }
}
