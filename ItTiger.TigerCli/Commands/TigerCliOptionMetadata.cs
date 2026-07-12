using System.Reflection;

namespace ItTiger.TigerCli.Commands;

internal sealed class TigerCliOptionMetadata
{
    public PropertyInfo Property { get; }
    public string[] Aliases { get; }
    public string? Description { get; }
    public string? DescriptionResourceKey { get; }
    public OptionValueKind ValueKind { get; }

    /// <summary>True if the option consumes a value token; false for no-value switches.</summary>
    public bool TakesValue { get; }

    /// <summary>True if the underlying property type is an enum (or nullable enum).</summary>
    public bool IsEnum { get; }

    /// <summary>Enum member names when <see cref="IsEnum"/> is true; otherwise empty.</summary>
    public string[] EnumValues { get; }

    /// <summary>Enum member names with <see cref="ForbiddenValues"/> excluded. Used for help display.</summary>
    public string[] FilteredEnumValues { get; }

    /// <summary>True if this is a repeated (collection) option.</summary>
    public bool IsRepeatable { get; }

    /// <summary>When true, the option must be explicitly provided on the command line.</summary>
    public bool Required { get; }

    /// <summary>Controls whether TigerCli may prompt for this option when missing.</summary>
    public TigerCliPromptable? Promptable { get; }

    /// <summary>Whether a provider-backed prompt may bind the only selectable outcome without showing the select UI.</summary>
    public bool AutoSelectSingleChoice { get; }

    /// <summary>Optional named provider key used by provider-backed prompting.</summary>
    public string? Provider { get; }

    /// <summary>
    /// Optional named provider key used only in edit mode; falls back to <see cref="Provider"/>
    /// when not configured. Ignored in add/normal commands. Null when empty/whitespace.
    /// </summary>
    public string? EditProvider { get; }

    public int? MinLength { get; }
    public int? MaxLength { get; }
    public int? MinValue { get; }
    public int? MaxValue { get; }
    public string? MinValueProvider { get; }
    public string? MaxValueProvider { get; }
    public string? RequiredWhenOption { get; }
    public string? RequiredWhenValue { get; }
    public string[]? RequiredWhenValueIn { get; }
    public string[]? RequiredWhenValueNotIn { get; }
    public string? PromptWhenOption { get; }
    public string? PromptWhenValue { get; }
    public string[]? PromptWhenValueIn { get; }
    public string[]? PromptWhenValueNotIn { get; }
    public string? DependsOnOption { get; }
    public string[] DependsOnOptions { get; }
    public bool Secret { get; }
    public bool AllowCommandLineValue { get; }

    /// <summary>Whether the option participates in edit prompting and provider validation.</summary>
    public bool Editable { get; }

    /// <summary>Whether the option's effective value is validated against its provider's choices.</summary>
    public bool ValidateAgainstProvider { get; }

    /// <summary>How supplied values are matched against provider choices (case/path rules).</summary>
    public TigerCliValueMatchPreset ValueMatching { get; }

    /// <summary>
    /// True when the property carries <see cref="TigerCliFolderSelectAttribute"/>, so a missing value
    /// is prompted with the inline folder picker instead of a plain text prompt.
    /// </summary>
    public bool UseFolderPicker { get; }

    /// <summary>
    /// True when the property carries <see cref="TigerCliMultiSelectAttribute"/>, so the option is a
    /// provider-backed multi-select (checklist prompt; comma/repeat non-interactive parsing).
    /// </summary>
    public bool UseMultiSelect { get; }

    /// <summary>Element type of the multi-select collection (e.g. <c>string</c>, <c>long</c>, <c>Guid</c>). Null when not a multi-select.</summary>
    public Type? MultiSelectElementType { get; }

    /// <summary>True when the multi-select property is an array (<c>T[]</c>); false for <c>List&lt;T&gt;</c>.</summary>
    public bool MultiSelectIsArray { get; }

    /// <summary>Whether command-line tokens that do not match a provider choice are kept verbatim (string collections only).</summary>
    public bool MultiSelectAllowCustomValues { get; }

    /// <summary>Whether a zero-length multi-select result is accepted.</summary>
    public bool MultiSelectAllowEmpty { get; }

    /// <summary>Values that are not accepted. Used to exclude enum sentinels from validation and help.</summary>
    public object[]? ForbiddenValues { get; }

    /// <summary>
    /// The value placeholder to show in help, e.g. "connection-string" or "Silent|Quiet|Normal".
    /// Null for no-value switches.
    /// </summary>
    public string? ValuePlaceholder { get; }

    /// <summary>
    /// Explicit ValueName from the attribute, if specified.
    /// </summary>
    public string? ExplicitValueName { get; }

