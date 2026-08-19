using System.Reflection;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Resolved metadata for a settings property marked with <see cref="TigerCliRawArgumentsAttribute"/>.
/// Constructing it validates the declaration, so configuration mistakes surface where the app is
/// built rather than while a command line is being parsed.
/// </summary>
internal sealed class TigerCliRawArgumentsMetadata
{
    public PropertyInfo Property { get; }
    public string DisplayName { get; }
    public string? Description { get; }
    public string? DescriptionResourceKey { get; }
    public bool Required { get; }

    /// <summary>Whether the target property is <c>string[]</c> rather than a list type.</summary>
    public bool IsArray { get; }

    public TigerCliRawArgumentsMetadata(PropertyInfo property, TigerCliRawArgumentsAttribute attribute)
    {
        var owner = $"{property.DeclaringType?.Name}.{property.Name}";

        if (property.GetCustomAttribute<TigerCliOptionAttribute>() != null
            || property.GetCustomAttribute<TigerCliArgumentAttribute>() != null)
        {
            throw new InvalidOperationException(
                $"[TigerCliRawArguments] on '{owner}' cannot be combined with [TigerCliOption] or [TigerCliArgument].");
        }

        if (property.SetMethod == null)
            throw new InvalidOperationException(
                $"[TigerCliRawArguments] on '{owner}' requires a writable property.");

        var propertyType = property.PropertyType;
        if (propertyType == typeof(string[]))
        {
            IsArray = true;
        }
        else if (propertyType == typeof(List<string>) || propertyType == typeof(IReadOnlyList<string>))
        {
            IsArray = false;
        }
        else
        {
            throw new InvalidOperationException(
                $"[TigerCliRawArguments] on '{owner}' requires a string[], List<string>, or IReadOnlyList<string> property, "
                + $"but the property type is '{propertyType.Name}'.");
        }

        Property = property;
        DisplayName = string.IsNullOrWhiteSpace(attribute.Name)
            ? DeriveDisplayName(property.Name)
            : attribute.Name;
        Description = attribute.Description;
        DescriptionResourceKey = attribute.DescriptionResourceKey;
        Required = attribute.Required;
    }

    /// <summary>
    /// Builds the value to assign to the target property. Arrays bind the token array directly;
    /// list-typed properties bind a <see cref="List{T}"/>, which also satisfies
    /// <see cref="IReadOnlyList{T}"/>.
    /// </summary>
    public object CreateValue(IReadOnlyList<string> tokens) =>
        IsArray ? tokens.ToArray() : new List<string>(tokens);

    private static string DeriveDisplayName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return "args";

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
