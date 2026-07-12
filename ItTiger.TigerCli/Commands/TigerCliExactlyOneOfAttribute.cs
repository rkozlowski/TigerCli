namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Declares that exactly one of the listed option properties must be provided or resolved.
/// </summary>
/// <remarks>
/// Apply this to a <see cref="TigerCliSettings"/>-derived class. Each named property must be a
/// <see cref="TigerCliOptionAttribute"/> property. Validation runs after prompting, so a value supplied
/// by an allowed prompt counts as present; in non-interactive mode no prompt fills missing values, so
/// none or multiple supplied options fail validation. The optional <see cref="Description"/> is used as
/// both the help note and the validation message.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TigerCliExactlyOneOfAttribute : Attribute
{
    /// <summary>The option property names that form the mutually exclusive group.</summary>
    public string[] PropertyNames { get; }

    /// <summary>
    /// Optional description override for the validation error and help note.
    /// When null, a default message is generated from the option aliases.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Declares that exactly one of the specified settings properties must be provided.
    /// </summary>
    /// <param name="propertyNames">
    /// Two or more property names (use <c>nameof(...)</c>) that form the mutually exclusive group.
    /// </param>
    public TigerCliExactlyOneOfAttribute(params string[] propertyNames)
    {
        if (propertyNames.Length < 2)
            throw new ArgumentException(
                "An ExactlyOneOf group must contain at least two property names.",
                nameof(propertyNames));

        PropertyNames = propertyNames;
    }
}
