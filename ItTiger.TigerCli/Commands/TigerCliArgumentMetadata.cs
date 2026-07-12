using System.Reflection;

namespace ItTiger.TigerCli.Commands;

internal sealed class TigerCliArgumentMetadata
{
    public PropertyInfo Property { get; }
    public int Index { get; }
    public string DisplayName { get; }
    public string? Description { get; }
    public string? DescriptionResourceKey { get; }
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
    public TigerCliPromptable? Promptable { get; }

    /// <summary>Whether a provider-backed prompt may bind the only selectable outcome without showing the select UI.</summary>
    public bool AutoSelectSingleChoice { get; }

    /// <summary>Whether the argument participates in edit-mode prompting and edit-mode provider validation. In normal commands provider validation is driven by an explicit provider instead.</summary>
    public bool Editable { get; }

    /// <summary>Whether the argument's effective value is validated against its provider's choices.</summary>
    public bool ValidateAgainstProvider { get; }

    /// <summary>How supplied values are matched against provider choices (case/path rules).</summary>
    public TigerCliValueMatchPreset ValueMatching { get; }

    public TigerCliArgumentMetadata(PropertyInfo property, TigerCliArgumentAttribute attribute)
    {
        Property = property;
        Index = attribute.Index;
        DisplayName = string.IsNullOrWhiteSpace(attribute.Name)
            ? DeriveDisplayName(property.Name)
            : attribute.Name;
        Description = attribute.Description;
        DescriptionResourceKey = attribute.DescriptionResourceKey;
        Provider = string.IsNullOrWhiteSpace(attribute.Provider) ? null : attribute.Provider;
        EditProvider = string.IsNullOrWhiteSpace(attribute.EditProvider) ? null : attribute.EditProvider;
        MinLength = attribute.MinLength >= 0 ? attribute.MinLength : null;
        MaxLength = attribute.MaxLength >= 0 ? attribute.MaxLength : null;
        MinValue = attribute.MinValue != int.MinValue ? attribute.MinValue : null;
        MaxValue = attribute.MaxValue != int.MaxValue ? attribute.MaxValue : null;
        MinValueProvider = string.IsNullOrWhiteSpace(attribute.MinValueProvider) ? null : attribute.MinValueProvider;
        MaxValueProvider = string.IsNullOrWhiteSpace(attribute.MaxValueProvider) ? null : attribute.MaxValueProvider;
        Promptable = attribute.PromptableValue;
        AutoSelectSingleChoice = attribute.AutoSelectSingleChoice;
        Editable = attribute.Editable;
        ValidateAgainstProvider = attribute.ValidateAgainstProvider;
        ValueMatching = attribute.ValueMatching;
    }

    private static string DeriveDisplayName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return "value";

        var result = new List<char>(propertyName.Length + 4);
        for (var i = 0; i < propertyName.Length; i++)
        {
            var ch = propertyName[i];
            if (i > 0 && char.IsUpper(ch))
                result.Add('-');
            result.Add(char.ToLowerInvariant(ch));
        }
        return new string(result.ToArray());
    }
}
