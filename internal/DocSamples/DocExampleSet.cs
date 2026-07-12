using CommandParserTest;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace DocSamples;

/// <summary>
/// Defines the committed documentation artifacts under <c>docs/examples/</c>. Everything here must
/// be deterministic: one pinned theme (never the ambient <see cref="TigerConsole.CurrentTheme"/>),
/// fixed sample data, no environment-dependent widths (every width comes from
/// <see cref="DocTerminal"/>), LF line endings only.
/// Curated artifacts are generated as HTML + PNG pairs: the HTML page is the inspectable/diffable
/// truth, and <see cref="PngCompanion"/> renders the embeddable PNG (plus its <c>.png.txt</c>
/// sidecar) from the same measured layout, on the shared documentation terminal.
/// The drift test in ItTiger.TigerCli.Tests regenerates this set and compares it to the committed
/// files, so a rendering change that alters an artifact fails until the artifacts are regenerated.
/// </summary>
public static class DocExampleSet
{
    private static readonly ITheme Theme = new TigerBlueTheme();

    public static async Task<IReadOnlyList<DocArtifact>> GenerateAllAsync()
    {
        // App-run capture resolves markup through the process-global CurrentTheme (and honours the
        // TIGERCLI_THEME environment variable), and TestShell resolves its theme from CurrentTheme
        // at construction; pin both so artifacts are identical no matter which process regenerates
        // them (generator or drift test) and what the machine has configured.
        var originalTheme = TigerConsole.CurrentTheme;
        var originalThemeEnv = Environment.GetEnvironmentVariable("TIGERCLI_THEME");
        try
        {
            TigerConsole.CurrentTheme = Theme;
            Environment.SetEnvironmentVariable("TIGERCLI_THEME", null);

            var artifacts = new List<DocArtifact> { DocArtifact.Text("assets/tigercli.css", DocPage.Css) };
            artifacts.AddRange(MarkupAndStyles());
            artifacts.AddRange(CliTablePresets());
            artifacts.AddRange(CliListExample());
            artifacts.AddRange(CliDetailsExample());
            artifacts.AddRange(FrameAndBlocks());
            artifacts.AddRange(ThemeShowcase.Generate());
            artifacts.Add(await CommandAppRunAsync());
            artifacts.AddRange(await RoiCitiesSamples.GenerateAsync());
            artifacts.AddRange(await FolderCopySamples.GenerateAsync());
            artifacts.AddRange(await TuiStoryboards.GenerateAsync());
            return artifacts;
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
            Environment.SetEnvironmentVariable("TIGERCLI_THEME", originalThemeEnv);
        }
    }

    // ---- command app run (styled app-run capture) -------------------------------------------

    // Real CommandParserTest runs captured through TigerCliAppTestHost.WithHtmlCapture: the same
    // pipeline an app-boundary test drives, so help and framework-error artifacts cannot drift
    // from actual rendering.
    // HTML-only for now: app-run capture yields HTML fragments, not grids, so the PNG companion
    // needs a segment-level capture (planned slice; see docs/design/doc-artifacts.md).
    private static async Task<DocArtifact> CommandAppRunAsync()
    {
        var help = await CaptureAsync("--help");
        var helpErrors = await CaptureAsync("--help-errors");
        var missingRequired = await CaptureAsync("raw", "--non-interactive");

        var sections = new List<DocPage.Section>
        {
            new(
                "Generated help (--help)",
                "Metadata-driven help for the whole app: description, commands, and built-in options.",
                "parser-test --help",
                help.StdOutHtml!),

            new(
                "Exit-code help (--help-errors)",
                "Typed exit codes documented from the app's exit-code enum.",
                "parser-test --help-errors",
                helpErrors.StdOutHtml!),

            new(
                "Framework error (missing required option, non-interactive)",
                "With --non-interactive the framework fails fast instead of prompting; the error "
                + "goes to stderr and maps to the app's typed validation exit code.",
                "parser-test raw --non-interactive",
                missingRequired.StdErrHtml!),
        };

        return DocArtifact.Text(
            "command-app-run.html",
            DocPage.BuildPage(
                "Command app runs",
                "Full CommandParserTest app runs captured as HTML through "
                + "TigerCliAppTestHost.WithHtmlCapture, with the TigerBlue theme.",
                sections));
    }