    public TigerCliOptionMetadata(PropertyInfo property, TigerCliOptionAttribute attribute)
    {
        Property = property;
        Aliases = attribute.Aliases;
        Description = attribute.Description;
        DescriptionResourceKey = attribute.DescriptionResourceKey;
        ExplicitValueName = attribute.ValueName;
        Required = attribute.Required;
        Promptable = attribute.PromptableValue;
        AutoSelectSingleChoice = attribute.AutoSelectSingleChoice;
        Provider = string.IsNullOrWhiteSpace(attribute.Provider) ? null : attribute.Provider;
        EditProvider = string.IsNullOrWhiteSpace(attribute.EditProvider) ? null : attribute.EditProvider;
        MinLength = attribute.MinLength >= 0 ? attribute.MinLength : null;
        MaxLength = attribute.MaxLength >= 0 ? attribute.MaxLength : null;
        MinValue = attribute.MinValue != int.MinValue ? attribute.MinValue : null;
        MaxValue = attribute.MaxValue != int.MaxValue ? attribute.MaxValue : null;
        MinValueProvider = string.IsNullOrWhiteSpace(attribute.MinValueProvider) ? null : attribute.MinValueProvider;
        MaxValueProvider = string.IsNullOrWhiteSpace(attribute.MaxValueProvider) ? null : attribute.MaxValueProvider;
        RequiredWhenOption = string.IsNullOrWhiteSpace(attribute.RequiredWhenOption) ? null : attribute.RequiredWhenOption;
        RequiredWhenValue = attribute.RequiredWhenValue;
        RequiredWhenValueIn = attribute.RequiredWhenValueIn;
        RequiredWhenValueNotIn = attribute.RequiredWhenValueNotIn;
        PromptWhenOption = string.IsNullOrWhiteSpace(attribute.PromptWhenOption) ? null : attribute.PromptWhenOption;
        PromptWhenValue = attribute.PromptWhenValue;
        PromptWhenValueIn = attribute.PromptWhenValueIn;
        PromptWhenValueNotIn = attribute.PromptWhenValueNotIn;
        DependsOnOption = string.IsNullOrWhiteSpace(attribute.DependsOnOption) ? null : attribute.DependsOnOption;
        DependsOnOptions = NormalizeDependencyNames(attribute.DependsOnOptions);
        Secret = attribute.Secret;
        AllowCommandLineValue = attribute.AllowCommandLineValue;
        Editable = attribute.Editable;
        ValidateAgainstProvider = attribute.ValidateAgainstProvider;
        ValueMatching = attribute.ValueMatching;
        ForbiddenValues = attribute.ForbiddenValues;
        ValueKind = ResolveValueKind(property.PropertyType);

        UseFolderPicker = property.GetCustomAttribute<TigerCliFolderSelectAttribute>() != null;
        if (UseFolderPicker)
        {
            var folderUnderlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (folderUnderlying != typeof(string))
                throw new InvalidOperationException(
                    $"[TigerCliFolderSelect] on '{property.DeclaringType?.Name}.{property.Name}' requires a string or string? property, but the property type is '{property.PropertyType.Name}'.");
        }

        var multiSelectAttribute = property.GetCustomAttribute<TigerCliMultiSelectAttribute>();
        UseMultiSelect = multiSelectAttribute != null;
        if (multiSelectAttribute != null)
        {
            if (UseFolderPicker)
                throw new InvalidOperationException(
                    $"[TigerCliMultiSelect] on '{property.DeclaringType?.Name}.{property.Name}' cannot be combined with [TigerCliFolderSelect].");

            (MultiSelectElementType, MultiSelectIsArray) = ResolveMultiSelectElementType(property);
            MultiSelectAllowCustomValues = multiSelectAttribute.AllowCustomValues;
            MultiSelectAllowEmpty = multiSelectAttribute.AllowEmpty;

            if (MultiSelectAllowCustomValues && MultiSelectElementType != typeof(string))
                throw new InvalidOperationException(
                    $"[TigerCliMultiSelect(AllowCustomValues = true)] on '{property.DeclaringType?.Name}.{property.Name}' is only valid for string collections, but the element type is '{MultiSelectElementType!.Name}'.");

            // A multi-select is its own value kind so binding and validation take the checklist path
            // instead of the generic repeated-scalar/key-value paths.
            ValueKind = OptionValueKind.MultiSelect;
        }

        var underlyingType = GetUnderlyingType(property.PropertyType);
        IsEnum = underlyingType.IsEnum;
        EnumValues = IsEnum ? Enum.GetNames(underlyingType) : [];
        IsRepeatable = ValueKind is OptionValueKind.RepeatedScalar or OptionValueKind.RepeatedKeyValue or OptionValueKind.MultiSelect;

        // Filter enum values to exclude forbidden ones for help display
        if (IsEnum && ForbiddenValues is { Length: > 0 })
        {
            var forbiddenNames = ForbiddenValues
                .Select(v => v.ToString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            FilteredEnumValues = EnumValues.Where(v => !forbiddenNames.Contains(v)).ToArray();
        }
        else
        {
            FilteredEnumValues = EnumValues;
        }

        // Determine if option takes a value
        TakesValue = ResolveTakesValue(property.PropertyType);

        // Build the value placeholder for help rendering
        ValuePlaceholder = TakesValue ? ResolveValuePlaceholder(attribute.ValueName, underlyingType, FilteredEnumValues) : null;
    }

    private static string[] NormalizeDependencyNames(string[]? names) =>
        names?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray()
        ?? [];

    /// <summary>
    /// Gets the default value of this option from a fresh settings instance.
    /// Returns null when the default is not meaningful for help display.
    /// </summary>
    public string? GetDefaultDisplayValue(TigerCliSettings defaultInstance)
    {
        // Required options never show a default in help
        if (Required || Secret)
            return null;

        var value = Property.GetValue(defaultInstance);

        // No-value bool switches: don't show "Default: False"
        if (!TakesValue)
            return null;

        if (value == null)
            return null;

        // Suppress default if it is a forbidden value
        if (ForbiddenValues is { Length: > 0 } && ForbiddenValues.Any(fv => fv.Equals(value)))
            return null;

        // For enums, show the name
        var underlyingType = GetUnderlyingType(Property.PropertyType);
        if (underlyingType.IsEnum)
            return value.ToString();

        // For strings, only show if non-empty
        if (value is string s)
            return string.IsNullOrEmpty(s) ? null : s;

        // For collections, skip
        if (IsRepeatable)
            return null;

        return null;
    }

    private static bool ResolveTakesValue(Type propertyType)
    {
        // Non-nullable bool is a no-value switch
        if (propertyType == typeof(bool))
            return false;

        // Everything else takes a value (including bool?)
        return true;
    }

    private static string ResolveValuePlaceholder(string? explicitValueName, Type underlyingType, string[] filteredEnumValues)
    {
        // Explicit ValueName wins
        if (!string.IsNullOrEmpty(explicitValueName))
            return explicitValueName;

        // Enum: show filtered member names (excludes forbidden values)
        if (underlyingType.IsEnum)
            return string.Join("|", filteredEnumValues);

        // Nullable bool: show true|false
        // (underlyingType is already the unwrapped type)
        if (underlyingType == typeof(bool))
            return "true|false";

        // Generic fallback
        return "value";
    }

    private static Type GetUnderlyingType(Type type)
    {
        // Unwrap Nullable<T>
        var inner = Nullable.GetUnderlyingType(type);
        if (inner != null)
            return inner;

        // Unwrap collection element types
        if (type == typeof(string[]))
            return typeof(string);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = type.GetGenericArguments()[0];
            if (elementType.IsGenericType &&
                elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                return typeof(string); // key-value pair doesn't have a single underlying enum
            return elementType;
        }

        return type;
    }

    private static readonly HashSet<Type> SupportedMultiSelectElementTypes =
    [
        typeof(string), typeof(int), typeof(short), typeof(long), typeof(Guid)
    ];

    private static (Type ElementType, bool IsArray) ResolveMultiSelectElementType(PropertyInfo property)
    {
        var type = property.PropertyType;
        Type? elementType = null;
        var isArray = false;

        if (type.IsArray)
        {
            elementType = type.GetElementType();
            isArray = true;
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            elementType = type.GetGenericArguments()[0];
        }

        if (elementType == null || !SupportedMultiSelectElementTypes.Contains(elementType))
            throw new InvalidOperationException(
                $"[TigerCliMultiSelect] on '{property.DeclaringType?.Name}.{property.Name}' requires a List<T> or T[] property where T is string, int, short, long, or Guid, but the property type is '{property.PropertyType.Name}'.");

        return (elementType, isArray);
    }

    private static OptionValueKind ResolveValueKind(Type type)
    {
        if (type == typeof(string[]))
            return OptionValueKind.RepeatedScalar;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = type.GetGenericArguments()[0];

            if (elementType == typeof(string))
                return OptionValueKind.RepeatedScalar;

            if (elementType.IsGenericType &&
                elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                return OptionValueKind.RepeatedKeyValue;
            }
        }

        return OptionValueKind.Scalar;
    }
}

internal enum OptionValueKind
{
    Scalar,
    RepeatedScalar,
    RepeatedKeyValue,
    MultiSelect
}
