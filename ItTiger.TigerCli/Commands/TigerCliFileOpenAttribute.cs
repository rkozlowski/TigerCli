namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Marks a string command option as an existing-file path that uses the inline file picker when
/// TigerCli prompts for a missing value.
/// </summary>
/// <remarks>
/// Apply this alongside <see cref="TigerCliOptionAttribute"/>. Values supplied on the command line
/// are not browsed, but are still validated as existing files. A file-open option is treated as a
/// required path even when <see cref="TigerCliOptionAttribute.Required"/> is false. The attribute
/// supports one file only.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TigerCliFileOpenAttribute : Attribute
{
    /// <summary>
    /// Gets or sets an optional filesystem search pattern, such as <c>*.json</c>, used to filter the
    /// files shown by the picker. It does not weaken validation of a directly supplied path.
    /// </summary>
    public string? Filter { get; set; }
}
