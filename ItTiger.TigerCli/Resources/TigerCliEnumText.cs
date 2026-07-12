using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using ItTiger.Core;

namespace ItTiger.TigerCli.Resources;

/// <summary>
/// Resolves the display label and optional description for an enum type or
/// enum member, walking the app-owned text precedence chain:
///   TigerTextAttribute (ResourceKey/source text -> Text)
///   DisplayAttribute (with optional ResourceType)
///   DescriptionAttribute (description only)
///   member/type name (label-only fallback).
///
/// All app-resource lookups use the explicit <see cref="CultureInfo"/> supplied
/// by the caller. DisplayAttribute.ResourceType is resolved via a tiny scoped
/// <see cref="CultureInfo.CurrentUICulture"/> swap that is restored in a
/// <c>finally</c> block; the framework otherwise never mutates that global.
/// </summary>
internal static class TigerCliEnumText
{
    public readonly record struct ResolvedText(string Label, string? Description);

    /// <summary>
    /// Resolves text for the enum type itself (used by the exit-code help heading).
    /// </summary>
    public static ResolvedText Resolve(Type enumType, CultureInfo culture, ResourceManager? appResources)
    {
        var tigerText = enumType.GetCustomAttribute<TigerTextAttribute>(inherit: false);
        var display = enumType.GetCustomAttribute<DisplayAttribute>(inherit: false);
        var description = enumType.GetCustomAttribute<DescriptionAttribute>(inherit: false);

        var label = ResolveLabel(
                tigerText,
                display,
                culture,
                appResources,
                fallback: enumType.Name);

        var desc = ResolveDescription(
                tigerText,
                display,
                description,
                culture,
                appResources);

        return new ResolvedText(label, desc);
    }

    /// <summary>
    /// Resolves text for an enum field/member (used for exit-code rows and prompt labels).
    /// </summary>
    public static ResolvedText Resolve(FieldInfo field, CultureInfo culture, ResourceManager? appResources)
    {
        var tigerText = field.GetCustomAttribute<TigerTextAttribute>(inherit: false);
        var display = field.GetCustomAttribute<DisplayAttribute>(inherit: false);
        var description = field.GetCustomAttribute<DescriptionAttribute>(inherit: false);

        var label = ResolveLabel(
                tigerText,
                display,
                culture,
                appResources,
                fallback: field.Name);

        var desc = ResolveDescription(
                tigerText,
                display,
                description,
                culture,
                appResources);

        return new ResolvedText(label, desc);
    }

    /// <summary>
    /// Resolves a label for a specific enum value. Returns the value's
    /// <see cref="Enum.ToString()"/> when no attributes are present.
    /// </summary>
    public static string ResolveMemberLabel(Type enumType, object enumValue, CultureInfo culture, ResourceManager? appResources)
    {
        var memberName = enumValue.ToString() ?? string.Empty;
        var field = enumType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
            return memberName;
        return Resolve(field, culture, appResources).Label;
    }

    // ── Label chain ────────────────────────────────────────────────
    // TigerText.ResourceKey or TigerText.Text (app resources) -> TigerText.Text -> Display.GetName() -> fallback (member/type name)
    // Note: DescriptionAttribute intentionally NOT in the label chain (description-only,
    // preserves existing --help-errors output where label = enum NAME and DescriptionAttribute
    // text appears as a separate description line).
    private static string ResolveLabel(
        TigerTextAttribute? tigerText,
        DisplayAttribute? display,
        CultureInfo culture,
        ResourceManager? appResources,
        string fallback)
    {
        if (tigerText != null)
        {
            if (!string.IsNullOrEmpty(tigerText.Text))
            {
                var resourceKey = !string.IsNullOrEmpty(tigerText.ResourceKey)
                    ? tigerText.ResourceKey
                    : tigerText.Text;
                var resolved = LookupAppResource(appResources, resourceKey, culture);
                return !string.IsNullOrEmpty(resolved)
                    ? resolved
                    : tigerText.Text;
            }
        }

        if (display != null)
        {
            var name = ResolveDisplayName(display, culture);
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        return fallback;
    }

    // ── Description chain ──────────────────────────────────────────
    // TigerText.DescriptionResourceKey or TigerText.Description -> TigerText.Description -> Display.GetDescription() -> DescriptionAttribute.Description -> null.
    private static string? ResolveDescription(
        TigerTextAttribute? tigerText,
        DisplayAttribute? display,
        DescriptionAttribute? description,
        CultureInfo culture,
        ResourceManager? appResources)
    {
        if (tigerText != null)
        {
            if (!string.IsNullOrEmpty(tigerText.Description))
            {
                var resourceKey = !string.IsNullOrEmpty(tigerText.DescriptionResourceKey)
                    ? tigerText.DescriptionResourceKey
                    : tigerText.Description;
                var resolved = LookupAppResource(appResources, resourceKey, culture);
                return !string.IsNullOrEmpty(resolved)
                    ? resolved
                    : tigerText.Description;
            }
        }

        if (display != null)
        {
            var desc = ResolveDisplayDescription(display, culture);
            if (!string.IsNullOrEmpty(desc))
                return desc;
        }

        if (description != null && !string.IsNullOrEmpty(description.Description))
            return description.Description;

        return null;
    }

    private static string? LookupAppResource(ResourceManager? rm, string key, CultureInfo culture)
    {
        if (rm == null)
            return null;
        try
        {
            return rm.GetString(key, culture);
        }
        catch
        {
            return null;
        }
    }

    // DisplayAttribute.GetName/GetDescription only honor the global CurrentUICulture when
    // ResourceType is set. To target the run's resolved culture without leaking thread state,
    // swap CurrentUICulture for the duration of the call only.
    private static string? ResolveDisplayName(DisplayAttribute display, CultureInfo culture)
    {
        if (display.ResourceType == null)
            return display.GetName();
        return WithCultureScope(culture, display.GetName);
    }

    private static string? ResolveDisplayDescription(DisplayAttribute display, CultureInfo culture)
    {
        if (display.ResourceType == null)
            return display.GetDescription();
        return WithCultureScope(culture, display.GetDescription);
    }

    private static string? WithCultureScope(CultureInfo culture, Func<string?> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
