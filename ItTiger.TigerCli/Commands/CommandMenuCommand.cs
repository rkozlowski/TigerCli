namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Empty settings for the internal command-menu sentinel. The menu carries no options or
/// arguments of its own; it only selects another command to run.
/// </summary>
internal sealed class CommandMenuSettings : TigerCliSettings
{
}

/// <summary>
/// Internal sentinel handler used to register the command menu as a normal command (default or
/// named). The menu is intercepted in <see cref="TigerCliApp"/> before execution — it never runs
/// this handler — but a concrete handler type lets the menu reuse the standard command
/// registration model. The registration is flagged <c>IsCommandMenu</c> so it is hidden from help
/// and from the menu's own eligible list.
/// </summary>
internal sealed class CommandMenuCommand : TigerCliAsyncCommandHandler<CommandMenuSettings>
{
    public override Task<int> ExecuteAsync(CommandMenuSettings settings) => Task.FromResult(0);
}
