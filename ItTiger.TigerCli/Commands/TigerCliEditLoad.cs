namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Result of an edit-command loader. Produced by an app loader registered through
/// <see cref="TigerCliCommandBuilder.AsEdit{TSettings}(System.Func{TSettings, System.Threading.Tasks.Task{TigerCliEditLoad{TSettings}}})"/>.
/// Use <see cref="Found"/> to supply the existing object's values, or
/// <see cref="NotFound"/> when no matching object exists. On <see cref="NotFound"/>
/// the framework reports an invalid-arguments error and the command handler does not run.
/// </summary>
/// <remarks>
/// The loader receives the bound settings after selector arguments and command-line overrides have
/// been applied. It is not given a cancellation token parameter; exceptions from the loader flow through
/// the normal command run failure path.
/// </remarks>
/// <typeparam name="TSettings">The command settings type.</typeparam>
public sealed class TigerCliEditLoad<TSettings>
    where TSettings : TigerCliSettings
{
    private TigerCliEditLoad(bool isFound, TSettings? existing)
    {
        IsFound = isFound;
        Existing = existing;
    }

    /// <summary>True when an existing object was found and its values are available.</summary>
    public bool IsFound { get; }

    /// <summary>
    /// The existing object's values, or null when <see cref="IsFound"/> is false.
    /// The framework copies these values into the live settings for every property
    /// that was not supplied on the command line.
    /// </summary>
    public TSettings? Existing { get; }

    /// <summary>
    /// Indicates the existing object was found. <paramref name="existing"/> carries the
    /// current values to seed the edit; command-line values still win over them.
    /// </summary>
    public static TigerCliEditLoad<TSettings> Found(TSettings existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return new TigerCliEditLoad<TSettings>(isFound: true, existing);
    }

    /// <summary>Indicates no matching object was found; the framework reports an error.</summary>
    public static TigerCliEditLoad<TSettings> NotFound() =>
        new(isFound: false, existing: null);
}

/// <summary>
/// Type-erased edit-loader result the framework passes around internally.
/// </summary>
internal readonly record struct TigerCliEditLoadResult(bool IsFound, TigerCliSettings? Existing);
