using System.Globalization;
using System.Diagnostics;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Activity;
using ActivityContext = ItTiger.TigerCli.Tui.Activity.ActivityContext;

namespace FolderCopy;

/// <summary>
/// The default (and only) Folder Copy command. It resolves the source/destination folders (prompting
/// with the folder picker when missing in semi-interactive mode), plans the copy, and runs it through a
/// single <see cref="TigerTui.RunActivityAsync(ActivityDialogSpec, Func{ActivityContext, CancellationToken, Task})"/>
/// path — a rich progress dialog in semi-interactive mode, and the same work headlessly under
/// <c>--non-interactive</c>. There is no separate "interactive" vs "non-interactive" copy body.
/// </summary>
public sealed class FolderCopyCommand : TigerCliAsyncCommandHandler<FolderCopySettings, FolderCopyExitCode>
{
    internal const double BytesPerMb = 1024.0 * 1024.0;

    public override async Task<FolderCopyExitCode> ExecuteAsync(FolderCopySettings settings)
    {
        // Source/Destination are Required options: a missing value has already been prompted (semi-
        // interactive) or failed validation (non-interactive) before we get here, so they are non-null.
        var source = settings.Source!;
        var destination = settings.Destination!;

        if (!Directory.Exists(source))
        {
            TigerConsole.MarkupErrorLine(settings.E("[Error]Source folder does not exist:[/] {0}", source));
            return FolderCopyExitCode.CopyFailed;
        }

        if (PathsAreSame(source, destination))
        {
            TigerConsole.MarkupErrorLine(settings.T("[Error]Source and destination are the same folder.[/]"));
            return FolderCopyExitCode.CopyFailed;
        }

        // Scanning phase: walk the source tree inside its own simple activity so a slow filesystem walk
        // shows progress feedback (and stays cancellable) instead of silently blocking before the copy.
        var scanResult = await TigerTui.RunActivityAsync(
            settings.T("Scanning source folder…"),
            (_, ct) => FolderCopyPlanner.PlanAsync(source, ct));

        if (scanResult.Outcome == ActivityOutcome.Failed)
        {
            TigerConsole.MarkupErrorLine(settings.E(
                "[Error]Could not scan source folder:[/] {0}", scanResult.Exception?.Message ?? "unknown error"));
            return FolderCopyExitCode.CopyFailed;
        }

        if (!scanResult.IsCompleted)
        {
            TigerConsole.MarkupErrorLine(settings.T("[Warning]Copy cancelled.[/]"));
            return FolderCopyExitCode.Cancelled;
        }

        var plan = scanResult.Value!;
        if (plan.Items.Count == 0)
        {
            TigerConsole.MarkupLine(settings.E("[Muted]No files to copy under[/] {0}[Muted].[/]", source));
            return FolderCopyExitCode.Ok;
        }

        var spec = BuildActivitySpec(settings, plan, destination);
        var stopwatch = Stopwatch.StartNew();

        var result = await TigerTui.RunActivityAsync(spec, async (context, ct) =>
        {
            await FolderCopyPlanner.ExecuteAsync(plan, destination,
                progress => ReportProgress(context, progress, stopwatch.Elapsed), ct).ConfigureAwait(false);

            return plan.Items.Count;
        });

        stopwatch.Stop();
        return ReportOutcome(settings, result, plan, source, destination, stopwatch.Elapsed);
    }

    private FolderCopyExitCode ReportOutcome(
        FolderCopySettings settings,
        ActivityResult<int> result,
        CopyPlan plan,
        string source,
        string destination,
        TimeSpan elapsed)
    {
        switch (result.Outcome)
        {
            case ActivityOutcome.Completed:
                TigerConsole.MarkupLine(settings.E(
                    "[Success]Copied[/] {0} [Success]files[/] ({1} MB) [Success]from[/] {2} [Success]to[/] {3} [Muted]in {4}.[/]",
                    plan.Items.Count,
                    (plan.TotalBytes / BytesPerMb).ToString("F1", CultureInfo.InvariantCulture),
                    source,
                    destination,
                    FormatDuration(elapsed)));
                return FolderCopyExitCode.Ok;

            case ActivityOutcome.Failed:
                TigerConsole.MarkupErrorLine(settings.E(
                    "[Error]Copy failed:[/] {0}", result.Exception?.Message ?? "unknown error"));
                return FolderCopyExitCode.CopyFailed;

            case ActivityOutcome.Cancelled:
            case ActivityOutcome.Aborted:
            case ActivityOutcome.SystemCancelled:
                TigerConsole.MarkupErrorLine(settings.T("[Warning]Copy cancelled.[/]"));
                return FolderCopyExitCode.Cancelled;

            case ActivityOutcome.TimedOut:
                TigerConsole.MarkupErrorLine(settings.T("[Warning]Copy timed out.[/]"));
                return FolderCopyExitCode.Cancelled;

            default:
                return FolderCopyExitCode.InternalError;
        }
    }

