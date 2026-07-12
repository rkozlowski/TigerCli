using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui;

/// <summary>
/// The rich outcome of a <see cref="TigerTui"/> activity run: the <see cref="ActivityOutcome"/>, the
/// exact modal <see cref="DialogResultKind"/> it was derived from, the produced <see cref="Value"/>
/// (meaningful only when <see cref="ActivityOutcome.Completed"/>), and the <see cref="Exception"/>
/// (set only when <see cref="ActivityOutcome.Failed"/>).
/// </summary>
/// <remarks>
/// This is a rich result in the same spirit as <see cref="TigerTuiResult{T}"/>: it is never collapsed
/// to value-or-<c>null</c>. Operation failures are reported here as <see cref="ActivityOutcome.Failed"/>
/// with the captured <see cref="Exception"/> rather than being thrown.
/// </remarks>
public readonly record struct ActivityResult<T>
{
    /// <summary>Creates an activity result with its outcome, dialog result, value, and exception.</summary>
    /// <param name="outcome">The terminal outcome of the activity.</param>
    /// <param name="dialogResultKind">The modal result from which the outcome was derived.</param>
    /// <param name="value">The value produced by a completed activity.</param>
    /// <param name="exception">The exception captured from a failed activity.</param>
    public ActivityResult(
        ActivityOutcome outcome,
        DialogResultKind dialogResultKind,
        T? value = default,
        Exception? exception = null)
    {
        Outcome = outcome;
        DialogResultKind = dialogResultKind;
        Value = value;
        Exception = exception;
    }

    /// <summary>The terminal outcome of the run.</summary>
    public ActivityOutcome Outcome { get; }

    /// <summary>The modal result kind the outcome was derived from.</summary>
    public DialogResultKind DialogResultKind { get; }

    /// <summary>The produced value; only meaningful when <see cref="IsCompleted"/> is <c>true</c>.</summary>
    public T? Value { get; }

    /// <summary>The captured operation exception; set only when <see cref="Outcome"/> is <see cref="ActivityOutcome.Failed"/>.</summary>
    public Exception? Exception { get; }

    /// <summary>True when the operation completed successfully.</summary>
    public bool IsCompleted => Outcome == ActivityOutcome.Completed;

    /// <summary>Returns the value via <paramref name="value"/> when completed; otherwise the default.</summary>
    public bool TryGetValue(out T? value)
    {
        value = IsCompleted ? Value : default;
        return IsCompleted;
    }

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    public static ActivityResult<T> Completed(T? value, DialogResultKind kind) =>
        new(ActivityOutcome.Completed, kind, value);

    /// <summary>A failed result carrying <paramref name="exception"/>.</summary>
    public static ActivityResult<T> Failed(Exception exception, DialogResultKind kind) =>
        new(ActivityOutcome.Failed, kind, default, exception);
}
