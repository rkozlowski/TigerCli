namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Declares that a command accepts a <em>raw trailing argument tail</em>: every command-line token
/// after the first <c>--</c> separator, bound literally and in order. Apply to a writable
/// string-collection property on a <see cref="TigerCliSettings"/>-derived type.
/// </summary>
/// <remarks>
/// <para>
/// The tail exists for commands that hand their trailing tokens to something else — a child
/// executable, a nested tool, a pass-through argument list. TigerCli parses only the <em>head</em>
/// of the command line (the part before <c>--</c>) and never inspects the tail: tail tokens are not
/// matched against app options, framework options such as <c>--theme</c> / <c>--non-interactive</c>,
/// or app-contributed global options; they are never prompted for and never validated against a
/// provider.
/// </para>
/// <para>
/// The separator itself is not part of the bound tail. Every token after it is preserved exactly,
/// including tokens starting with <c>-</c> or <c>--</c>, repeated tokens, empty strings, and any
/// later <c>--</c>. Only the first <c>--</c> splits the command line.
/// </para>
/// <para>
/// Support is opt-in per command. A command whose settings type does not declare this attribute
/// rejects a supplied tail as a usage error with the standard help hint.
/// </para>
/// <example>
/// <code>
/// public sealed class ExecSettings : TigerCliSettings
/// {
///     [TigerCliOption("--connection-string-env", Description = "Environment variable holding the connection string.")]
///     public string ConnectionStringEnv { get; set; } = string.Empty;
///
///     [TigerCliRawArguments(
///         Name = "tool-args",
///         Description = "Child executable and its arguments, passed through unchanged.",
///         Required = true)]
///     public IReadOnlyList&lt;string&gt; Arguments { get; init; } = [];
/// }
/// </code>
/// Invoked as <c>app exec --connection-string-env DB_CONNECTION -- my-tool --report</c>, the
/// command receives <c>["my-tool", "--report"]</c>.
/// </example>
/// <para>
/// Supported property types are <c>string[]</c>, <c>List&lt;string&gt;</c>, and
/// <c>IReadOnlyList&lt;string&gt;</c>; the property must be writable (an <c>init</c> accessor is
/// fine). A settings type may declare at most one raw-argument property, and the property must not
/// also carry <see cref="TigerCliOptionAttribute"/> or <see cref="TigerCliArgumentAttribute"/>.
/// Violations are configuration errors and fail when the application is built.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TigerCliRawArgumentsAttribute : Attribute
{
    /// <summary>
    /// Optional display name used in generated help, rendered as <c>-- &lt;name&gt;...</c>. When not
    /// set, a kebab-case form of the property name is used.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Fallback help text for the tail. Replaced by <see cref="DescriptionResourceKey"/> when that
    /// key resolves.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional resource key resolved through the app-owned <see cref="System.Resources.ResourceManager"/>
    /// registered via <c>TigerCliAppBuilder.UseAppResources(...)</c> against the active run culture.
    /// When the key resolves to a non-empty string it replaces <see cref="Description"/> in help.
    /// Missing keys silently fall back to <see cref="Description"/>; the raw key is never surfaced.
    /// </summary>
    public string? DescriptionResourceKey { get; set; }

    /// <summary>
    /// When <c>true</c>, the command requires at least one token after the separator. Both a missing
    /// separator and an empty tail (<c>app exec --</c>) then fail with a usage error and the
    /// <see cref="TigerCliExitKind.MissingRequiredArgument"/> exit mapping. The tail is never
    /// prompted for, so a required tail fails identically in both interaction modes. The default is
    /// <c>false</c>, which binds an empty collection.
    /// </summary>
    public bool Required { get; set; }
}
