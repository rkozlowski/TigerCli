using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class DefaultSettings : TigerCliSettings
{
    [TigerCliOption("-n|--name",
        Description = "Name to greet.",
        DescriptionResourceKey = "Opt_Default_Name_Description")]
    public string? Name { get; set; }

    [TigerCliOption("--city",
        Description = "City to greet when no name is given.",
        DescriptionResourceKey = "Opt_Default_City_Description")]
    public GreetingCity? City { get; set; }
}

// Non-ASCII member names exercise Unicode values end to end: parsing, output, and shell completion.
public enum GreetingCity
{
    Kraków,
    Łódź,
    Gdańsk
}

public sealed class DefaultCommand
    : TigerCliAsyncCommandHandler<DefaultSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(DefaultSettings settings)
    {
        var name = !string.IsNullOrEmpty(settings.Name) ? settings.Name
            : settings.City is { } city ? city.ToString()
            : settings.T("World");
        TigerConsole.MarkupLine(settings.E("Hello, [White]{0}[/]!", name));
        return Task.FromResult(ParserTestExitCode.Ok);
    }
}
