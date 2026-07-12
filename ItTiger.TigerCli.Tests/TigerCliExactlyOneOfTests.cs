using ItTiger.TigerCli.Commands;

namespace ItTiger.TigerCli.Tests;

// ── Test settings types ──────────────────────────────────────────────

[TigerCliExactlyOneOf(nameof(FilePath), nameof(Query))]
public sealed class ExactlyOneOfSettings : TigerCliSettings
{
    [TigerCliOption("-f|--file", Description = "Path to the SQL script file.")]
    public string? FilePath { get; set; }

    [TigerCliOption("-q|--query", ValueName = "sql", Description = "Inline SQL query.")]
    public string? Query { get; set; }

    [TigerCliOption("-c|--connection", Description = "Connection string.")]
    public string ConnectionString { get; set; } = "default-conn";
}

[TigerCliExactlyOneOf(nameof(A), nameof(B), nameof(C))]
public sealed class ExactlyOneOfThreeSettings : TigerCliSettings
{
    [TigerCliOption("--alpha")]
    public string? A { get; set; }

    [TigerCliOption("--beta")]
    public string? B { get; set; }

    [TigerCliOption("--gamma")]
    public string? C { get; set; }
}

[TigerCliExactlyOneOf(nameof(FilePath), nameof(Query), Description = "Provide either a file or a query, but not both.")]
public sealed class ExactlyOneOfCustomDescSettings : TigerCliSettings
{
    [TigerCliOption("-f|--file", Description = "Path to the SQL script file.")]
    public string? FilePath { get; set; }

    [TigerCliOption("-q|--query", Description = "Inline SQL query.")]
    public string? Query { get; set; }
}

[TigerCliExactlyOneOf(nameof(X), nameof(Y))]
[TigerCliExactlyOneOf(nameof(A), nameof(B))]
public sealed class MultipleGroupsSettings : TigerCliSettings
{
    [TigerCliOption("--opt-x")]
    public string? X { get; set; }

    [TigerCliOption("--opt-y")]
    public string? Y { get; set; }

    [TigerCliOption("--opt-a")]
    public string? A { get; set; }

    [TigerCliOption("--opt-b")]
    public string? B { get; set; }
}

// ── Command handlers ─────────────────────────────────────────────────

public sealed class ExactlyOneOfCommand : TigerCliAsyncCommandHandler<ExactlyOneOfSettings>
{
    public override Task<int> ExecuteAsync(ExactlyOneOfSettings settings) => Task.FromResult(0);
}

public sealed class ExactlyOneOfThreeCommand : TigerCliAsyncCommandHandler<ExactlyOneOfThreeSettings>
{
    public override Task<int> ExecuteAsync(ExactlyOneOfThreeSettings settings) => Task.FromResult(0);
}

public sealed class ExactlyOneOfCustomDescCommand : TigerCliAsyncCommandHandler<ExactlyOneOfCustomDescSettings>
{
    public override Task<int> ExecuteAsync(ExactlyOneOfCustomDescSettings settings) => Task.FromResult(0);
}

public sealed class MultipleGroupsCommand : TigerCliAsyncCommandHandler<MultipleGroupsSettings>
{
    public override Task<int> ExecuteAsync(MultipleGroupsSettings settings) => Task.FromResult(0);
}

// ── Tests ────────────────────────────────────────────────────────────

public class TigerCliExactlyOneOfTests
{
    // ── Validation: exactly one provided → success ──

    [Fact]
    public async Task ExactlyOneOf_FirstProvided_Succeeds()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfCommand>()
            .Build();

        var result = await app.RunAsync(["--file", "script.sql"]);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExactlyOneOf_SecondProvided_Succeeds()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfCommand>()
            .Build();