    private static Task<TigerCliAppRunResult> CaptureAsync(params string[] args)
        => TigerCliAppTestHost.For(CommandParserTestApp.Create())
            .WithArgs(args)
            .WithHtmlCapture()
            .RunAsync();

    // ---- markup / styles ------------------------------------------------------------------

    private static readonly (string Heading, string Description, string Markup)[] MarkupFragments =
    [
        ("Raw colours",
            "Named colours and 'fg on bg' inks, straight from markup.",
            "[Red]error[/] [Green]success[/] [Yellow]warning[/] [Cyan]info[/]\n"
            + "[Yellow on DarkBlue] yellow on dark blue [/]"),

        ("Text decorations",
            "Bold, italic, and underline render as the stable tc-* CSS classes. "
            + "Compose decorations by nesting.",
            "[b]Bold[/] [i]Italic[/] [u]Underline[/] [Bold][Underline]both[/][/]"),

        ("Semantic status tokens",
            "Semantic tokens are resolved by the theme (TigerBlue here), so apps state intent "
            + "and themes decide the ink.",
            "[Accent]accent[/] [Muted]muted[/] [Success]success[/] [Warning]warning[/] [Error]error[/]"),

        ("Structured-output roles",
            "The CRUD/structured-output roles used by CliList and CliDetails. [Link] carries the "
            + "link role; anchors are opt-in per render (shown on the CliList page).",
            "[Heading]Devices[/]\n[Key]cam-042[/] [Value]Loading dock camera[/]\n"
            + "[Path]C:\\media\\incoming[/] [Link]https://example.com/devices/cam-042[/]"),
    ];

    private static IReadOnlyList<DocArtifact> MarkupAndStyles()
    {
        var sections = MarkupFragments
            .Select(f => new DocPage.Section(
                f.Heading, f.Description, f.Markup, TigerConsole.MarkupToHtml(f.Markup, theme: Theme)))
            .ToList();

        var page = DocArtifact.Text(
            "markup-and-styles.html",
            DocPage.BuildPage(
                "Markup and styles",
                "TigerCli bracket markup rendered through HtmlSink with the TigerBlue theme. "
                + "Each fragment shows the markup source and the rendered result.",
                sections));

        // PNG companion: the same fragments stacked in one Preformatted grid (cell content is
        // markup, parsed by the measure pass), a [Heading] caption above each and a blank line
        // between them.
        var preformatted = new CliCellStyle { FormattingMode = CliFormattingMode.Preformatted };
        var grid = new CliGrid(1, (MarkupFragments.Length * 3) - 1);
        for (int i = 0; i < MarkupFragments.Length; i++)
        {
            grid.Set(0, i * 3, $"[Heading]{MarkupFragments[i].Heading}[/]", preformatted);
            grid.Set(0, (i * 3) + 1, MarkupFragments[i].Markup, preformatted);
            if (i < MarkupFragments.Length - 1)
                grid.Set(0, (i * 3) + 2, "");
        }

        return [page, .. PngCompanion.FromGrid("markup-and-styles", grid)];
    }

    // ---- CliTable presets -----------------------------------------------------------------

    // Mirrors the TableTasting harness: the same list/details preset pairs and connection-profile
    // data shape, trimmed to three profiles to keep the artifacts compact.
    private static readonly (string Name, CliTableStylePreset ListPreset, CliTableStylePreset DetailsPreset)[]
        PresetPairs =
        [
            ("Alpha", CliTableStylePreset.Milano, CliTableStylePreset.Lucca),
            ("Beta", CliTableStylePreset.Roma, CliTableStylePreset.Roma),
            ("Gamma", CliTableStylePreset.Parma, CliTableStylePreset.Verona),
            ("Delta", CliTableStylePreset.Torino, CliTableStylePreset.Torino),
        ];

    private sealed record Profile(
        string Name, string Server, string Authentication, string Username,
        string Encrypt, string Database, string Timeout, string Environment);

    private static readonly Profile[] Profiles =
    [
        new("local-dev", "localhost,1433", "SQL Password", "tiger_dev",
            "Optional", "TigerDev", "15 seconds", "Development"),
        new("reporting", "sql-reporting.internal", "Integrated", "CORP\\svc_reports",
            "Required", "Reports", "30 seconds", "Production"),
        new("staging", "sql-staging.internal", "Entra ID", "db-admin@contoso.com",
            "Required", "TigerStage", "20 seconds", "Staging"),
    ];

