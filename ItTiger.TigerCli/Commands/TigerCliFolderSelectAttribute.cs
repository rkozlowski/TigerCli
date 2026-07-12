namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Marks a <c>string</c> / <c>string?</c> command option so that, when TigerCli needs to prompt
/// for its missing value in semi-interactive mode, it uses the inline folder picker
/// (<c>InlineFolderSelect</c>) instead of a plain text prompt. The selected folder path becomes
/// the option value.
/// </summary>
/// <remarks>
/// <para>
/// Apply this alongside <see cref="TigerCliOptionAttribute"/> on the same property. It has no effect
/// on command-line parsing: a value supplied on the command line is bound normally and the picker is
/// never shown. In non-interactive mode no picker is shown; normal missing-required validation still
/// applies. The current/default value of the property (if any) seeds the picker's initial folder.
/// </para>
/// <para>
/// Only <c>string</c> and nullable <c>string</c> properties are supported. Applying it to any other
/// property type is a configuration error and is rejected when the command's option metadata is built.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TigerCliFolderSelectAttribute : Attribute
{
}
