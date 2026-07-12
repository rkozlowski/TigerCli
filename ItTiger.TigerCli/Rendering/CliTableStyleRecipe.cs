using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// A colour-free table style "recipe": the structural and role-based definition of a table preset
/// (frame configuration, padding, surface role, and title/frame accent roles). A recipe does not
/// hard-code colours — it references theme roles and surfaces and resolves to a concrete
/// <see cref="CliTableStyle"/> through an <see cref="ITheme"/>.
///
/// <para>The built-in "city" recipes are exposed as static properties (e.g. <see cref="Roma"/>).
/// Because this is a <c>record</c>, callers can derive their own from a city recipe with a
/// <c>with</c> expression, then resolve it:
/// <code>
/// public static readonly CliTableStyleRecipe InvoiceRecipe =
///     CliTableStyleRecipe.Roma with { Surface = SurfaceRole.Panel, TitleAccent = TableAccent.Success };
/// // ...
/// table.ApplyStyle(InvoiceRecipe.Resolve(theme));
/// </code></para>
///
/// <para>Table styles never use <see cref="ThemeStyle.DialogSurface"/>. An elevated table surface comes
/// from <see cref="SurfaceRole.Panel"/>; dialogs/controls keep their own <c>DialogSurface</c>.</para>
/// </summary>
public sealed record CliTableStyleRecipe
{
    /// <summary>Outer frame segment style.</summary>
    public CliFrameSegmentStyle Outer { get; init; } = CliFrameSegmentStyle.None;

    /// <summary>Frame segment style for the rule below the header.</summary>
    public CliFrameSegmentStyle AfterHeader { get; init; } = CliFrameSegmentStyle.None;

    /// <summary>Frame segment style between columns (vertical) / fields.</summary>
    public CliFrameSegmentStyle BetweenElements { get; init; } = CliFrameSegmentStyle.None;

    /// <summary>Frame segment style between records (zebra rows).</summary>
    public CliFrameSegmentStyle BetweenRecords { get; init; } = CliFrameSegmentStyle.None;

    /// <summary>Junction/join rendering style for the frame.</summary>
    public CliFrameJoinStyle Join { get; init; } = CliFrameJoinStyle.SimplifiedCompatible;

    /// <summary>Body-cell padding.</summary>
    public CliCellPadding Padding { get; init; } = CliCellPadding.Both;

    /// <summary>Optional header padding; when <c>null</c>, header padding matches <see cref="Padding"/>.</summary>
    public CliCellPadding? HeaderPadding { get; init; }

    /// <summary>The surface family the table body sits on (resolved via <see cref="ITheme.ResolveSurface"/>).</summary>
    public SurfaceRole Surface { get; init; } = SurfaceRole.Default;

    /// <summary>The accent role used for the title foreground.</summary>
    public TableAccent TitleAccent { get; init; } = TableAccent.Default;

    /// <summary>The accent role used for the header foreground; when <see cref="TableAccent.Default"/>, the theme's <see cref="ThemeStyle.TableHeaderForeground"/> is used.</summary>
    public TableAccent HeaderAccent { get; init; } = TableAccent.Default;

    /// <summary>The accent role used for the frame foreground.</summary>
    public TableAccent FrameAccent { get; init; } = TableAccent.Default;

    /// <summary>Which orientations this recipe targets; single-orientation recipes clamp to theirs.</summary>
    public CliTableStyleOrientationSupport OrientationSupport { get; init; } = CliTableStyleOrientationSupport.Both;

    /// <summary>Whether resolved styles should enable alternate-record rendering by default.</summary>
    public bool AlternateRecordsEnabled { get; init; }

