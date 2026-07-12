namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// A provider choice with a canonical key and a user-facing label.
/// </summary>
/// <remarks>
/// Providers return these items for provider-backed prompts and provider validation. The label is
/// displayed in select prompts; the key is the value bound back to the settings property when selected
/// or when a supplied value matches the choice. Keys must be non-null and unique within one provider
/// result.
/// </remarks>
/// <typeparam name="TKey">The type of the canonical value bound when the choice is selected.</typeparam>
/// <param name="Key">The canonical value for the choice.</param>
/// <param name="Label">The display label shown to the user.</param>
public readonly record struct OptionItem<TKey>(TKey Key, string Label);


