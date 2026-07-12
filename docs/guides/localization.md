# TigerCli Localization

TigerCli apps can be English-only, localized, or somewhere in between. The framework localizes the text it owns, and gives app authors small CLI-oriented tools for localizing command metadata, enum text, provider labels, and command output.

## Positioning

TigerCli is not a localization framework and does not replace .NET localization infrastructure.

TigerCli is a locale-aware CLI framework. It uses standard .NET resource mechanisms where they are useful, while providing a simpler command-line authoring model on top of them.

TigerCli localization focuses on:

- framework-owned CLI text
- app-owned command metadata
- app-owned enum labels and descriptions
- culture flow into providers and command handlers
- source-text helpers such as `settings.T(...)`, `settings.F(...)`, and `settings.E(...)`
- `--culture`, supported/default cultures, provider culture, and `settings.Culture`

TigerCli intentionally does not try to solve:

- pluralization engines
- ICU MessageFormat
- translator workflow tooling
- automatic resource extraction
- runtime language-pack management
- arbitrary third-party library localization

## Quick Start

Register the cultures your app supports and the app resource manager TigerCli should use for app-owned text:

```csharp
return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .SetSupportedCultures("en-US", "pl-PL")
    .UseAppResources(AppStrings.ResourceManager)
    .SetDefaultCommand<GreetCommand>()
    .Build();
```

Then users can select a supported culture for the current run:

```bash
my-tool --culture pl-PL --help
my-tool greet Alice --culture pl-PL
```

For simple command output, prefer source-text localization helpers on the settings object:

```csharp
TigerConsole.MarkupLine(settings.E("Hello, [White]{0}[/]!", settings.Name));
```

For metadata and enum labels, prefer `TigerTextAttribute`:

```csharp
using ItTiger.Core;

public enum Mode
{
    [TigerText("Fast")]
    Fast
}
```

## Culture Configuration

By default, a TigerCli app supports only `en-US`. This means English-only apps do not need localization setup, and output will not change just because the user's machine uses another culture.

```csharp
TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .Build();
```

Effective culture setup:

```text
Default culture:    en-US
Supported cultures: en-US
```

`SetDefaultCulture("pl-PL")` makes `pl-PL` the default and implicitly supported:

```csharp
TigerCliApp.CreateBuilder()
    .SetDefaultCulture("pl-PL")
    .Build();
```

`SetSupportedCultures("pl-PL", "en-US")` makes the first supported culture the default when no explicit default has been configured:

```csharp
TigerCliApp.CreateBuilder()
    .SetSupportedCultures("pl-PL", "en-US")
    .Build();
```

Using both methods keeps the explicit default and automatically includes it in the supported set:

```csharp
TigerCliApp.CreateBuilder()
    .SetDefaultCulture("pl-PL")
    .SetSupportedCultures("en-US")
    .Build();
```

Effective culture setup:

```text
Default culture:    pl-PL
Supported cultures: pl-PL, en-US
```

`--culture` is a framework-owned global option. It is not bound to command settings, and commands should not define their own `--culture` option.

```bash
tool --culture en-US --help
tool --culture pl-PL --help
```

If the requested culture is not supported by the app, TigerCli fails through framework error handling. The error is rendered using the app default culture.

Use `DisableCultureOption()` when an app must not expose a command-line culture override:

```csharp
TigerCliApp.CreateBuilder()
    .SetDefaultCulture("en-US")
    .DisableCultureOption()
    .Build();
```

TigerCli does not infer the default culture from `CultureInfo.CurrentUICulture`, and the user override is matched against the configured supported culture names.

## Framework-Owned Text

TigerCli automatically localizes framework-owned text for the active run culture, including:

- help headings such as usage, commands, arguments, options, notes, and exit codes
- usage placeholders such as `<command>`, `[options]`, and generic option value placeholders
- built-in framework option descriptions for `--help`, `--version` / `--version-full` when enabled, `--help-errors`, `--non-interactive`, and `--culture`
- the `version` and `product version` labels in built-in version output
- standard application link labels from `AddWebsite(...)`, `AddRepository(...)`, `AddDocumentation(...)`, and standard links populated by `UseAssemblyMetadata(...)`
- parser, validation, prompt, and unsupported-culture errors
- the framework error prefix, such as `Error:` / `Błąd:`
- prompt built-ins such as Yes/No labels, empty states, and hints
- exit-code help framework headings and hints

TigerCli may ship framework resources for more than one culture, but an application chooses which of those cultures are enabled.

## App-Owned Metadata

TigerCli does not translate application-owned text by itself. It resolves app metadata through the resource manager you register with `UseAppResources(...)` when a resource key is provided.