    private static IReadOnlyList<DocArtifact> CliTablePresets()
    {
        // The pinned width plays the terminal's role, like TableTasting's bounded tables, so the
        // HTML fragments and the PNG companions show the same measured layout.
        var options = DocTerminal.Html();
        var sections = new List<DocPage.Section>();
        var artifacts = new List<DocArtifact>();

        foreach (var (name, listPreset, detailsPreset) in PresetPairs)
        {
            var list = new CliTable()
                .ApplyPreset(listPreset, Theme, CliTableOrientation.Vertical)
                .AddTitle($"{name} list - {listPreset}");
            list.AddHeader("Name", "Server", "Authentication", "Database", "Environment");
            foreach (var p in Profiles)
                list.AddRecord(p.Name, p.Server, p.Authentication, p.Database, p.Environment);

            var details = new CliTable()
                .ApplyPreset(detailsPreset, Theme, CliTableOrientation.Horizontal)
                .AddTitle($"{name} details - {detailsPreset}");
            details.AddHeader("Name:", "Server:", "Authentication:", "Username:", "Database:", "Environment:");
            var d = Profiles[1];
            details.AddRecord(d.Name, d.Server, d.Authentication, d.Username, d.Database, d.Environment);

            var listGrid = list.ToGrid();
            var detailsGrid = details.ToGrid();
            sections.Add(new DocPage.Section(
                $"{name} — {listPreset} list + {detailsPreset} details",
                $"List preset {listPreset} (vertical) paired with details preset {detailsPreset} (horizontal).",
                null,
                TigerConsole.RenderGridToHtml(listGrid, options) + "\n" + TigerConsole.RenderGridToHtml(detailsGrid, options)));

            // PNG companion: both tables of the pair stacked in one frame. The subgrids are
            // re-measured inside the stack at the same 120-column width the HTML used.
            var stack = new CliGrid(1, 3);
            stack.SetSubgrid(0, 0, listGrid);
            stack.Set(0, 1, "");
            stack.SetSubgrid(0, 2, detailsGrid);
            artifacts.AddRange(PngCompanion.FromGrid($"cli-table-presets-{name.ToLowerInvariant()}", stack));
            if (string.Equals(name, "Alpha", StringComparison.Ordinal))
            {
                artifacts.AddRange(PngCompanion.FromMeasuredGrid(
                    "cli-table-presets-alpha-terminal",
                    stack,
                    title: "CliTable presets"));
            }
        }

        var page = DocArtifact.Text(
            "cli-table-presets.html",
            DocPage.BuildPage(
                "CliTable style presets",
                "The preset pairs from the TableTasting harness, rendered at an emulated 120-column "
                + "terminal width (HtmlSinkOptions.SoftMaxWidth) with the TigerBlue theme.",
                sections));

        return [page, .. artifacts];
    }

    // ---- CliList ---------------------------------------------------------------------------

    private sealed record Device(string Id, string Name, string Status, string Url);

    private static readonly Device[] Devices =
    [
        new("cam-042", "Loading dock camera", "Online", "https://example.com/devices/cam-042"),
        new("cam-077", "Warehouse aisle 3 overview camera (north mount)", "Online", "https://example.com/devices/cam-077"),
        new("srv-001", "Recording server", "Degraded", "https://example.com/devices/srv-001"),
    ];

    // MinWidths keep the short Id/Status values whole when the list is width-constrained; only
    // the wide Name and Url columns give way.
    private static CliList<Device> DeviceList() => new CliList<Device>()
        .ApplyPreset(CliTableStylePreset.Lucca, Theme)
        .AddKeyColumn("Id", d => d.Id).SetWidth(minWidth: 8)
        .AddColumn("Name", d => d.Name)
        .AddColumn("Status", d => d.Status).SetWidth(minWidth: 9)
        .AddLinkColumn("Url", d => d.Url);