        var result = await app.RunAsync(["--query", "SELECT 1"]);
        Assert.Equal(0, result);
    }

    // ── Validation: none provided → error ──

    [Fact]
    public async Task ExactlyOneOf_NoneProvided_ReturnsError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfCommand>()
            .Build();

        var stderr = new StringWriter();
        Console.SetError(stderr);

        var result = await app.RunAsync([]);

        Assert.Equal(-1, result);
        Assert.Contains("Exactly one of --file or --query must be specified.", stderr.ToString());
    }

    // ── Validation: both provided → error ──

    [Fact]
    public async Task ExactlyOneOf_BothProvided_ReturnsError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfCommand>()
            .Build();

        var stderr = new StringWriter();
        Console.SetError(stderr);

        var result = await app.RunAsync(["--file", "script.sql", "--query", "SELECT 1"]);

        Assert.Equal(-1, result);
        Assert.Contains("Exactly one of --file or --query must be specified.", stderr.ToString());
    }

    // ── Validation: three options, exactly one provided → success ──

    [Fact]
    public async Task ExactlyOneOf_ThreeOptions_OneProvided_Succeeds()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfThreeCommand>()
            .Build();

        var result = await app.RunAsync(["--beta", "value"]);
        Assert.Equal(0, result);
    }

    // ── Validation: three options, none provided → error with correct message ──

    [Fact]
    public async Task ExactlyOneOf_ThreeOptions_NoneProvided_FormatsMessageCorrectly()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfThreeCommand>()
            .Build();

        var stderr = new StringWriter();
        Console.SetError(stderr);

        var result = await app.RunAsync([]);

        Assert.Equal(-1, result);
        Assert.Contains("Exactly one of --alpha, --beta or --gamma must be specified.", stderr.ToString());
    }

    // ── Validation: custom description ──

    [Fact]
    public async Task ExactlyOneOf_CustomDescription_UsedInError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfCustomDescCommand>()
            .Build();

        var stderr = new StringWriter();
        Console.SetError(stderr);

        var result = await app.RunAsync([]);

        Assert.Equal(-1, result);
        Assert.Contains("Provide either a file or a query, but not both.", stderr.ToString());
    }

    // ── Validation: multiple groups ──

    [Fact]
    public async Task MultipleGroups_BothSatisfied_Succeeds()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<MultipleGroupsCommand>()
            .Build();

        var result = await app.RunAsync(["--opt-x", "1", "--opt-a", "2"]);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task MultipleGroups_FirstGroupViolated_ReturnsError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<MultipleGroupsCommand>()
            .Build();

        var stderr = new StringWriter();
        Console.SetError(stderr);

        var result = await app.RunAsync(["--opt-a", "2"]);

        Assert.Equal(-1, result);
        Assert.Contains("Exactly one of --opt-x or --opt-y must be specified.", stderr.ToString());
    }

    // ── Presence tracking: option not provided retains default, still counts as absent ──

    [Fact]
    public async Task ExactlyOneOf_DefaultValueNotTreatedAsProvided()
    {
        // ConnectionString has a default value but is not in the ExactlyOneOf group.
        // --file and --query both null by default — neither is "provided".
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfCommand>()
            .Build();

        var stderr = new StringWriter();
        Console.SetError(stderr);

        // Only provide --connection (not in the group). Neither --file nor --query is on the command line.
        var result = await app.RunAsync(["--connection", "Server=."]);

        Assert.Equal(-1, result);
        Assert.Contains("Exactly one of --file or --query must be specified.", stderr.ToString());
    }

    // ── Attribute: fewer than 2 properties → throws ──

    [Fact]
    public void Attribute_FewerThanTwo_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TigerCliExactlyOneOfAttribute("OnlyOne"));
    }

    // ── Help: notes section includes ExactlyOneOf message ──

    [Fact]
    public async Task Help_IncludesExactlyOneOfNote()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfCommand>()
            .Build();

        var stdout = new StringWriter();
        Console.SetOut(stdout);

        var result = await app.RunAsync(["--help"]);

        Assert.Equal(0, result);
        var output = stdout.ToString();
        Assert.Contains("Notes:", output);
        Assert.Contains("Exactly one of --file or --query must be specified.", output);
    }

    [Fact]
    public async Task Help_CustomDescription_ShownInNotes()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfCustomDescCommand>()
            .Build();

        var stdout = new StringWriter();
        Console.SetOut(stdout);

        var result = await app.RunAsync(["--help"]);

        Assert.Equal(0, result);
        var output = stdout.ToString();
        Assert.Contains("Notes:", output);
        Assert.Contains("Provide either a file or a query, but not both.", output);
    }

    [Fact]
    public async Task Help_ThreeOptions_ShowsCorrectNote()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetDefaultCommand<ExactlyOneOfThreeCommand>()
            .Build();

        var stdout = new StringWriter();
        Console.SetOut(stdout);

        var result = await app.RunAsync(["--help"]);

        Assert.Equal(0, result);
        var output = stdout.ToString();
        Assert.Contains("Exactly one of --alpha, --beta or --gamma must be specified.", output);
    }
}