Resource-backed metadata includes:

- application descriptions from `AddDescription(...)`
- command descriptions from `AddCommand(...)`
- option descriptions from `TigerCliOptionAttribute.DescriptionResourceKey`
- argument descriptions from `TigerCliArgumentAttribute.DescriptionResourceKey`

```csharp
return TigerCliApp.CreateBuilder()
    .SetApplicationName("parser-test")
    .SetSupportedCultures("en-US", "pl-PL")
    .UseAppResources(AppStrings.ResourceManager)
    .AddDescription(
        "[green]TigerCli command parser manual test app.[/]",
        resourceKey: "App_Description")
    .AddCommand<EchoCommand>(
        "echo",
        "Echoes a message.",
        descriptionResourceKey: "Cmd_Echo_Description")
    .Build();
```

```csharp
[TigerCliOption("-n|--name",
    Description = "Name to greet.",
    DescriptionResourceKey = "Opt_Name_Description")]
public string Name { get; set; } = "World";
```

Resolution is intentionally forgiving:

1. If the key resolves to a non-empty value for the active culture, TigerCli uses it.
2. If resources are not configured, the key is missing, or the value is empty, TigerCli uses the fallback string.
3. Raw resource keys are never shown to users.

App-owned help metadata follows the same markup trust model as the fallback text. For details on trusted markup and escaping, see the [help rendering trust model](../reference/help-rendering-trust-model.md).

Assembly metadata read by `UseAssemblyMetadata(...)` is treated as app-owned project metadata. TigerCli does not translate assembly-derived application names, display names, descriptions, versions, product versions, copyright text, or URLs.

TigerCli does not automatically localize command names, command path tokens, option aliases, explicit argument names, display names, copyright text, explicit version strings, custom link labels passed to `AddLink(...)`, exception messages, custom validation messages, provider labels, assembly-provided metadata values, or arbitrary text passed directly to `TigerConsole`. If an app wants those strings localized, the app should supply localized text through its own metadata, provider code, validation code, or command-output helpers.

## Command Output With T/F/E

Command output is app-owned text. TigerCli does not translate arbitrary `TigerConsole.MarkupLine(...)` strings automatically, but every `TigerCliSettings` instance has source-text helpers that use the resolved culture and registered app resources.

Use these helpers for most command output:

- `settings.T("World")` looks up static text. The source text is both the resource key and the fallback.
- `settings.F("Returning raw code {0}", code)` looks up a format string and formats the arguments without escaping them.
- `settings.E("Hello, [White]{0}[/]!", name)` looks up a format string and escapes formatted arguments for TigerConsole markup output.

Use `E(...)` when producing localized markup output from settings and inserting user-provided or dynamic values into `TigerConsole.MarkupLine(...)` or `TigerConsole.MarkupErrorLine(...)`. Use `CliMarkupParser.Escape(...)` directly when no localization lookup is needed, or when escaping individual values before inserting them into trusted markup.

Before:

```csharp
var text = CommandParserTestStrings.Get("Greeting_Format", settings.Culture);
TigerConsole.MarkupLine(string.Format(text, CliMarkupParser.Escape(name)));
```

After:

```csharp
TigerConsole.MarkupLine(settings.E("Hello, [White]{0}[/]!", name));
```

The simple helper is easier to read, keeps the English source text next to the command behavior, and still lets `AppStrings.resx` provide a localized value for the active culture.

## Explicit-Key Helpers

Use explicit resource keys when a source-text key is not the right fit:

- long or heavily reused strings
- stable translation identity across English wording changes
- metadata-like strings shared by help and command output
- teams that prefer identifier-style resource keys

The explicit-key helpers are:

- `settings.TextByKey(resourceKey, fallback)` for static text
- `settings.FormatTextByKey(resourceKey, fallbackFormat, args...)` for formatted plain text
- `settings.EscapedFormatTextByKey(resourceKey, fallbackFormat, args...)` for localized markup output with escaped formatted arguments

```csharp
TigerConsole.MarkupErrorLine(settings.EscapedFormatTextByKey(
    "Errors_ProjectNotFound",
    "Project [White]{0}[/] was not found.",
    projectName));
```

For short command output, prefer `T(...)`, `F(...)`, and `E(...)`. Reach for explicit keys when they make the translation identity clearer.

## TigerTextAttribute

`TigerTextAttribute` from `ItTiger.Core` is the preferred Tiger style for app-owned enum text and other metadata that TigerCli resolves.

The simple form uses source text:

