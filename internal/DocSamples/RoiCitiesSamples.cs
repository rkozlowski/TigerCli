using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;
using ItTiger.TigerCli.Tui.Testing;
using BasicApp = RoiCities.Basic;
using ExtendedApp = RoiCities.Extended;

namespace DocSamples;

/// <summary>
/// Artifacts for <c>docs/getting-started.md</c>, captured from the ROI Cities example apps
/// (<c>RoiCities.Basic</c> / <c>RoiCities.Extended</c>) so the guide cannot drift from what the
/// apps actually render. Three capture forms are used:
/// <list type="bullet">
/// <item>Command output (list/show) renders the same <c>CliList</c>/<c>CliDetails</c> the commands
/// build (their public view factories), as HTML plus framed PNG companions.</item>
/// <item>Help and framework-error screens are full app runs captured through
/// <see cref="TigerCliAppTestHost.WithHtmlCapture"/> (HTML-only, like <c>command-app-run.html</c>;
/// app-run PNG capture is a planned slice — see <c>docs/design/doc-artifacts.md</c>).</item>
/// <item>Interactive prompt/menu frames drive the real app pipeline against a scripted
/// <see cref="TestShell"/> (the storyboard model of <see cref="TuiStoryboards"/>, at app level)
/// and capture <see cref="TestTerminal.LastRenderedGrid"/> as HTML + PNG pairs.</item>
/// </list>
/// Must run inside <see cref="DocExampleSet.GenerateAllAsync"/>'s pinned-theme region.
/// </summary>
internal static class RoiCitiesSamples
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    public static async Task<IReadOnlyList<DocArtifact>> GenerateAsync()
    {
        var sections = new List<DocPage.Section>();
        var pngs = new List<DocArtifact>();
        var options = DocTerminal.Html();

        // ---- basic app -----------------------------------------------------------------------

        var basicHelp = await CaptureRunAsync(BasicApp.RoiCitiesApp.Create(), "--help");
        sections.Add(new DocPage.Section(
            "Basic — generated help (--help)",
            "Metadata-driven help for the basic app: description, the two commands, and the "
            + "built-in framework options.",
            "roi-cities --help",
            basicHelp.StdOutHtml!));

        var store = new BasicApp.CityStore();
        var listGrid = BasicApp.ListCommand.CityList().Render(store.All).ToGrid();
        sections.Add(new DocPage.Section(
            "Basic — list command",
            "The list command projects the city store through a CliList: one column per field, "
            + "the name styled as the identity key.",
            "roi-cities list",
            TigerConsole.RenderGridToHtml(listGrid, options)));
        pngs.AddRange(PngCompanion.FromMeasuredGrid(
            "roi-cities-basic-list", listGrid, title: "roi-cities list"));

        var galway = store.Find("Galway")
            ?? throw new InvalidOperationException("ROI Cities capture: Galway is missing from the store.");
        var showGrid = BasicApp.ShowCommand.CityDetails(galway).ToGrid();
        sections.Add(new DocPage.Section(
            "Basic — show command",
            "The show command renders one city as a CliDetails label/value view.",
            "roi-cities show Galway",
            TigerConsole.RenderGridToHtml(showGrid, options)));
        pngs.AddRange(PngCompanion.FromMeasuredGrid(
            "roi-cities-basic-show", showGrid, title: "show Galway"));

        sections.Add(new DocPage.Section(
            "Basic — a missing city is prompted",
            "Run semi-interactively with the city argument missing, show asks for it with a text "
            + "input instead of failing.",
            "roi-cities show",
            await CaptureAppFrameAsync(
                BasicApp.RoiCitiesApp.Create(),
                ["show"],
                pngs,
                "roi-cities-basic-show-prompt",
                "roi-cities show",
                shell => EnqueueText(shell, "Galway"),
                expectedExitCode: 0)));

        var basicNonInteractive = await CaptureRunAsync(
            BasicApp.RoiCitiesApp.Create(), "show", "--non-interactive");
        Require(basicNonInteractive.ExitCode != 0, "basic non-interactive exit", basicNonInteractive.ExitCode);
        sections.Add(new DocPage.Section(
            "Basic — --non-interactive fails cleanly",
            "The same missing argument under --non-interactive: no prompt, an error on stderr, "
            + "and a non-zero exit code — automation never hangs on a question.",
            "roi-cities show --non-interactive",
            basicNonInteractive.StdErrHtml!));

        // ---- extended app ----------------------------------------------------------------------

        sections.Add(new DocPage.Section(
            "Extended — command menu",
            "With UseCommandMenu enabled, running the app bare opens a picker over the registered "
            + "commands; the selected command then runs through the normal pipeline.",
            "roi-cities",
            await CaptureAppFrameAsync(
                ExtendedApp.RoiCitiesApp.Create(),
                [],
                pngs,
                "roi-cities-extended-menu",
                "roi-cities",
                shell => shell.Terminal.EnqueueKey(ConsoleKey.Enter), // run "list" and finish
                expectedExitCode: (int)ExtendedApp.RoiCitiesExitCode.Ok)));

        sections.Add(new DocPage.Section(
            "Extended — provider-backed city select",
            "The show selector is provider-backed in the extended app: a missing city becomes a "
            + "select over the store's cities instead of a free-text input.",
            "roi-cities show",
            await CaptureAppFrameAsync(
                ExtendedApp.RoiCitiesApp.Create(),
                ["show"],
                pngs,
                "roi-cities-extended-show-select",
                "roi-cities show",
                shell =>
                {
                    // Store order: Dublin, Cork, Limerick, Waterford, Galway — pick Galway.
                    shell.Terminal.EnqueueKeys(
                        ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow);
                    shell.Terminal.EnqueueKey(ConsoleKey.Enter);
                },
                expectedExitCode: (int)ExtendedApp.RoiCitiesExitCode.Ok)));

        var extendedHelp = await CaptureRunAsync(ExtendedApp.RoiCitiesApp.Create(), "--help");
        sections.Add(new DocPage.Section(
            "Extended — generated help (--help)",
            "The extended app's help adds the display name, version metadata, and the exit-code "
            + "hint from the typed exit-code registration.",
            "roi-cities --help",
            extendedHelp.StdOutHtml!));

        var helpErrors = await CaptureRunAsync(ExtendedApp.RoiCitiesApp.Create(), "--help-errors");
        sections.Add(new DocPage.Section(
            "Extended — exit-code help (--help-errors)",
            "Typed exit codes documented from the app's RoiCitiesExitCode enum.",
            "roi-cities --help-errors",
            helpErrors.StdOutHtml!));

        var page = DocArtifact.Text(
            "roi-cities.html",
            DocPage.BuildPage(
                "Getting started — ROI Cities",
                "The two example apps from docs/getting-started.md, captured from real runs and "
                + "real rendering with the TigerBlue theme: the basic app's script-safe shape, and "
                + "the extended app's provider-backed prompting, command menu, and typed exit codes.",
                sections));

        return [page, .. pngs];
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static Task<TigerCliAppRunResult> CaptureRunAsync(TigerCliApp app, params string[] args)
        => TigerCliAppTestHost.For(app)
            .WithArgs(args)
            .WithHtmlCapture()
            .RunAsync();

    /// <summary>
    /// Runs the real app against a scripted <see cref="TestShell"/>, captures the first rendered
    /// prompt/menu frame as an HTML fragment plus a PNG companion (window chrome, canvas fitted to
    /// the frame), then lets <paramref name="completeInput"/> finish the run. Console output is
    /// redirected for the duration: command output goes through the process-global console, and
    /// only the shell-rendered frame belongs in the artifact.
    /// </summary>
    private static async Task<string> CaptureAppFrameAsync(
        TigerCliApp app,
        string[] args,
        List<DocArtifact> pngs,
        string pngName,
        string title,
        Action<TestShell> completeInput,
        int expectedExitCode)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalColorMode = TigerConsole.ColorMode;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            TigerConsole.ColorMode = CliColorMode.Never;

            var shell = DocTerminal.CreateShell();
            var run = app.RunAsync([.. args, "--no-color"], shell);

            await shell.Terminal.WaitForRenderCountAsync(1, StepTimeout);
            var grid = shell.Terminal.LastRenderedGrid
                ?? throw new InvalidOperationException($"ROI Cities capture '{pngName}': no grid was rendered.");
            pngs.AddRange(PngCompanion.FromMeasuredGrid(
                pngName, grid, title: title));
            var html = TigerConsole.RenderGridToHtml(grid);

            completeInput(shell);
            var exitCode = await run.WaitAsync(StepTimeout);
            Require(exitCode == expectedExitCode, $"'{pngName}' exit code", exitCode);
            return html;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            TigerConsole.ColorMode = originalColorMode;
        }
    }

    private static void EnqueueText(TestShell shell, string value)
    {
        foreach (var ch in value)
            shell.Terminal.EnqueueKey(ToConsoleKey(ch), keyChar: ch);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
    }

    private static ConsoleKey ToConsoleKey(char value) => value switch
    {
        >= 'a' and <= 'z' => ConsoleKey.A + (value - 'a'),
        >= 'A' and <= 'Z' => ConsoleKey.A + (value - 'A'),
        >= '0' and <= '9' => ConsoleKey.D0 + (value - '0'),
        _ => ConsoleKey.Spacebar,
    };

    private static void Require(bool condition, string what, object? actual)
    {
        if (!condition)
            throw new InvalidOperationException(
                $"ROI Cities capture: unexpected {what} ({actual ?? "<null>"}). "
                + "The scripted run no longer behaves as the artifact describes.");
    }
}
