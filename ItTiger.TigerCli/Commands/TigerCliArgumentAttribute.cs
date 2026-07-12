namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Binds a settings property to a positional command-line argument. Arguments are matched by
/// position (<see cref="Index"/>) after the command path; they carry no alias on the command line.
/// Apply to a writable property on a <see cref="TigerCliSettings"/>-derived type.
/// </summary>
/// <remarks>
/// Positional arguments are required. In semi-interactive mode, a missing promptable argument can be
/// resolved before missing-argument validation runs; in non-interactive mode no prompt is shown and
/// the missing argument fails validation. Built-in scalar prompts support <c>string</c>, <c>int</c> /
/// <c>int?</c>, enum / nullable enum, <c>bool?</c>, and provider-backed select values.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TigerCliArgumentAttribute : Attribute
{
    private TigerCliPromptable? _promptable;

    /// <summary>
    /// The zero-based positional index of this argument within the command's argument list.
    /// Set through the constructor; must be non-negative.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Optional display name used in help and prompt text. When not set, a kebab-case form of the
    /// property name is used.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Fallback help/prompt text for this argument. Replaced by
    /// <see cref="DescriptionResourceKey"/> when that key resolves.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional resource key resolved through the app-owned <see cref="System.Resources.ResourceManager"/>
    /// registered via <c>TigerCliAppBuilder.UseAppResources(...)</c> against the active
    /// run culture. When the key resolves to a non-empty string it replaces
    /// <see cref="Description"/> in help and prompt text. Missing keys silently fall
    /// back to <see cref="Description"/>; the raw key is never surfaced.
    /// </summary>
    public string? DescriptionResourceKey { get; set; }

    /// <summary>
    /// Optional named provider key. A provider serves two purposes: a missing value can be prompted
    /// as a select over the provider's choices in semi-interactive mode, and a supplied command-line
    /// value is validated against those choices unless <see cref="ValidateAgainstProvider"/> is
    /// <c>false</c>. Implicit name-matched providers may help prompting but do not make positional
    /// argument values provider-authoritative for validation. This is metadata and is not shown as a
    /// display label.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Optional named provider key used <b>only in edit mode</b>. When set (non-empty),
    /// edit-command resolution uses this provider in place of <see cref="Provider"/> for
    /// this argument; in add/normal commands it is ignored and <see cref="Provider"/> is
    /// used. This lets a settings type shared by an add and an edit command keep a typed
    /// value in add mode (no <see cref="Provider"/>) while offering a provider-backed
    /// selector over existing values in edit mode (e.g. a positional <c>name</c> that is a
    /// new name when adding but selects an existing record when editing). Empty or
    /// whitespace is treated as not configured.
    /// </summary>
    public string? EditProvider { get; set; }

    /// <summary>
    /// Minimum string length for the supplied value. Unset is <c>-1</c> (no minimum). Applied as
    /// live validation while prompting and as final framework validation for values supplied on the
    /// command line or accepted from a prompt.
    /// </summary>
    public int MinLength { get; set; } = -1;

    /// <summary>
    /// Maximum string length for the supplied value. Unset is <c>-1</c> (no maximum). Applied as
    /// live validation while prompting and as final framework validation for values supplied on the
    /// command line or accepted from a prompt.
    /// </summary>
    public int MaxLength { get; set; } = -1;

    /// <summary>
    /// Minimum integer value. Unset sentinel is <see cref="int.MinValue"/>. Mutually exclusive with
    /// <see cref="MinValueProvider"/>. Applied both as live validation while prompting and as final
    /// framework validation of supplied values. Bound failures are validation errors
    /// (<see cref="TigerCliExitKind.ValidationError"/>).
    /// </summary>
    public int MinValue { get; set; } = int.MinValue;

    /// <summary>
    /// Maximum integer value. Unset sentinel is <see cref="int.MaxValue"/>. Mutually exclusive with
    /// <see cref="MaxValueProvider"/>. Applied both as live validation while prompting and as final
    /// framework validation of supplied values. Bound failures are validation errors
    /// (<see cref="TigerCliExitKind.ValidationError"/>).
    /// </summary>
    public int MaxValue { get; set; } = int.MaxValue;

    /// <summary>
    /// Optional named provider key supplying the integer minimum dynamically. The provider must
    /// resolve to exactly one int-compatible key. Used only when <see cref="MinValue"/> is unset,
    /// during integer prompts and final integer-bound validation. Missing providers, non-integer
    /// values, multiple values, and <c>MinValueProvider</c> / <see cref="MinValue"/> conflicts are
    /// validation errors.
    /// </summary>
    public string? MinValueProvider { get; set; }

    /// <summary>
    /// Optional named provider key supplying the integer maximum dynamically. The provider must
    /// resolve to exactly one int-compatible key. Used only when <see cref="MaxValue"/> is unset,
    /// during integer prompts and final integer-bound validation. Missing providers, non-integer
    /// values, multiple values, and <c>MaxValueProvider</c> / <see cref="MaxValue"/> conflicts are
    /// validation errors.
    /// </summary>
    public string? MaxValueProvider { get; set; }

    /// <summary>
    /// Controls whether this argument participates in edit-command behavior. When
    /// false, the argument is not prompted as an editable field in edit mode and is
    /// not provider-validated as an editable field there — selector arguments defer to
    /// the edit loader instead. Arguments are not editable by default because
    /// positional arguments are usually selectors that identify the object being
    /// edited. This setting only affects edit mode; in normal commands provider
    /// validation is controlled by <see cref="Provider"/> and
    /// <see cref="ValidateAgainstProvider"/> alone.
    /// </summary>
    public bool Editable { get; set; }

    /// <summary>
    /// Controls whether the effective value of this argument is validated against its
    /// provider's choices. The default is <c>true</c>: in normal commands an argument
    /// with an explicit <see cref="Provider"/> is validated (a supplied value that is
    /// not an available choice fails with a validation error, and a matching value is
    /// re-bound to the canonical provider key); in edit mode validation applies to
    /// editable arguments. Set to <c>false</c> when the provider is suggestions-only
    /// and custom values are acceptable. Validation runs after missing-argument validation and reports
    /// <see cref="TigerCliExitKind.ValidationError"/> on mismatch. Has no effect when no provider is configured.
    /// </summary>
    public bool ValidateAgainstProvider { get; set; } = true;

    /// <summary>
    /// Controls how a supplied value is matched against this argument's provider choices during
    /// provider validation. The default (<see cref="TigerCliValueMatchPreset.Default"/>)
    /// matches string keys and labels case-insensitively. Use
    /// <see cref="TigerCliValueMatchPreset.Exact"/> for case-sensitive matching, or
    /// <see cref="TigerCliValueMatchPreset.FileSystemPath"/> for path-like provider keys.
    /// Regardless of preset, the bound value is the provider's canonical key.
    /// </summary>
    public TigerCliValueMatchPreset ValueMatching { get; set; } = TigerCliValueMatchPreset.Default;

    /// <summary>
    /// Controls whether and when TigerCli may prompt for this argument when it is missing.
    /// When not assigned, the effective command/app prompt mode decides, in normal prompt order.
    /// </summary>
    public TigerCliPromptable Promptable
    {
        get => _promptable ?? TigerCliPromptable.No;
        set => _promptable = value;
    }

    internal TigerCliPromptable? PromptableValue => _promptable;

    /// <summary>
    /// When true, a provider-backed prompt may skip the select UI and bind the only selectable
    /// outcome. The default is false so prompts continue to ask for confirmation unless opted in.
    /// </summary>
    public bool AutoSelectSingleChoice { get; set; }

    /// <summary>
    /// Declares a positional argument at the given zero-based <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based position of the argument; must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public TigerCliArgumentAttribute(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Argument index must be non-negative.");
        Index = index;
    }
}
