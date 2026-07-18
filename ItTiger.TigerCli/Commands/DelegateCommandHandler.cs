namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Settings used by the deliberately unbound delegate-command surface. Keeping delegate commands
/// on a real settings type lets them use the normal command pipeline without exposing typed
/// lambda-parameter binding.
/// </summary>
internal sealed class DelegateCommandSettings : TigerCliSettings
{
}

/// <summary>
/// Adapts a delegate command to the standard async command-handler contract.
/// </summary>
internal abstract class DelegateCommandHandler(Func<TigerCliSettings, Task<int>> handler)
    : TigerCliAsyncCommandHandler<DelegateCommandSettings>
{
    private readonly Func<TigerCliSettings, Task<int>> _handler =
        handler ?? throw new ArgumentNullException(nameof(handler));

    public override Task<int> ExecuteAsync(DelegateCommandSettings settings) => _handler(settings);
}

/// <summary>
/// Separate default and named handler types prevent the existing "same handler is also named"
/// default-command marker from treating every named delegate as the default delegate.
/// </summary>
internal sealed class DefaultDelegateCommandHandler(Func<TigerCliSettings, Task<int>> handler)
    : DelegateCommandHandler(handler)
{
}

internal sealed class NamedDelegateCommandHandler(Func<TigerCliSettings, Task<int>> handler)
    : DelegateCommandHandler(handler)
{
}
