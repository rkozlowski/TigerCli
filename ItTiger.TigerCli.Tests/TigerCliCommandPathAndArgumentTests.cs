using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliCommandPathAndArgumentTests
{
    private sealed class ProjectsSpAddSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "connection", Description = "Connection name.")]
        public string ConnectionName { get; set; } = string.Empty;

        [TigerCliArgument(1, Name = "project", Description = "Project name.")]
        public string ProjectName { get; set; } = string.Empty;

        [TigerCliOption("--schema", Description = "Schema name.")]
        public string Schema { get; set; } = "dbo";

        [TigerCliOption("--language", Description = "Language name.")]
        public string Language { get; set; } = "csharp";
    }

    private sealed class DefaultArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "name", Description = "Name to greet.")]
        public string Name { get; set; } = string.Empty;

        [TigerCliOption("--upper")]
        public bool Upper { get; set; }
    }

    private sealed class OptionOnlySettings : TigerCliSettings
    {
        [TigerCliOption("-m|--message", Required = true)]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class ProjectsSpAddCommand : TigerCliAsyncCommandHandler<ProjectsSpAddSettings>
    {
        public override Task<int> ExecuteAsync(ProjectsSpAddSettings settings)
        {
            TigerConsole.MarkupLine(
                $"sp-add {CliMarkupParser.Escape(settings.ConnectionName)} {CliMarkupParser.Escape(settings.ProjectName)} {CliMarkupParser.Escape(settings.Schema)} {CliMarkupParser.Escape(settings.Language)}");
            return Task.FromResult(0);
        }
    }

    private sealed class DefaultArgumentCommand : TigerCliAsyncCommandHandler<DefaultArgumentSettings>
    {
        public override Task<int> ExecuteAsync(DefaultArgumentSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(
                settings.Upper ? settings.Name.ToUpperInvariant() : settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class OptionOnlyCommand : TigerCliAsyncCommandHandler<OptionOnlySettings>
    {
        public override Task<int> ExecuteAsync(OptionOnlySettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Message));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task MultiTokenCommandPath_ResolvesAndBindsPositionals()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(app, ["projects", "sp-add", "local", "MyProject", "--schema", "sales"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sp-add local MyProject sales csharp", result.Stdout);
    }

    [Fact]
    public async Task MissingPositionalArgument_UsesMissingRequiredArgumentPolicy()
    {
        var app = CreateProjectsApp(builder => builder.UseExitCodes(0, -9)
            .ExitKind(TigerCliExitKind.MissingRequiredArgument, 77));

        var result = await RunCapturedAsync(app, ["projects", "sp-add", "local", "--non-interactive"]);

        Assert.Equal(77, result.ExitCode);
        Assert.Contains("Missing required argument: <project>", result.Stderr);
    }

    [Fact]
    public async Task PositionalArgumentAfterOptions_IsParseError()
    {
        var app = CreateProjectsApp(builder => builder.UseExitCodes(0, -9)
            .ExitKind(TigerCliExitKind.InvalidArguments, 64));

        var result = await RunCapturedAsync(app, ["projects", "sp-add", "local", "--schema", "dbo", "MyProject"]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("Unexpected positional argument after options: MyProject", result.Stderr);
    }

    [Fact]
    public async Task OptionsAfterPositionals_AreUnorderedRelativeToOtherOptions()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(
            app,
            ["projects", "sp-add", "local", "MyProject", "--language", "fsharp", "--schema", "sales"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sp-add local MyProject sales fsharp", result.Stdout);
    }

    [Fact]
    public async Task Help_IncludesPositionalArgumentsInUsage()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(app, ["projects", "sp-add", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("parser-test projects sp-add <connection> <project> [options]", result.Stdout);
    }

    [Fact]
    public async Task Help_RendersArgumentsSectionBeforeOptions()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(app, ["projects", "sp-add", "--help"]);

        Assert.Contains("Arguments:", result.Stdout);
        Assert.Contains("<connection>", result.Stdout);
        Assert.Contains("Connection name.", result.Stdout);
        Assert.True(result.Stdout.IndexOf("Arguments:", StringComparison.Ordinal) <
            result.Stdout.IndexOf("Options:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DefaultCommand_WithPositionalArguments_IsSupported()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .SetDefaultCommand<DefaultArgumentCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["riley", "--upper"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("RILEY", result.Stdout);
    }

    [Fact]
    public async Task ExistingOptionOnlyCommand_RemainsUnchanged()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .AddCommand<OptionOnlyCommand>("echo")
            .Build();

        var result = await RunCapturedAsync(app, ["echo", "--message", "hello"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Stdout);
    }

    private static TigerCliApp CreateProjectsApp(Action<TigerCliAppBuilder>? configure = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .AddCommandGroup("projects", group => group.AddCommand<ProjectsSpAddCommand>("sp-add"));

        configure?.Invoke(builder);
        return builder.Build();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
