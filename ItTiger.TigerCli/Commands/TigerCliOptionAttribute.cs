using System.Text.RegularExpressions;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Binds a settings property to a named command-line option (e.g. <c>-c</c> / <c>--connection</c>).
/// Apply to a writable property on a <see cref="TigerCliSettings"/>-derived type; the constructor
/// template defines the option's aliases.
/// </summary>
/// <remarks>
/// In semi-interactive mode, a missing promptable scalar option can be resolved before required-value
/// validation runs. Built-in scalar prompts support <c>string</c>, <c>int</c> / <c>int?</c>, enum /
/// nullable enum, <c>bool?</c>, and provider-backed select values. In non-interactive mode no prompt
/// is shown; missing required options fail validation instead of blocking for input.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TigerCliOptionAttribute : Attribute
{
    private static readonly Regex ShortNamePattern = new(@"^[a-zA-Z]{1,8}$", RegexOptions.Compiled);
    private static readonly Regex LongNamePattern = new(@"^[a-zA-Z][a-zA-Z0-9_-]*$", RegexOptions.Compiled);
    private TigerCliPromptable? _promptable;

    /// <summary>
    /// The option's aliases as parsed from the constructor template (e.g. <c>-c</c> and
    /// <c>--connection</c> from <c>"-c|--connection"</c>).
    /// </summary>
    public string[] Aliases { get; }

    /// <summary>
    /// Fallback help/prompt text for this option. Replaced by
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
    /// Optional placeholder name shown in help output (e.g. "connection-string", "sql").
    /// If not specified, a fallback is derived automatically from the property type.
    /// </summary>
    public string? ValueName { get; set; }

    /// <summary>
    /// Optional named provider key. A provider serves two purposes: a missing value can be prompted
    /// as a select over the provider's choices in semi-interactive mode, and a supplied command-line
    /// value is validated against those choices unless <see cref="ValidateAgainstProvider"/> is
    /// <c>false</c>. This is metadata and is not shown as a display label.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Optional named provider key used <b>only in edit mode</b>. When set (non-empty),
    /// edit-command resolution uses this provider in place of <see cref="Provider"/> for
    /// this option; in add/normal commands it is ignored and <see cref="Provider"/> is used.
    /// This is the option-level symmetry of the argument-level edit provider, for settings
    /// types shared by an add and an edit command where the edit command needs a
    /// provider-backed selector over existing values. Empty or whitespace is treated as not
    /// configured.
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
    /// Alias of the option that makes this option required when it has
    /// <see cref="RequiredWhenValue"/>.
    /// </summary>
    public string? RequiredWhenOption { get; set; }

    /// <summary>
    /// Expected value for <see cref="RequiredWhenOption"/>. Enum names, bool
    /// values, and strings are compared against the referenced option value.
    /// </summary>
    public string? RequiredWhenValue { get; set; }

    /// <summary>
    /// Expected values for <see cref="RequiredWhenOption"/>. The condition matches
    /// when the referenced option value equals any configured value.
    /// </summary>
    public string[]? RequiredWhenValueIn { get; set; }

    /// <summary>
    /// Excluded values for <see cref="RequiredWhenOption"/>. The condition matches
    /// when the referenced option has a value and it equals none of the configured values.
    /// </summary>
    public string[]? RequiredWhenValueNotIn { get; set; }

    /// <summary>
    /// Alias of the option that allows prompting for this option when it has
    /// <see cref="PromptWhenValue"/>.
    /// </summary>
    public string? PromptWhenOption { get; set; }

    /// <summary>
    /// Expected value for <see cref="PromptWhenOption"/>. When set and the
    /// condition is false, TigerCli will not prompt for this option.
    /// </summary>
    public string? PromptWhenValue { get; set; }

    /// <summary>
    /// Expected values for <see cref="PromptWhenOption"/>. The condition matches
    /// when the referenced option value equals any configured value.
    /// </summary>
    public string[]? PromptWhenValueIn { get; set; }

    /// <summary>
    /// Excluded values for <see cref="PromptWhenOption"/>. The condition matches
    /// when the referenced option has a value and it equals none of the configured values.
    /// </summary>
    public string[]? PromptWhenValueNotIn { get; set; }

    /// <summary>
    /// Alias or property name of an option that should be resolved before this option.
    /// This is an ordering dependency only; it does not imply requiredness or prompting.
    /// </summary>
    public string? DependsOnOption { get; set; }

    /// <summary>
    /// Aliases or property names of options that should be resolved before this option.
    /// These are ordering dependencies only; they do not imply requiredness or prompting.
    /// </summary>
    public string[]? DependsOnOptions { get; set; }

    /// <summary>
    /// When true, automatic text prompting masks the rendered value.
    /// </summary>
    public bool Secret { get; set; }

    /// <summary>
    /// Controls whether this option may be supplied through argv. Set false
    /// for prompt-only secrets such as passwords.
    /// </summary>
    public bool AllowCommandLineValue { get; set; } = true;

    /// <summary>
    /// When true, the option must be provided by argv or resolved by an allowed prompt before the
    /// handler runs. Default initializer values are not treated as valid input.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Controls whether this option participates in edit-command behavior. When
    /// false, the option is not prompted as an editable field in edit mode and is
    /// not provider-validated. Options are editable by default. Note that this
    /// setting is not fully inert outside edit mode: a non-editable option is
    /// currently skipped by provider validation in all command modes.
    /// </summary>
    public bool Editable { get; set; } = true;

    /// <summary>
    /// Controls whether the effective value of this option is validated against its
    /// provider's choices. The default is <c>true</c>, so a provider-backed editable
    /// option is validated whenever a provider is configured — in all command modes,
    /// interactive and non-interactive. A supplied value that is not an available
    /// choice fails with a validation error; a matching value is re-bound to the
    /// canonical provider key. Validation runs after required-value validation and reports
    /// <see cref="TigerCliExitKind.ValidationError"/> on mismatch. Set to <c>false</c> when the provider is
    /// suggestions-only and custom values are acceptable. Has no effect when no
    /// provider is configured or when the option is not editable.
    /// </summary>
    public bool ValidateAgainstProvider { get; set; } = true;

    /// <summary>
    /// Controls how a supplied value is matched against this option's provider choices during
    /// provider validation and multi-select resolution. The default
    /// (<see cref="TigerCliValueMatchPreset.Default"/>) matches string keys and labels
    /// case-insensitively. Use <see cref="TigerCliValueMatchPreset.Exact"/> for case-sensitive
    /// matching, or <see cref="TigerCliValueMatchPreset.FileSystemPath"/> for path-like provider
    /// keys. Regardless of preset, the bound value is the provider's canonical key.
    /// </summary>
    public TigerCliValueMatchPreset ValueMatching { get; set; } = TigerCliValueMatchPreset.Default;

    /// <summary>
    /// Controls whether TigerCli may prompt for this option when it is missing.
    /// Unspecified uses the effective command/app prompt mode.
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
    /// Values that are not accepted for this option.
    /// Primarily used with enum options to exclude sentinel values (e.g. <c>Mode.Unspecified</c>)
    /// from both validation and help output.
    /// </summary>
    public object[]? ForbiddenValues { get; set; }

    /// <summary>
    /// Defines a CLI option with one or more aliases separated by <c>|</c>, e.g.
    /// <c>"-c|--connection"</c>. Short aliases (<c>-x</c>) must be 1–8 letters; long aliases
    /// (<c>--name</c>) must start with a letter followed by letters, digits, <c>_</c> or <c>-</c>.
    /// </summary>
    /// <param name="template">The alias template; at least one alias, each starting with <c>-</c> or <c>--</c>.</param>
    /// <exception cref="ArgumentException">The template defines no alias or contains an invalid alias.</exception>
    public TigerCliOptionAttribute(string template)
    {
        Aliases = template.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (Aliases.Length == 0)
            throw new ArgumentException("Option template must define at least one alias.", nameof(template));

        foreach (var alias in Aliases)
        {
            if (alias.StartsWith("--"))
            {
                var name = alias[2..];
                if (!LongNamePattern.IsMatch(name))
                    throw new ArgumentException(
                        $"Invalid long option name '{alias}'. Must match [a-zA-Z][a-zA-Z0-9_-]*.",
                        nameof(template));
            }
            else if (alias.StartsWith('-'))
            {
                var name = alias[1..];
                if (!ShortNamePattern.IsMatch(name))
                    throw new ArgumentException(
                        $"Invalid short option name '{alias}'. Must be 1-8 letters only.",
                        nameof(template));
            }
            else
            {
                throw new ArgumentException(
                    $"Option alias '{alias}' must start with '-' or '--'.",
                    nameof(template));
            }
        }
    }
}
