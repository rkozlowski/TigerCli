using CommandParserTest.Resources;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Primitives;

namespace CommandParserTest;

public static class CommandParserTestApp
{
    public static TigerCliApp Create()
    {
        // Each AddCommand / AddDescription call passes a fallback English string
        // plus an optional resource key. When the registered app ResourceManager
        // resolves the key for the active --culture, the localized string wins;
        // otherwise the fallback is used. Missing keys silently fall back - the
        // raw key is never shown.
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .SetDefaultCulture("en-US")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(CommandParserTestStrings.ResourceManager)
            .AddDescription(
                "[green]TigerCli command parser manual test app.[/]",
                resourceKey: "App_Description")
            .UseExitCodes<ParserTestExitCode>(ParserTestExitCode.Ok, ParserTestExitCode.InternalError)
                .ExitKind(TigerCliExitKind.InvalidArguments, ParserTestExitCode.InvalidArguments)
                .ExitKind(TigerCliExitKind.MissingRequiredArgument, ParserTestExitCode.MissingRequiredArgument)
                .ExitKind(TigerCliExitKind.ValidationError, ParserTestExitCode.ValidationError)
                .ExitKind(TigerCliExitKind.InteractiveNotAllowed, ParserTestExitCode.InteractiveNotAllowed)
                .ExitKind(TigerCliExitKind.UnhandledException, ParserTestExitCode.UnhandledException)
            .SetDefaultCommand<DefaultCommand>()
            .AddCommand<EchoCommand>("echo", "Echoes a message.",
                descriptionResourceKey: "Cmd_Echo_Description")
            .AddCommand<ModeCommand>("mode", "Prints the effective interaction mode.",
                descriptionResourceKey: "Cmd_Mode_Description")
            .AddCommandGroup("projects", group => group
                .AddCommand<ProjectsSpAddCommand>("sp-add", "Adds a stored procedure to a project.",
                    descriptionResourceKey: "Cmd_ProjectsSpAdd_Description"))
            .AddCommandGroup("prompt", group => group
                .AddCommand<PromptSmokeCommand>("smoke", "Prompts missing simple values.",
                    descriptionResourceKey: "Cmd_PromptSmoke_Description"))
            .AddCommandGroup("provider", group => group
                .AddCommand<ProviderSmokeCommand>("smoke", "Prompts provider-backed connection and project values.",
                    descriptionResourceKey: "Cmd_ProviderSmoke_Description"))
            .AddCommand<FeaturesCommand>("features", "Selects feature flags with a dynamic multi-select.")
            .ConfigureProviders(providers =>
            {
                // Provider callbacks read ctx.Culture and return localized labels.
                // The selected key (e.g. "local", "demo") remains language-neutral.
                providers.Add<string>("connections", ctx =>
                [
                    new OptionItem<string>("local",
                        CommandParserTestStrings.Get("Provider_Connection_Local_Label", ctx.Culture)),
                    new OptionItem<string>("demo",
                        CommandParserTestStrings.Get("Provider_Connection_Demo_Label", ctx.Culture))
                ]);

                providers.Add<ProviderSmokeSettings, string>("projects", (settings, ctx) =>
                    settings.ConnectionName == "local"
                        ?
                        [
                            new OptionItem<string>("billing",
                                CommandParserTestStrings.Get("Provider_Project_Billing_Label", ctx.Culture)),
                            new OptionItem<string>("inventory",
                                CommandParserTestStrings.Get("Provider_Project_Inventory_Label", ctx.Culture))
                        ]
                        :
                        [
                            new OptionItem<string>("sandbox",
                                CommandParserTestStrings.Get("Provider_Project_Sandbox_Label", ctx.Culture)),
                            new OptionItem<string>("training",
                                CommandParserTestStrings.Get("Provider_Project_Training_Label", ctx.Culture))
                        ]);

                // Keyed multi-select choices: the key is the bit value, the label is display text.
                // The command ORs the selected keys into a combined flag mask. The provider key is
                // deliberately distinct from any property name to avoid property-name provider matching.
                providers.Add<long>("feature-flags", _ =>
                [
                    new OptionItem<long>(1, "Read (0x1)"),
                    new OptionItem<long>(2, "Write (0x2)"),
                    new OptionItem<long>(4, "Execute (0x4)")
                ]);
            })
            .AddCommand<FailCommand>("fail", "Returns a typed failure code.",
                descriptionResourceKey: "Cmd_Fail_Description")
            .AddCommand<ThrowCommand>("throw", "Throws an exception to test UnhandledException mapping.",
                descriptionResourceKey: "Cmd_Throw_Description")
            .AddCommand<RawCommand>("raw", "Returns a raw integer exit code.",
                descriptionResourceKey: "Cmd_Raw_Description")
            .Build();
    }
}