    /// <summary>
    /// Resolves this recipe into a concrete <see cref="CliTableStyle"/> using <paramref name="theme"/>
    /// (the <see cref="TigerConsole.CurrentTheme"/> when <c>null</c>). The requested
    /// <paramref name="orientation"/> is honoured for universal recipes and ignored for
    /// orientation-locked ones. The title is rendered on the base (Default) surface, never on the
    /// table body surface; only the title foreground varies.
    /// </summary>
    public CliTableStyle Resolve(ITheme? theme = null,
        CliTableOrientation orientation = CliTableOrientation.Vertical)
    {
        theme ??= TigerConsole.CurrentTheme;

        var resolvedOrientation = OrientationSupport switch
        {
            CliTableStyleOrientationSupport.VerticalOnly => CliTableOrientation.Vertical,
            CliTableStyleOrientationSupport.HorizontalOnly => CliTableOrientation.Horizontal,
            _ => orientation
        };

        // Table ink (foregrounds) come from theme roles, not from the recipe.
        var bodyFg = theme.Resolve(ThemeStyle.TableBodyForeground).CharStyle?.Foreground;
        var rawHeaderFg = theme.Resolve(ThemeStyle.TableHeaderForeground).CharStyle?.Foreground;
        var headerFg = ResolveAccent(theme, HeaderAccent, rawHeaderFg);
        var defaultFrameFg = theme.Resolve(ThemeStyle.TableFrameForeground).CharStyle?.Foreground;
        var defaultTitleFg = theme.Resolve(ThemeStyle.TableTitleForeground).CharStyle?.Foreground;

        // Surface backgrounds. Titles always sit on the base (Default) surface.
        var surface = theme.ResolveSurface(Surface);
        var surfaceBg = surface.Background;
        var baseBackground = theme.ResolveSurface(SurfaceRole.Default).Background;

        var frameFg = ResolveAccent(theme, FrameAccent, defaultFrameFg);
        var titleFg = ResolveAccent(theme, TitleAccent, defaultTitleFg);

        // Alternate (zebra) record style, derived from the resolved surface family. Affects data
        // cells only — never the title, header, frame, or base surface.
        CliCellStyle? altStyle = surface.AltBackground is { } altBg
            ? new CliCellStyle(new CliCharStyle(surface.AltForeground ?? bodyFg, altBg)) { Padding = Padding }
            : null;

        var frame = new CliTableFrameConfig
        {
            JoinStyle = Join,
            OuterFrame = new CliFrameSegment(Outer),
            AfterHeader = new CliFrameSegment(AfterHeader),
            BeforeFooter = new CliFrameSegment(CliFrameSegmentStyle.None),
            BetweenElements = new CliFrameSegment(BetweenElements),
            BetweenRecords = new CliFrameSegment(BetweenRecords),
            CharStyle = new CliCharStyle(frameFg, surfaceBg),
        };

        return new CliTableStyle
        {
            Orientation = resolvedOrientation,
            OrientationSupport = this.OrientationSupport,
            FrameConfig = frame,
            DefaultCellStyle = new CliCellStyle(new CliCharStyle(bodyFg, surfaceBg))
            {
                Padding = Padding,
            },
            HeaderStyle = new CliCellStyle(new CliCharStyle(headerFg, surfaceBg))
            {
                HorizontalAlignment = resolvedOrientation == CliTableOrientation.Horizontal
                    ? CliTextAlignment.Left
                    : CliTextAlignment.Center,
                Padding = HeaderPadding ?? Padding,
            },
            // Title sits outside the surface: themed title foreground on the base background.
            TitleStyle = new CliCellStyle(new CliCharStyle(titleFg, baseBackground))
            {
                FormattingMode = CliFormattingMode.Preformatted,
                HorizontalAlignment = CliTextAlignment.Center,
                Padding = Padding,
            },
            DataStyle = null,
            DataAltStyle = altStyle,
            AlternateRecordsEnabled = this.AlternateRecordsEnabled,
        };
    }

    private static CliColor? ResolveAccent(ITheme theme, TableAccent accent, CliColor? fallback) => accent switch
    {
        TableAccent.Accent => theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground,
        TableAccent.Success => theme.Resolve(ThemeStyle.Success).CharStyle?.Foreground,
        TableAccent.Warning => theme.Resolve(ThemeStyle.Warning).CharStyle?.Foreground,
        _ => fallback
    };

    // ---- Built-in "city" recipes. Source of truth: TableTasting final variants. ----

    /// <summary>Roma — double outer frame, single header rule and column separators, panel surface, accent title. Universal.</summary>
    public static CliTableStyleRecipe Roma { get; } = new()
    {
        Outer = CliFrameSegmentStyle.DoubleFrame,
        AfterHeader = CliFrameSegmentStyle.SingleFrame,
        BetweenElements = CliFrameSegmentStyle.SingleFrame,
        BetweenRecords = CliFrameSegmentStyle.None,
        Padding = CliCellPadding.Both,
        Surface = SurfaceRole.Panel,
        TitleAccent = TableAccent.Accent,
        OrientationSupport = CliTableStyleOrientationSupport.Both,
    };

    /// <summary>Milano — clean single-line boxed grid on a panel surface, success (green) title, warning (yellow) header. Universal.</summary>
    public static CliTableStyleRecipe Milano { get; } = Roma with
    {
        Outer = CliFrameSegmentStyle.SingleFrame,
        TitleAccent = TableAccent.Success,
        HeaderAccent = TableAccent.Warning,
    };

