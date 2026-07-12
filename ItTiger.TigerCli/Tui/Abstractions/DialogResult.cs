using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Abstractions;


/// <summary>
/// Result returned by a modal dialog, preserving the exact result kind and any optional payload.
/// </summary>
public readonly record struct DialogResult(DialogResultKind Kind, object? Payload);