```csharp
using ItTiger.Core;

[TigerText("Prompt smoke modes")]
public enum PromptSmokeMode
{
    [TigerText("Fast")]
    Fast,

    [TigerText("Invalid arguments",
        Description = "Invalid command-line arguments.")]
    InvalidArguments
}
```

When `ResourceKey` is omitted, `Text` is used as a source-text resource key and fallback. When `DescriptionResourceKey` is omitted, `Description` is used as a source-text resource key and fallback.

Explicit keys are available when you need stable identifier-style keys:

```csharp
[TigerText(
    "Invalid arguments",
    ResourceKey = "Exit_InvalidArguments_Label",
    Description = "Invalid command-line arguments.",
    DescriptionResourceKey = "Exit_InvalidArguments_Description")]
InvalidArguments
```

TigerCli also supports `DisplayAttribute` as a standard .NET fallback, but it is not the preferred Tiger style for new code. `DescriptionAttribute` remains literal metadata; TigerCli does not treat it as a resource key or magically localize it.

Enum text is used for:

- parser-driven enum prompt labels
- parser-driven flags prompt labels
- exit-code enum labels and descriptions
- exit-code enum type headings

Localized enum labels are display-only. Command-line parsing still uses enum member names:

```bash
tool prompt smoke Alice --mode Fast
```

A localized label such as `Szybki` is not accepted as the command-line value unless the app implements that separately.

## Providers And Culture

Named providers receive `TigerCliProviderContext`, including the resolved `Culture`.

Use `ctx.Culture` to return localized labels without changing process-wide culture:

```csharp
.ConfigureProviders(providers =>
{
    providers.Add<string>("connections", ctx =>
    [
        new OptionItem<string>("local",
            AppStrings.Get("Provider_Connection_Local_Label", ctx.Culture)),
        new OptionItem<string>("demo",
            AppStrings.Get("Provider_Connection_Demo_Label", ctx.Culture))
    ]);
});
```

Provider labels are display text. Keep provider keys stable and language-neutral:

```text
key:   demo
label: Demo connection / Połączenie demo
```

TigerCli binds the selected key or value to settings, not the display label.

## Command Handlers And settings.Culture

`settings.Culture` exposes the resolved culture for the current run. TigerCli sets it before validation and handler execution.

Most command output should use `settings.T(...)`, `settings.F(...)`, or `settings.E(...)` instead of accessing a `ResourceManager` directly. `settings.Culture` is still useful when command logic itself needs culture-aware behavior, for example:

- formatting an app-owned report
- loading localized data outside the command-output helpers
- passing culture into app services
- implementing custom provider logic

TigerCli helper lookups use `settings.Culture` and do not require mutating `CultureInfo.CurrentUICulture`.

## Testing Localized Apps

Use `TigerCliAppTestHost` to run the real app pipeline with a fixed culture.

```csharp
using ItTiger.TigerCli.Testing;

var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("--culture", "pl-PL", "--help")
    .RunAsync();

Assert.Equal(0, result.ExitCode);
Assert.Contains("Użycie", result.StdOut);
Assert.Empty(result.StdErr);
```

You can also assert localized command output:

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("greet", "--name", "Ala", "--culture", "pl-PL")
    .RunAsync();

Assert.Contains("Cześć", result.StdOut);
```

For deterministic tests, configure the app's supported/default cultures explicitly or pass `--culture` in the tested args. See [app testing](app-testing.md) for the test host workflow.

## Common Mistakes

- Do not use raw `Console.WriteLine`; use `TigerConsole.MarkupLine(...)` and `TigerConsole.MarkupErrorLine(...)`.
- Do not insert dynamic values into TigerCli markup without escaping. For localized command output, prefer `settings.E(...)` or `settings.EscapedFormatTextByKey(...)`; for raw escaping, use `CliMarkupParser.Escape(...)`.
- Do not use localized labels as command-line values or stable keys.
- Do not assume every app supports `pl-PL` just because TigerCli ships Polish framework resources.
- Do not overuse explicit resource keys when source-text helpers make the code clearer.
- Do not expect `DescriptionAttribute` to localize itself; use `TigerTextAttribute` or `DisplayAttribute`.
- Do not expect TigerCli to solve full i18n concerns such as pluralization, ICU formatting, translator workflows, or runtime language packs.

## Related Docs

- Build localizable command apps with [command apps](command-apps.md).
- Understand binding and enum parsing in [arguments and options](arguments-and-options.md).
- Localize provider labels with [prompting and providers](prompting-and-providers.md).
- Test localized CLI behavior with [app testing](app-testing.md).
- Review markup trust rules in the [help rendering trust model](../reference/help-rendering-trust-model.md).
