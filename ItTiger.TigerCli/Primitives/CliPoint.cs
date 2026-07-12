namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// A zero-based (column, row) position within a <see cref="ItTiger.TigerCli.Rendering.CliGrid"/>.
/// </summary>
public readonly record struct CliPoint(int Column, int Row);
