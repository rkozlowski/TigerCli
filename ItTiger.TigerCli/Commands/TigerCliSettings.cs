using System.Globalization;
using System.Resources;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Resources;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Base class for command settings. A settings class declares a command's CLI surface through
/// properties decorated with <see cref="TigerCliOptionAttribute"/> and
/// <see cref="TigerCliArgumentAttribute"/>; the framework creates the instance, binds command-line
/// input onto the decorated properties, prompts for missing promptable values (mode permitting),
/// runs framework validation and then <see cref="Validate"/>, and finally passes the instance to
/// the command handler's <c>ExecuteAsync</c>.
/// </summary>
public abstract class TigerCliSettings
{
    /// <summary>
    /// The effective interaction mode, resolved by the framework before validation and handler
    /// execution. Handlers read this to decide whether interactive UI is available.
    /// </summary>
    public TigerCliInteractionMode InteractionMode { get; internal set; } = TigerCliInteractionMode.SemiInteractive;

    /// <summary>
    /// The resolved active run culture, populated by the framework before validation and handler
    /// execution. Defaults to <c>en-US</c>. The text-lookup helpers on this class resolve
    /// resources against this culture without mutating <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public CultureInfo Culture { get; internal set; } = CultureInfo.GetCultureInfo("en-US");

    internal ResourceManager? AppResources { get; set; }

    /// <summary>
    /// Source-text lookup: uses <paramref name="text"/> as both the resource key and the fallback,
    /// resolves against the app resources (registered via <c>UseAppResources</c>) using
    /// <see cref="Culture"/>, and returns <paramref name="text"/> unchanged when resources, key,
    /// or value are missing.
    /// </summary>
    public string T(string text)
    {
        return TextByKey(text, text);
    }

    /// <summary>
    /// Source-text format lookup: resolves the format string via <see cref="T"/>, then formats
    /// with <see cref="CultureInfo.InvariantCulture"/>. Arguments are not markup-escaped; use
    /// <see cref="E"/> when arguments may contain markup-significant characters.
    /// </summary>
    public string F(string fallbackFormat, params object?[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, T(fallbackFormat), args);
    }

    /// <summary>
    /// Source-text format lookup for TigerCli markup: resolves the format string via <see cref="T"/>,
    /// converts each argument invariantly, escapes it with <c>CliMarkupParser.Escape</c>, then
    /// formats invariantly.
    /// </summary>
    public string E(string fallbackFormat, params object?[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, T(fallbackFormat), EscapeArgs(args));
    }

    /// <summary>
    /// Explicit-key lookup from the registered app <see cref="ResourceManager"/> using
    /// <see cref="Culture"/>; returns <paramref name="fallback"/> when resources, key, or value
    /// are missing.
    /// </summary>
    public string TextByKey(string resourceKey, string fallback)
    {
        return TigerCliAppText.Resolve(fallback, resourceKey, Culture, AppResources) ?? fallback;
    }

    /// <summary>
    /// Explicit-key format lookup via <see cref="TextByKey"/>, then formats with
    /// <see cref="CultureInfo.InvariantCulture"/>. Arguments are not markup-escaped; use
    /// <see cref="EscapedFormatTextByKey"/> when arguments may contain markup-significant
    /// characters.
    /// </summary>
    public string FormatTextByKey(string resourceKey, string fallbackFormat, params object?[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, TextByKey(resourceKey, fallbackFormat), args);
    }

    /// <summary>
    /// Explicit-key format lookup for TigerCli markup via <see cref="TextByKey"/>: converts each
    /// argument invariantly, escapes it with <c>CliMarkupParser.Escape</c>, then formats invariantly.
    /// </summary>
    public string EscapedFormatTextByKey(string resourceKey, string fallbackFormat, params object?[] args)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            TextByKey(resourceKey, fallbackFormat),
            EscapeArgs(args));
    }

    /// <summary>
    /// User-defined validation hook, invoked by the framework after binding, prompting, and
    /// framework validation succeed and before the handler executes. Return
    /// <see cref="TigerCliValidationResult.Error(string)"/> to fail the run: the message is
    /// rendered as literal text (markup-escaped) in the framework error line and the run ends with
    /// the <see cref="TigerCliExitKind.ValidationError"/> exit mapping. The default implementation
    /// returns <see cref="TigerCliValidationResult.Success"/>. Use the text-lookup helpers (such
    /// as <see cref="T"/>) to produce localized messages.
    /// </summary>
    public virtual TigerCliValidationResult Validate() => TigerCliValidationResult.Success();

    private static object?[] EscapeArgs(object?[] args)
    {
        return args
            .Select(arg => (object?)CliMarkupParser.Escape(
                Convert.ToString(arg, CultureInfo.InvariantCulture) ?? string.Empty))
            .ToArray();
    }
}
