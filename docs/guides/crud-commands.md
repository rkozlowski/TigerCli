# CRUD Command Apps

TigerCli is a framework for **command-oriented CRUD-style CLI apps** — tools whose commands list, show, add, edit, and delete records (connections, profiles, devices, environments, …). This guide maps each CRUD verb onto the TigerCli abstraction that implements it well, so commands stay consistent and you do not re-hand-roll rendering or prompting per command.

## The CRUD command map

| Verb | Use | What it does |
|------|-----|--------------|
| `list` | [`CliList`](structured-output.md#clilist) | Render many records as a table. |
| `show` | [`CliDetails`](structured-output.md#clidetails) | Render one record as labelled fields. |
| `add` | [prompts](prompting-and-providers.md) | Collect required new values, validate, create. |
| `edit` | [prompts + `.AsEdit()`](command-apps.md#edit-commands) | Load existing, show existing values as defaults, keep or replace each, validate, save. |
| `delete` | [confirmation prompt](semi-interactive-prompts.md) | Confirm, then remove. |

Keep output on the TigerCli rendering path. Use `MarkupLine` for simple messages (confirmations, status, errors) — not for normal list/details rendering.

## CRUD commands are shaped around selectors

A [selector](arguments-and-options.md#arguments-options-and-selectors) is command input that identifies the object key used by the command — usually the natural key / primary key of the domain object, including any parent/context key that participates in its identity. The selector stays the same across the CRUD verbs:

```text
project add    <connection> <project> --schema dbo
project show   <connection> <project>
project edit   <connection> <project> --schema sales
project delete <connection> <project>
```

In all four commands the selector/key is the same `(connection-name, project-name)` pair:

- In `add`, the selector identifies the **new object being created**.
- In `show`, `edit`, and `delete`, the same selector identifies the **existing object**.
- `--schema` is object data, not a selector — it stays an option **even when required**. Required does not mean selector; selector usually means positional.

## list → CliList

A `list` command should not manually render headings, spacing, indentation, loops, or column formatting with `MarkupLine`. Declare columns once and render the sequence:

```csharp
var list = new CliList<Device>()
    .ApplyPreset(CliTableStylePreset.Lucca)
    .AddTitle(s.T("Devices"))
    .AddKeyColumn(s.T("Id"), device => device.Id)
    .AddColumn(s.T("Name"), device => device.Name)
    .AddColumn(s.T("Model"), device => device.Model)
    .AddKeyColumn(s.T("Group"), device => device.GroupId);

TigerConsole.Render(list.Render(devices));
```

Use `AddKeyColumn` for identity/anchor values (IDs, names, codes, slugs, group IDs), `AddPathColumn` for filesystem paths, and `AddColumn(label, selector, style: …)` for other [semantic value styles](structured-output.md#semantic-value-styles). An empty sequence renders a header-only table by default; branch before `Render` if you want a custom empty message.

## show → CliDetails

A `show`/details command renders a single record as labelled fields:

```csharp
var details = new CliDetails()
    .ApplyPreset(CliTableStylePreset.Lucca)
    .AddTitle(s.T("SQL Server connection"))
    .AddKey(s.T("Name:"), profile.Name)
    .Add(s.T("Server:"), profile.Server)
    .Add(s.T("Authentication:"), profile.Authentication)
    .AddWhen(profile.Authentication == AuthenticationType.SqlPassword,
        s.T("Username:"), profile.Username)
    .AddOptional(s.T("Database:"), profile.Database)
    .AddOptionalPath(s.T("Config:"), profile.ConfigPath);

TigerConsole.Render(details);
```

`AddKey`/`AddPath` and the `style:` parameter style the **value**, not the label. See [CliDetails](structured-output.md#clidetails).

## add → prompts for required new values

An `add` command creates a new object. Its positionals are selectors: the new object's key and any parent/context keys that participate in its identity. The object's data are options — required ones marked `Required = true`, the rest defaulted. Draw values from CLI arguments/options, initializer defaults, providers, and prompts for any required value that was not supplied, then validate and create. See [prompting and providers](prompting-and-providers.md) for parser-driven prompts and provider-backed choices.

## edit → prompts with existing values via `.AsEdit()`

**Edit is not "add with optional values."** An edit command must:

1. **Load** the existing item.
2. **Show existing values as editable/default values.**
3. **Allow the user to keep or replace** each value.
4. **Validate.**
5. **Save** the changes.

`.AsEdit()` is the TigerCli way to express that flow, and it is built on selectors: the positional arguments identify the object to load, and TigerCli resolves them *before* calling your loader. You opt in on the command registration and supply a loader; TigerCli then seeds editable fields from the loaded object, treats command-line values as explicit overrides (never prompted, never overwritten), prompts the remaining editable fields with the existing value as the default, validates against any configured provider, and hands your handler the resolved object to save:

```csharp
builder.AddCommandGroup("connection", group => group
    .AddCommand<EditConnectionCommand>("edit", b => b
    .AsEdit<ConnectionSettings>(async settings =>
    {
        // settings.Profile (the selector argument) is already bound from the command line.
        var profile = await store.FindAsync(settings.Profile);
        return profile is null
            ? TigerCliEditLoad<ConnectionSettings>.NotFound()
            : TigerCliEditLoad<ConnectionSettings>.Found(new ConnectionSettings
              {
                  Server   = profile.Server,
                  Database = profile.Database,
              });
    })));
```

```text
connection edit reporting --server sql-new
```

`--server sql-new` overrides and is not prompted; `--database` and other editable fields are seeded from the existing profile and (in semi-interactive mode) prompted with the existing value preselected, so accepting the default keeps it. Saving the resolved object stays in your `ExecuteAsync`.

Contrast this with add: add starts from initializer/default values and collects required new values; edit starts from the **loaded existing values** and lets the user keep or replace them. Expressing edit as "add with everything optional" loses the load step, the existing-value defaults, and provider validation of stale values. Use `.AsEdit()`.

### Sharing one settings class between add and edit

Because add and edit share the same selector/key and differ only in *flow*, they can — and often should — share a settings class. The selector properties are selectors in **both** commands; only the edit-mode input experience differs, and that is exactly what `EditProvider` is for:

```csharp
public sealed class ProjectSettings : TigerCliSettings
{
    [TigerCliArgument(0, Name = "connection", EditProvider = "connections",
        Promptable = TigerCliPromptable.Normal, Description = "Connection name.")]
    public string ConnectionName { get; set; } = "";

    [TigerCliArgument(1, Name = "project", EditProvider = "projects",
        Promptable = TigerCliPromptable.Normal, Description = "Project name.")]
    public string ProjectName { get; set; } = "";

    [TigerCliOption("--schema", Required = true,
        Promptable = TigerCliPromptable.Normal, Description = "Schema name.")]
    public string Schema { get; set; } = "";
}
```

```text
project add  <connection> <project> --schema dbo
project edit <connection> <project> --schema sales
```

- `<connection>` and `<project>` are the selectors in **both** commands. In `add` they identify the new project being created (under an existing connection); in `edit` they identify the existing project to load. Same properties, same role, same command shape.
- `EditProvider` does not make a property a selector — the property already is one. It changes the edit-mode input experience for that selector: in `add`, a missing `<project>` is typed as a new key (`EditProvider` is ignored outside edit mode, and no normal `Provider` is set); in `edit`, a missing `<project>` is selected from the provider's existing project keys.
- `--schema` is required in both, but it is **not** a selector — it is object data being set or changed — so it stays an option. Required does not mean selector.

The same selector property can be typed as a new key in add mode but selected from existing keys in edit mode. That is why `EditProvider` exists.

For the full behaviour — editability, provider validation, edit-only providers (`EditProvider`), and secret fields — see [Edit commands](command-apps.md#edit-commands).

## Multi-value fields → dynamic multi-select

When an add/edit field holds *zero or more* values chosen from a provider, mark it `[TigerCliMultiSelect]` next to `[TigerCliOption]`. A common CRUD shape is a set of bit flags where the prompt shows friendly labels but the app stores a combined numeric mask (e.g. language options):

```csharp
public sealed class ProjectSettings : TigerCliSettings
{
    [TigerCliOption("--language-options", Provider = "language-options",
        Promptable = TigerCliPromptable.Normal, Description = "Language options to enable.")]
    [TigerCliMultiSelect]
    public long[]? LanguageOptions { get; set; }
}

// Provider returns keyed choices: the key is the bit value, the label is display text.
.ConfigureProviders(providers => providers.Add<long>("language-options", _ =>
    LoadLanguageOptions().Select(o =>
        new OptionItem<long>(o.BitValue, $"{o.Name} (0x{o.BitValue:X})")).ToList()));
```

The command receives the selected keys and applies the domain logic — here OR-ing them into one mask:

```csharp
var languageOptions = settings.LanguageOptions?.Aggregate(0L, (acc, value) => acc | value);
```

Division of responsibility:

```text
TigerCli:                                  App:
- loads provider choices                   - decides which provider to expose
- displays labels                          - decides what labels mean
- interactive checklist selection          - applies domain logic (e.g. OR-ing bit values)
- comma-separated + repeated CLI values
- validates values against keys/labels
- binds selected keys into the property
```

Non-interactive invocations (comma-separated, by label, or repeated) all bind the same keys:

```bash
app projects add --language-options 4,8
app projects add --language-options "Use DateOnly (0x4)"
app projects add --language-options 4 --language-options 8
```

In edit mode the current value seeds the checklist preselection, so the existing selection is offered for keep-or-change like any other editable field. See [multi-select](prompting-and-providers.md#multi-select-select-zero-or-many) for string lists, `AllowEmpty`, and `AllowCustomValues`.

## delete → confirmation prompts

A `delete` command should confirm before removing, then report the outcome:

```csharp
var confirmed = await TigerTui.ConfirmAsync(s.T("Delete connection '{0}'?", name), preselect: false);
if (confirmed != true) // false or null (cancelled / non-interactive)
{
    TigerConsole.MarkupLine(s.T("[Muted]Cancelled.[/]"));
    return (int)ExitCode.Cancelled;
}

await store.DeleteAsync(name);
TigerConsole.MarkupLine(s.T("[Success]Deleted.[/]"));
```

See [semi-interactive prompts](semi-interactive-prompts.md) for confirm/select/input prompts and how they degrade safely in non-interactive mode.

## Related Docs

- [Structured output](structured-output.md) — `CliList`, `CliDetails`, semantic value styles.
- [CliTable](cli-table.md) — the lower-level table API the builders sit on.
- [Command apps](command-apps.md) — command registration, settings, and `.AsEdit()`.
- [Prompting and providers](prompting-and-providers.md) — collecting and validating values.
- [Semi-interactive prompts](semi-interactive-prompts.md) — confirm/select/input controls.
