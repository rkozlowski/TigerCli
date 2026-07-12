namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// The safe, thread-safe surface a background activity operation uses to report progress. It exposes
/// only value updates — never rendering primitives, widgets, or the grid. Updates are coalesced and
/// applied to the UI on the modal-loop thread; the operation never renders or mutates widgets directly.
/// </summary>
public sealed class ActivityContext
{
    private readonly ActivityState _state;

    internal ActivityContext(ActivityState state)
    {
        _state = state;
    }

    /// <summary>Sets a single value of a dynamic row by index.</summary>
    public void SetValue(string row, int index, object? value) => _state.SetValue(row, index, value);

    /// <summary>Replaces all values of a dynamic row (count must match the row's declared length).</summary>
    public void SetValues(string row, params object?[] values) => _state.SetValues(row, values);

    /// <summary>Convenience for a single-value text row: sets value 0 of <paramref name="row"/>.</summary>
    public void SetMessage(string row, string text) => _state.SetValue(row, 0, text);
}
