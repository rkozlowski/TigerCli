using System;

namespace ItTiger.Core;

/// <summary>
/// Provides display text and optional localized resource keys for TigerCli-facing enum values,
/// settings properties, and command metadata.
/// </summary>
/// <remarks>
/// TigerCli uses this metadata when it needs a human-facing label or description while keeping
/// code identifiers stable. Set <see cref="ResourceKey"/> or <see cref="DescriptionResourceKey"/>
/// when the text should be resolved from application resources for the active culture.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Enum |
    AttributeTargets.Field |
    AttributeTargets.Property |
    AttributeTargets.Class,
    AllowMultiple = false)]
public sealed class TigerTextAttribute : Attribute
{
    /// <summary>Creates metadata with the default display text.</summary>
    public TigerTextAttribute(string text)
    {
        Text = text;
    }

    /// <summary>Default display text used when no resource key is supplied or resolved.</summary>
    public string Text { get; }

    /// <summary>Optional resource key for the localized display text.</summary>
    public string? ResourceKey { get; set; }

    /// <summary>Optional default description text.</summary>
    public string? Description { get; set; }

    /// <summary>Optional resource key for the localized description text.</summary>
    public string? DescriptionResourceKey { get; set; }
}