    private static IReadOnlyList<DocArtifact> CliListExample()
    {
        // The first section is the paired render: HTML and PNG share the grid measured at the
        // 120-column paired-artifact width (the PNG has no anchors; links render as styled text).
        var listGrid = DeviceList().Render(Devices).ToGrid();
        var sections = new List<DocPage.Section>
        {
            new(
                "Device list (Lucca preset, anchors on)",
                "CliList projects items into a column-per-field table. AddKeyColumn styles identity "
                + "values, AddLinkColumn marks values as links — with HtmlHyperlinkMode.Anchor they "
                + "render as real, clickable anchors.",
                null,
                TigerConsole.RenderGridToHtml(listGrid, DocTerminal.Html(HtmlHyperlinkMode.Anchor))),

            // The one deliberate departure from the documentation terminal: this section's subject
            // *is* width-dependent wrapping, so it renders the same list at a narrower emulated
            // width. It is an HTML fragment inside the page, not a captured terminal artifact.
            new(
                "The same list at an emulated 60-column terminal",
                "HtmlSinkOptions.SoftMaxWidth plays the terminal's role for the measure pass: the "
                + "over-wide Name column word-wraps exactly as it would in a 60-column console.",
                null,
                TigerConsole.RenderToHtml(
                    DeviceList()
                        .DefaultWrapping(new CliWrapping(CliWrapMode.WordWrap, false, "…"))
                        .Render(Devices),
                    new HtmlSinkOptions { HyperlinkMode = HtmlHyperlinkMode.Anchor, SoftMaxWidth = 60 })),
        };

        var page = DocArtifact.Text(
            "cli-list.html",
            DocPage.BuildPage(
                "CliList",
                "List command output rendered through HtmlSink with the TigerBlue theme.",
                sections));

        return [page, .. PngCompanion.FromMeasuredGrid("cli-list", listGrid)];
    }

    // ---- CliDetails --------------------------------------------------------------------------

    private static CliDetails DeviceDetails(CliTableStylePreset preset) => new CliDetails()
        .ApplyPreset(preset, Theme)
        .AddKey("Id:", "cam-042")
        .Add("Name:", "Loading dock camera")
        .Add("Status:", "Online", style: ThemeStyle.Success)
        .AddPath("Storage:", "C:\\media\\incoming")
        .AddLink("Url:", "https://example.com/devices/cam-042")
        .Add("Notes:", null); // Add renders the missing display for null; AddOptional would omit the row

    private static IReadOnlyList<DocArtifact> CliDetailsExample()
    {
        var anchor = DocTerminal.Html(HtmlHyperlinkMode.Anchor);
        var sections = new List<DocPage.Section>
        {
            new(
                "Device details (Details preset)",
                "CliDetails renders one record as label/value rows. AddKey, AddPath, and AddLink "
                + "carry the semantic roles; a null value renders the missing-value display "
                + "(AddOptional would omit the row instead).",
                null,
                TigerConsole.RenderToHtml(DeviceDetails(CliTableStylePreset.Details), anchor)),

            new(
                "The same record with the DetailsCondensed preset",
                "Preset choice is one line; the record definition is unchanged.",
                null,
                TigerConsole.RenderToHtml(DeviceDetails(CliTableStylePreset.DetailsCondensed), anchor)),
        };

        var page = DocArtifact.Text(
            "cli-details.html",
            DocPage.BuildPage(
                "CliDetails",
                "Single-record show/details output rendered through HtmlSink with the TigerBlue theme.",
                sections));

        // PNG companion: both preset variants stacked, from fresh instances of the same record
        // definitions the HTML sections rendered.
        var stack = new CliGrid(1, 3);
        stack.SetSubgrid(0, 0, DeviceDetails(CliTableStylePreset.Details).ToGrid());
        stack.Set(0, 1, "");
        stack.SetSubgrid(0, 2, DeviceDetails(CliTableStylePreset.DetailsCondensed).ToGrid());

        return [page, .. PngCompanion.FromGrid("cli-details", stack)];
    }

    // ---- frame and block diagnostics ---------------------------------------------------------

    // A focused diagnostic artifact for frame/border rendering quality: box-drawing joins must be
    // seamless (especially in the PNG, where glyphs are stretched onto the integer pixel grid) and
    // link/underline styling must stay on content — never on the surrounding frame glyphs or
    // padding. Reviewed by eye in the PNG and by diff in the HTML.
    private static CliTable FrameDiagnosticTable(string title, CliFrameSegmentStyle outer, CliFrameSegmentStyle inner)
    {
        var table = new CliTable
        {
            FrameConfig = new CliTableFrameConfig
            {
                OuterFrame = new(outer),
                AfterHeader = new(inner),
                BetweenElements = new(inner),
                BetweenRecords = new(inner),
            },
        };
        table.AddTitle(title);
        table.AddHeader("Frame", "Outer", "Inner");
        table.AddRecord("corners + junctions", outer.ToString(), inner.ToString());
        table.AddRecord("row separators", "═ ║ ╬", "─ │ ┼");
        return table;
    }

