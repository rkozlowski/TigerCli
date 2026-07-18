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
/// process exit code as-is for app-specific enums. <see cref="TigerCliExitKind"/> is the deliberate
/// exception: it is a portable semantic outcome and resolves through the app's exit-code policy,
/// letting reusable command libraries return a framework vocabulary without owning the consuming
/// app's final process codes. Registering an app-specific enum via
/// <c>TigerCliAppBuilder.UseExitCodes&lt;TExitCode&gt;</c> keeps app codes and framework codes in one
/// documented scheme.
/// </summary>
public abstract class TigerCliAsyncCommandHandler<TSettings, TExitCode>
    where TSettings : TigerCliSettings, new()
    where TExitCode : struct, Enum
{
    /// <summary>
    /// Executes the command with fully bound, prompted, and validated <paramref name="settings"/>.
    /// An app-specific enum value becomes its underlying process exit code directly; a
    /// <see cref="TigerCliExitKind"/> value resolves through the app's exit-code policy.
    /// A thrown <see cref="ItTiger.TigerCli.Exceptions.TigerCliCommandException"/> is reported and
    /// resolved through the app's exit-code policy for its declared kind; any other exception
    /// escaping this method is reported and mapped through
    /// <see cref="TigerCliExitKind.UnhandledException"/>.
    /// </summary>
    public abstract Task<TExitCode> ExecuteAsync(TSettings settings);
}
