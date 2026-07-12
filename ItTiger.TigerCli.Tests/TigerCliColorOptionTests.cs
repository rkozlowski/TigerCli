using System;
using System.IO;
using System.Threading.Tasks;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers the framework <c>--color</c> / <c>--no-color</c> option: it sets
/// <see cref="TigerConsole.ColorMode"/>, the CLI wins over the builder default, recognized modes are
/// stripped from the command's arguments, and unrecognized <c>--color</c> values pass through to the
/// application (so an app may keep its own <c>--color</c> option).
/// </summary>
public sealed class TigerCliColorOptionTests
{
    private sealed class NameSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class NameCommand : TigerCliAsyncCommandHandler<NameSettings>
    {
        public override Task<int> ExecuteAsync(NameSettings settings) => Task.FromResult(0);
    }

    private static TigerCliApp App(CliColorMode? defaultMode = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("color-test")
            .SetDefaultCommand<NameCommand>();
        if (defaultMode is { } mode)
            builder = builder.SetColorMode(mode);
        return builder.Build();
    }

    private static async Task<(int Exit, CliColorMode Mode)> RunAsync(TigerCliApp app, params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalMode = TigerConsole.ColorMode;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exit = await app.RunAsync(args);
            return (exit, TigerConsole.ColorMode); // read mode before the finally restores it
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            TigerConsole.ColorMode = originalMode;
        }
    }

    [Theory]
    [InlineData("auto", CliColorMode.Auto)]
    [InlineData("never", CliColorMode.Never)]
    [InlineData("16", CliColorMode.Standard16)]
    [InlineData("256", CliColorMode.Ansi256)]
    public async Task ColorOption_SetsColorMode_AndCommandStillRuns(string value, CliColorMode expected)
    {
        var (exit, mode) = await RunAsync(App(), "--name", "x", "--color", value);

        Assert.Equal(0, exit); // --color was stripped; required --name still satisfied
        Assert.Equal(expected, mode);
    }

    [Fact]
    public async Task ColorOption_EqualsForm_SetsColorMode()
    {
        var (exit, mode) = await RunAsync(App(), "--name", "x", "--color=256");

        Assert.Equal(0, exit);
        Assert.Equal(CliColorMode.Ansi256, mode);
    }

    [Fact]
    public async Task NoColor_SetsNever_AndIsStripped()
    {
        var (exit, mode) = await RunAsync(App(), "--name", "x", "--no-color");

        Assert.Equal(0, exit);
        Assert.Equal(CliColorMode.Never, mode);
    }

    [Fact]
    public async Task BuilderDefault_Applies_WhenNoCliOption()
    {
        var (exit, mode) = await RunAsync(App(CliColorMode.Standard16), "--name", "x");

        Assert.Equal(0, exit);
        Assert.Equal(CliColorMode.Standard16, mode);
    }

    [Fact]
    public async Task CliOption_OverridesBuilderDefault()
    {
        var (exit, mode) = await RunAsync(App(CliColorMode.Standard16), "--name", "x", "--color", "256");

        Assert.Equal(0, exit);
        Assert.Equal(CliColorMode.Ansi256, mode);
    }

    [Fact]
    public async Task UnrecognizedColorValue_IsNotClaimed_AndPassesThroughToApp()
    {
        // "bogus" is not a recognized mode, so the framework does not claim or strip --color; the
        // app (which has no --color option here) then reports it as an unknown option.
        var (exit, mode) = await RunAsync(App(), "--name", "x", "--color", "bogus");

        Assert.NotEqual(0, exit);
        Assert.Equal(CliColorMode.Auto, mode); // builder default unchanged
    }
}