    private static IReadOnlyList<DocArtifact> FrameAndBlocks()
    {
        var single = FrameDiagnosticTable(
            "Single frame", CliFrameSegmentStyle.SingleFrame, CliFrameSegmentStyle.SingleFrame);
        var doubled = FrameDiagnosticTable(
            "Double frame", CliFrameSegmentStyle.DoubleFrame, CliFrameSegmentStyle.DoubleFrame);
        var mixed = FrameDiagnosticTable(
            "Mixed frame", CliFrameSegmentStyle.DoubleFrame, CliFrameSegmentStyle.SingleFrame);

        // Block-element shades, a progress bar, and the four basic text styles plus underline.
        // Preformatted cells keep the markup live (parsed in the measure pass).
        var preformatted = new CliCellStyle { FormattingMode = CliFormattingMode.Preformatted };
        var blocks = new CliGrid(1, 3);
        blocks.Set(0, 0, "Shades    ░░░░░░ ▒▒▒▒▒▒ ▓▓▓▓▓▓ ██████", preformatted);
        blocks.Set(0, 1, "Progress  ██████████████░░░░░░░░░░ 56%", preformatted);
        blocks.Set(0, 2, "Styles    normal [b]bold[/] [i]italic[/] [b][i]bold+italic[/][/] [u]underline[/]", preformatted);

        // A link value sitting directly against frame separators: the value stays underlined,
        // the frame glyphs and cell padding around it must not be.
        var linked = new CliDetails()
            .ApplyPreset(CliTableStylePreset.Details, Theme)
            .Add("Name:", "Frame diagnostics")
            .AddLink("Url:", "https://example.com/frames");

        var options = DocTerminal.Html();
        var anchor = DocTerminal.Html(HtmlHyperlinkMode.Anchor);
        var singleGrid = single.ToGrid();
        var doubledGrid = doubled.ToGrid();
        var mixedGrid = mixed.ToGrid();
        var linkedGrid = linked.ToGrid();

        var sections = new List<DocPage.Section>
        {
            new(
                "Single-line frame",
                "Outer frame, header rule, and record/element separators all single-line: "
                + "┌ ┬ ┐ ├ ┼ ┤ └ ┴ ┘ junctions must join without gaps.",
                null,
                TigerConsole.RenderGridToHtml(singleGrid, options)),

            new(
                "Double-line frame",
                "The same table with double-line frames throughout (╔ ╦ ╗ ╠ ╬ ╣ ╚ ╩ ╝).",
                null,
                TigerConsole.RenderGridToHtml(doubledGrid, options)),

            new(
                "Mixed frame (double outer, single inner)",
                "Double outer frame joined to single inner rules (╒-style transition junctions).",
                null,
                TigerConsole.RenderGridToHtml(mixedGrid, options)),

            new(
                "Block elements and text styles",
                "Shade/block characters and a progress-bar run must render as continuous fills; "
                + "the styles line verifies normal, bold, italic, bold+italic, and underline.",
                null,
                TigerConsole.RenderGridToHtml(blocks, options)),

            new(
                "Link against frame separators",
                "The Url value is a link (underlined, and an anchor here): the underline must stay "
                + "on the value only — never on the frame glyphs, separators, or cell padding.",
                null,
                TigerConsole.RenderGridToHtml(linkedGrid, anchor)),
        };

        var page = DocArtifact.Text(
            "frame-and-blocks.html",
            DocPage.BuildPage(
                "Frame and block diagnostics",
                "Box-drawing frames, block elements, and decoration-isolation checks rendered "
                + "through HtmlSink; the PNG companion shows the same grids through PngSink.",
                sections));

        // PNG companion: everything stacked on one canvas so frame joins and decoration leakage
        // can be inspected in a single image.
        var stack = new CliGrid(1, 9);
        stack.SetSubgrid(0, 0, singleGrid);
        stack.Set(0, 1, "");
        stack.SetSubgrid(0, 2, doubledGrid);
        stack.Set(0, 3, "");
        stack.SetSubgrid(0, 4, mixedGrid);
        stack.Set(0, 5, "");
        stack.SetSubgrid(0, 6, blocks);
        stack.Set(0, 7, "");
        stack.SetSubgrid(0, 8, linkedGrid);

        return [page, .. PngCompanion.FromGrid("frame-and-blocks", stack)];
    }
}