    /// <summary>Napoli — full single-line grid with record separators on the default surface, success title. Universal.</summary>
    public static CliTableStyleRecipe Napoli { get; } = new()
    {
        Outer = CliFrameSegmentStyle.SingleFrame,
        AfterHeader = CliFrameSegmentStyle.SingleFrame,
        BetweenElements = CliFrameSegmentStyle.SingleFrame,
        BetweenRecords = CliFrameSegmentStyle.SingleFrame,
        Padding = CliCellPadding.Both,
        Surface = SurfaceRole.Default,
        TitleAccent = TableAccent.Success,
        OrientationSupport = CliTableStyleOrientationSupport.Both,
    };

    /// <summary>Torino — frameless outer with header rule and column separators on the default surface, success title. Universal.</summary>
    public static CliTableStyleRecipe Torino { get; } = new()
    {
        Outer = CliFrameSegmentStyle.None,
        AfterHeader = CliFrameSegmentStyle.SingleFrame,
        BetweenElements = CliFrameSegmentStyle.SingleFrame,
        BetweenRecords = CliFrameSegmentStyle.None,
        Padding = CliCellPadding.Both,
        Surface = SurfaceRole.Default,
        TitleAccent = TableAccent.Success,
        OrientationSupport = CliTableStyleOrientationSupport.Both,
    };

    /// <summary>Genova — tight (no-padding) single-line boxed grid on a panel surface, accent title. Universal.</summary>
    public static CliTableStyleRecipe Genova { get; } = new()
    {
        Outer = CliFrameSegmentStyle.SingleFrame,
        AfterHeader = CliFrameSegmentStyle.SingleFrame,
        BetweenElements = CliFrameSegmentStyle.SingleFrame,
        BetweenRecords = CliFrameSegmentStyle.None,
        Padding = CliCellPadding.None,
        Surface = SurfaceRole.Panel,
        TitleAccent = TableAccent.Accent,
        OrientationSupport = CliTableStyleOrientationSupport.Both,
    };

    /// <summary>Bologna — Roma framing on the default surface with a double outer frame, success title. Universal.</summary>
    public static CliTableStyleRecipe Bologna { get; } = Roma with
    {
        Surface = SurfaceRole.Default,
        TitleAccent = TableAccent.Success,
    };

    /// <summary>Palermo — attention style: alert surface, warning (yellow) title and frame, alert zebra. Universal.</summary>
    public static CliTableStyleRecipe Palermo { get; } = new()
    {
        Outer = CliFrameSegmentStyle.DoubleFrame,
        AfterHeader = CliFrameSegmentStyle.SingleFrame,
        BetweenElements = CliFrameSegmentStyle.SingleFrame,
        BetweenRecords = CliFrameSegmentStyle.None,
        Padding = CliCellPadding.Both,
        Surface = SurfaceRole.Alert,
        TitleAccent = TableAccent.Warning,
        FrameAccent = TableAccent.Warning,
        OrientationSupport = CliTableStyleOrientationSupport.Both,
    };

    /// <summary>Parma — compact vertical list: frameless, columns separated by a single space. Vertical only.</summary>
    public static CliTableStyleRecipe Parma { get; } = new()
    {
        Outer = CliFrameSegmentStyle.None,
        AfterHeader = CliFrameSegmentStyle.None,
        BetweenElements = CliFrameSegmentStyle.Space,
        BetweenRecords = CliFrameSegmentStyle.None,
        Padding = CliCellPadding.None,
        Surface = SurfaceRole.Default,
        TitleAccent = TableAccent.Success,
        OrientationSupport = CliTableStyleOrientationSupport.VerticalOnly,
    };

    /// <summary>Verona — condensed detail view: frameless, left-padded values, tight header. Horizontal only.</summary>
    public static CliTableStyleRecipe Verona { get; } = new()
    {
        Outer = CliFrameSegmentStyle.None,
        AfterHeader = CliFrameSegmentStyle.None,
        BetweenElements = CliFrameSegmentStyle.None,
        BetweenRecords = CliFrameSegmentStyle.None,
        Padding = CliCellPadding.Left,
        HeaderPadding = CliCellPadding.None,
        Surface = SurfaceRole.Default,
        TitleAccent = TableAccent.Success,
        OrientationSupport = CliTableStyleOrientationSupport.HorizontalOnly,
    };

    /// <summary>Lucca — Milano-based detail view: boxed single-line frame on a panel surface, no between-elements separator, success (green) title, warning (yellow) header. Horizontal only.</summary>
    public static CliTableStyleRecipe Lucca { get; } = Milano with
    {
        BetweenElements = CliFrameSegmentStyle.None,
        OrientationSupport = CliTableStyleOrientationSupport.HorizontalOnly,
    };
}
