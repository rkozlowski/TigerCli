namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Marks a collection command option as a <em>multi-select</em>. TigerCli then treats the option as
/// "select zero or more values" sourced from the option's value provider, in the same spirit as
/// <see cref="TigerCliFolderSelectAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Apply this alongside <see cref="TigerCliOptionAttribute"/> on the same property. The provider is
/// resolved exactly like a single-select provider option (via <see cref="TigerCliOptionAttribute.Provider"/>
/// or a property-name provider registered with <c>ConfigureProviders</c> / group / command <c>AddProvider</c>).
/// </para>
/// <para>
/// Supported property types are <c>List&lt;T&gt;</c> and <c>T[]</c> where <c>T</c> is <c>string</c>,
/// <c>int</c>, <c>short</c>, <c>long</c>, or <c>Guid</c>. String collections model a simple list of
/// values; the others model key/label choices where the provider returns <c>OptionItem&lt;T&gt;</c>
/// (a display label per selectable key) and the selected keys are bound back into the collection.
/// </para>
/// <list type="bullet">
/// <item><description><b>Non-interactive</b>: the value is a comma-separated list (and/or a repeated
/// option, e.g. <c>--x a,b</c> or <c>--x a --x b</c>). Each token is matched against the provider's
/// choices by key or label using <see cref="TigerCliOptionAttribute.ValueMatching"/>; the matched
/// provider key is bound, not the raw token. Unless <see cref="AllowCustomValues"/> is set (string
/// collections only), unknown tokens are rejected with a validation error.</description></item>
/// <item><description><b>Semi-interactive</b>: a missing value is prompted with the inline multi-select
/// checklist (<c>InlineMultiSelect</c>). The property's current value seeds the preselection, and
/// selected provider keys are bound in choice order.</description></item>
/// </list>
/// <para>
/// An empty selection is allowed by default; set <see cref="AllowEmpty"/> to <c>false</c> to require at
/// least one value when the option is prompted or supplied.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TigerCliMultiSelectAttribute : Attribute
{
    /// <summary>
    /// When true, command-line tokens that do not match any provider choice are kept as-is instead of
    /// being rejected. Only valid for <c>string</c> collections; setting it on a keyed collection is a
    /// configuration error.
    /// </summary>
    public bool AllowCustomValues { get; set; }

    /// <summary>
    /// Whether a zero-length selection is accepted. Defaults to <c>true</c>. When false, an empty result
    /// (no tokens supplied, or nothing picked in the checklist) is rejected as a validation error.
    /// </summary>
    public bool AllowEmpty { get; set; } = true;
}
