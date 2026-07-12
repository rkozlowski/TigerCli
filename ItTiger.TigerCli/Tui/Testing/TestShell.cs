using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Windowing;

namespace ItTiger.TigerCli.Tui.Testing;

/// <summary>
/// Test shell that runs the real semi-interactive modal loop against a <see cref="TestTerminal"/>.
/// </summary>
public sealed class TestShell : ICliAppShell
{
    private readonly InlineShell _shell;
    private readonly ManualTuiClock? _manualClock;
    private readonly CancellationTokenSource _systemCts = new();

    /// <summary>Creates a test shell with a new in-memory terminal.</summary>
    /// <param name="viewportWidth">The simulated terminal width in cells.</param>
    /// <param name="viewportHeight">The simulated terminal height in cells.</param>
    /// <param name="viewportHeightPercent">The percentage of terminal height available to the modal viewport.</param>
    /// <param name="interactionMode">The interaction policy used by the shell.</param>
    /// <param name="culture">The UI culture, or <c>null</c> for <c>en-US</c>.</param>
    /// <param name="useManualClock">Whether timeout tests use a manually advanced clock.</param>
    public TestShell(
        int viewportWidth = 80,
        int viewportHeight = 24,
        int viewportHeightPercent = 100,
        TigerCliInteractionMode interactionMode = TigerCliInteractionMode.SemiInteractive,
        CultureInfo? culture = null,
        bool useManualClock = false)
        : this(new TestTerminal(viewportWidth, viewportHeight), viewportHeightPercent, interactionMode, culture, useManualClock)
    {
    }

    /// <summary>Creates a test shell over an existing in-memory terminal.</summary>
    /// <param name="terminal">The terminal used to simulate input and capture output.</param>
    /// <param name="viewportHeightPercent">The percentage of terminal height available to the modal viewport.</param>
    /// <param name="interactionMode">The interaction policy used by the shell.</param>
    /// <param name="culture">The UI culture, or <c>null</c> for <c>en-US</c>.</param>
    /// <param name="useManualClock">Whether timeout tests use a manually advanced clock.</param>
    public TestShell(
        TestTerminal terminal,
        int viewportHeightPercent = 100,
        TigerCliInteractionMode interactionMode = TigerCliInteractionMode.SemiInteractive,
        CultureInfo? culture = null,
        bool useManualClock = false)
    {
        Terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _manualClock = useManualClock ? new ManualTuiClock() : null;
        _shell = new InlineShell(Terminal, viewportHeightPercent, interactionMode, _manualClock, _systemCts.Token);
        Culture = culture ?? CultureInfo.GetCultureInfo("en-US");
        _shell.SetCulture(Culture);
    }

    /// <summary>The in-memory terminal used for simulated input and captured rendering.</summary>
    public TestTerminal Terminal { get; }

    /// <summary>
    /// True when this shell was created with <c>useManualClock: true</c>, so the modal
    /// inactivity timeout advances only via <see cref="AdvanceTime"/> rather than the
    /// wall clock. Use this for deterministic timeout/timer-reset tests.
    /// </summary>
    public bool UsesManualClock => _manualClock is not null;

    /// <summary>
    /// Advances the modal inactivity-timeout clock by <paramref name="delta"/> of virtual
    /// time. Only the timeout deadline reads this clock; input polling stays real-time, so
    /// combine this with <c>Terminal.WaitForInputDrainedAsync</c> to order key presses and
    /// time advances deterministically. Requires <c>useManualClock: true</c>.
    /// </summary>
    public void AdvanceTime(TimeSpan delta)
    {
        if (_manualClock is null)
            throw new InvalidOperationException(
                "TestShell was not created with a manual clock. Pass useManualClock: true to enable AdvanceTime.");

        _manualClock.Advance(delta);
    }

    /// <summary>
    /// Deterministic seam for process/system cancellation: trips the same system-cancellation token a
    /// real Ctrl-C / SIGINT / SIGTERM handler would, so a modal running on this shell completes with
    /// <see cref="DialogResultKind.SystemCancel"/> without raising a real OS signal.
    /// </summary>
    public void RaiseSystemCancellation() => _systemCts.Cancel();

    /// <inheritdoc/>
    public ITheme Theme => _shell.Theme;
    /// <inheritdoc/>
    public bool IsFullWindow => _shell.IsFullWindow;
    /// <inheritdoc/>
    public Size Viewport => _shell.Viewport;
    /// <inheritdoc/>
    public TigerCliInteractionMode InteractionMode => _shell.InteractionMode;
    /// <inheritdoc/>
    public CultureInfo Culture { get; }

    /// <inheritdoc/>
    public Task<DialogResult> RunModalAsync(ICliDialog dialog, CancellationToken ct = default)
    {
        return _shell.RunModalAsync(dialog, ct);
    }

    /// <inheritdoc/>
    public Task<DialogResult> RunModalAsync(ICliDialog dialog, TimeSpan? timeout = default, CancellationToken ct = default)
    {
        return _shell.RunModalAsync(dialog, timeout, ct);
    }
}
