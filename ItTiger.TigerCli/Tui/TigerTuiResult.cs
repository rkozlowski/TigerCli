using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui;

/// <summary>
/// The rich outcome of a TigerCli modal prompt: the exact <see cref="DialogResultKind"/> the modal
/// loop reported, plus the produced <see cref="Value"/> (meaningful only when the prompt completed
/// with <see cref="DialogResultKind.Ok"/>).
/// </summary>
/// <remarks>
/// This is the framework-internal primitive. TigerCli internals branch on <see cref="ResultKind"/> so
/// they can tell the cancellation flavours apart (e.g. <see cref="DialogResultKind.Cancel"/> vs
/// <see cref="DialogResultKind.TokenCancel"/> vs <see cref="DialogResultKind.Timeout"/>). The simple
/// <c>TigerTui</c> helpers are adapters over this type and collapse it at the outer edge (e.g. to
/// value-or-<c>null</c>), preserving their established observable behavior.
/// </remarks>
public readonly record struct TigerTuiResult<T>
{
    /// <summary>Creates a prompt result with its exact dialog outcome and optional value.</summary>
    /// <param name="resultKind">The dialog result reported by the modal loop.</param>
    /// <param name="value">The value produced by a successful prompt.</param>
    public TigerTuiResult(DialogResultKind resultKind, T? value = default)
    {
        ResultKind = resultKind;
        Value = value;
    }

    /// <summary>The exact dialog result kind reported by the modal loop.</summary>
    public DialogResultKind ResultKind { get; }

    /// <summary>The produced value; only meaningful when <see cref="IsOk"/> is <c>true</c>.</summary>
    public T? Value { get; }

    /// <summary>True when the prompt completed successfully (<see cref="DialogResultKind.Ok"/>).</summary>
    public bool IsOk => ResultKind == DialogResultKind.Ok;

    /// <summary>Returns the value via <paramref name="value"/> when <see cref="IsOk"/>; otherwise the default.</summary>
    public bool TryGetValue(out T? value)
    {
        value = IsOk ? Value : default;
        return IsOk;
    }

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    public static TigerTuiResult<T> Ok(T? value) => new(DialogResultKind.Ok, value);

    /// <summary>A non-success result carrying only the <paramref name="kind"/> (no value).</summary>
    public static TigerTuiResult<T> FromKind(DialogResultKind kind) => new(kind);
}