    /// <summary>
    /// Maps one planner progress snapshot onto the dialog's dynamic rows. Updates coalesce and are
    /// applied on the modal-loop thread. Internal (not private) so the documentation capture in
    /// <c>internal/DocSamples</c> drives the dialog through the exact same mapping.
    /// </summary>
    internal static void ReportProgress(ActivityContext context, CopyProgress progress, TimeSpan elapsed)
    {
        context.SetValues("file", progress.CurrentRelativePath);
        context.SetValues("current",
            progress.CurrentFileBytes,
            Math.Max(1, progress.CurrentFileLength),
            Percent(progress.CurrentFileBytes, progress.CurrentFileLength));
        context.SetValues("files",
            progress.FilesCompleted,
            progress.TotalFiles,
            Percent(progress.FilesCompleted, progress.TotalFiles));
        context.SetValues("bytes",
            progress.BytesCopied / BytesPerMb,
            progress.TotalBytes / BytesPerMb,
            Percent(progress.BytesCopied, progress.TotalBytes));
        context.SetValues("time",
            FormatDuration(elapsed),
            FormatEta(elapsed, progress.BytesCopied, progress.TotalBytes));
    }

    // A rich, multi-row activity layout: current file name, a per-file progress bar, an overall
    // files-copied bar, an overall bytes-copied bar, and an elapsed/ETA line — all progress bars in
    // the Dash style. The non-interactive message gives scripts a single line of context in place of
    // the live dialog. Internal (not private) so internal/DocSamples can capture the real dialog.
    internal static ActivityDialogSpec BuildActivitySpec(
        FolderCopySettings settings, CopyPlan plan, string destination)
    {
        var headline = settings.E(
            "Copying {0} files ({1} MB) to {2}…",
            plan.Items.Count,
            (plan.TotalBytes / BytesPerMb).ToString("F1", CultureInfo.InvariantCulture),
            destination);

        return ActivityDialogSpec.Create()
            .SetNonInteractiveMessage(headline)
            .AddColumn(width: 14, align: CliTextAlignment.Right).Padding(CliCellPadding.Right)
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddColumn(width: 26, align: CliTextAlignment.Left).Padding(CliCellPadding.Left)
            .AddRow(null, r => r.Cell(0, span: 3).Text(headline).Align(CliTextAlignment.Center))
            .AddRow("file", r => r
                .Cell(0).Text("File:")
                .Cell(1, span: 2).Text("{0}").Align(CliTextAlignment.Left)
                .Values(string.Empty))
            .AddRow("current", r => r
                .Cell(0).Text("Current:")
                .Cell(1).ProgressBar(valueIndex: 0, maxValueIndex: 1, style: ProgressBarStyle.Dash, colorMode: ProgressBarColorMode.ThreeColor)
                .Cell(2).Text(" [Value]{2,5:F1}%[/]")
                .Values(0, 1, 0.0))
            .AddRow("files", r => r
                .Cell(0).Text("Files:")
                .Cell(1).ProgressBar(valueIndex: 0, maxValueIndex: 1, style: ProgressBarStyle.Dash, colorMode: ProgressBarColorMode.ThreeColor)
                .Cell(2).Text(" [Value]{2,5:F1}%[/] [Success]{0}/{1}[/]")
                .Values(0, plan.Items.Count, 0.0))
            .AddRow("bytes", r => r
                .Cell(0).Text("Total size:")
                .Cell(1).ProgressBar(valueIndex: 0, maxValueIndex: 1, style: ProgressBarStyle.Dash, colorMode: ProgressBarColorMode.ThreeColor)
                .Cell(2).Text(" [Value]{2,5:F1}%[/] [Success]{0:F1}/{1:F1}MB[/]")
                .Values(0.0, plan.TotalBytes / BytesPerMb, 0.0))
            .AddRow("time", r => r
                .Cell(0).Text("Elapsed:")
                .Cell(1, span: 2).Text("[Muted]{0} • ETA {1}[/]").Align(CliTextAlignment.Left)
                .Values(FormatDuration(TimeSpan.Zero), "—"))
            .Build();
    }

    // Percent/FormatDuration/FormatEta are internal (not private) so the documentation capture in
    // internal/DocSamples computes its expected frame content through the same code.
    internal static double Percent(long value, long max) =>
        max <= 0 ? 100.0 : 100.0 * value / max;

    internal static string FormatDuration(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : elapsed.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    internal static string FormatEta(TimeSpan elapsed, long bytesCopied, long totalBytes)
    {
        if (bytesCopied <= 0 || totalBytes <= 0 || bytesCopied >= totalBytes)
            return "—";

        var fractionDone = (double)bytesCopied / totalBytes;
        var remaining = TimeSpan.FromSeconds(elapsed.TotalSeconds * (1 - fractionDone) / fractionDone);
        return FormatDuration(remaining);
    }

    private static bool PathsAreSame(string a, string b)
    {
        var fullA = Path.TrimEndingDirectorySeparator(Path.GetFullPath(a));
        var fullB = Path.TrimEndingDirectorySeparator(Path.GetFullPath(b));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(fullA, fullB, comparison);
    }
}
