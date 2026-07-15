namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Base class for async command handlers returning a raw integer exit code. Handlers are created
/// per run — via their parameterless constructor, or via the registration's factory when one was
/// supplied — so they must not assume shared state between runs.
/// </summary>
public abstract class TigerCliAsyncCommandHandler<TSettings> where TSettings : TigerCliSettings, new()
{
    /// <summary>
    /// Executes the command with fully bound, prompted, and validated <paramref name="settings"/>.
    /// The returned integer becomes the process exit code as-is; it is not remapped by the app's
    /// exit-code policy, which applies only to framework-owned outcomes
    /// (<see cref="TigerCliExitKind"/>). A thrown
    /// <see cref="ItTiger.TigerCli.Exceptions.TigerCliCommandException"/> is reported and resolved
    /// through the app's exit-code policy for its declared kind; any other exception escaping this
    /// method is reported and mapped through <see cref="TigerCliExitKind.UnhandledException"/>.
    /// </summary>
    public abstract Task<int> ExecuteAsync(TSettings settings);
}

/// <summary>
/// Base class for async command handlers returning a typed exit code. The returned
/// <typeparamref name="TExitCode"/> value is converted to its underlying integer and used as the
/// process exit code as-is; it is not remapped by the app's exit-code policy, which applies only
/// to framework-owned outcomes (<see cref="TigerCliExitKind"/>). Registering the same enum via
/// <c>TigerCliAppBuilder.UseExitCodes&lt;TExitCode&gt;</c> keeps app codes and framework codes in
/// one documented scheme.
/// </summary>
public abstract class TigerCliAsyncCommandHandler<TSettings, TExitCode>
    where TSettings : TigerCliSettings, new()
    where TExitCode : struct, Enum
{
    /// <summary>
    /// Executes the command with fully bound, prompted, and validated <paramref name="settings"/>.
    /// A thrown <see cref="ItTiger.TigerCli.Exceptions.TigerCliCommandException"/> is reported and
    /// resolved through the app's exit-code policy for its declared kind; any other exception
    /// escaping this method is reported and mapped through
    /// <see cref="TigerCliExitKind.UnhandledException"/>.
    /// </summary>
    public abstract Task<TExitCode> ExecuteAsync(TSettings settings);
}
