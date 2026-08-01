namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Marks a string command option as an output-file path that uses the inline file picker when
/// TigerCli prompts for a missing value.
/// </summary>
/// <remarks>
/// The selected value remains one string path. Its parent directory must already exist. Existing
/// files are governed by <see cref="Overwrite"/> and, when configured, <see cref="OverwriteWhenOption"/>.
/// A file-save option is treated as a required path even when
/// <see cref="TigerCliOptionAttribute.Required"/> is false.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TigerCliFileSaveAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the extension appended when the accepted file name has no extension. A leading
    /// dot is optional. Null or whitespace disables extension completion.
    /// </summary>
    public string? DefaultExtension { get; set; }

    /// <summary>Gets or sets the file name used to seed an otherwise empty interactive picker.</summary>
    public string? DefaultFileName { get; set; }

    /// <summary>Gets or sets the policy applied when the resolved path already exists.</summary>
    public TigerCliFileOverwrite Overwrite { get; set; } = TigerCliFileOverwrite.Prompt;

    /// <summary>
    /// Gets or sets the name of a <see cref="bool"/> or nullable <see cref="bool"/> property on the
    /// same settings type. When its bound value is true, overwrite is allowed without rejection or
    /// confirmation. False and null leave <see cref="Overwrite"/> in effect.
    /// </summary>
    public string? OverwriteWhenOption { get; set; }

    /// <summary>
    /// Gets or sets an optional filesystem search pattern, such as <c>*.json</c>, used to filter the
    /// files shown by the picker. It does not restrict a path typed or supplied directly.
    /// </summary>
    public string? Filter { get; set; }
}
