using System.Diagnostics;
using System.Globalization;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Activity;
using ActivityContext = ItTiger.TigerCli.Tui.Activity.ActivityContext;

namespace FileCopy;

internal static class FileCopyCommandRunner
{
    private const double BytesPerMb = 1024.0 * 1024.0;

    public static async Task<FileCopyExitCode> RunAsync(
        TigerCliSettings settings,
        string source,
        string destination,
        bool overwrite)
    {
        if (!File.Exists(source))
        {
            TigerConsole.MarkupErrorLine(settings.E("[Error]Source file does not exist:[/] {0}", source));
            return FileCopyExitCode.CopyFailed;
        }

        var length = new FileInfo(source).Length;
        var stopwatch = Stopwatch.StartNew();
        var spec = BuildActivitySpec(settings, source, destination, length);
        var result = await TigerTui.RunActivityAsync(spec, async (context, ct) =>
        {
            await FileCopyPlanner.CopyAsync(
                source,
                destination,
                overwrite,
                progress => ReportProgress(context, progress, stopwatch.Elapsed),
                ct).ConfigureAwait(false);
            return length;
        }).ConfigureAwait(false);
        stopwatch.Stop();

        return result.Outcome switch
        {
            ActivityOutcome.Completed => ReportCompleted(settings, source, destination, length, stopwatch.Elapsed),
            ActivityOutcome.Cancelled or ActivityOutcome.Aborted or ActivityOutcome.SystemCancelled =>
                ReportCancelled(settings, "Copy cancelled."),
            ActivityOutcome.TimedOut => ReportCancelled(settings, "Copy timed out."),
            ActivityOutcome.Failed => ReportFailed(settings, result.Exception),
            _ => FileCopyExitCode.InternalError,
        };
    }

    internal static ActivityDialogSpec BuildActivitySpec(
        TigerCliSettings settings,
        string source,
        string destination,
        long totalBytes)
    {
        var headline = settings.E(
            "Copying {0} ({1} MB) to {2}…",
            Path.GetFileName(source),
            (totalBytes / BytesPerMb).ToString("F1", CultureInfo.InvariantCulture),
            destination);

        return ActivityDialogSpec.Create()
            .SetNonInteractiveMessage(headline)
            .AddColumn(width: 12, align: CliTextAlignment.Right).Padding(CliCellPadding.Right)
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddColumn(width: 20, align: CliTextAlignment.Left).Padding(CliCellPadding.Left)
            .AddRow(null, row => row.Cell(0, span: 3).Text(headline).Align(CliTextAlignment.Center))
            .AddRow("file", row => row
                .Cell(0).Text("File:")
                .Cell(1, span: 2).Text("{0}").Align(CliTextAlignment.Left)
                .Values(Path.GetFileName(source)))
            .AddRow("bytes", row => row
                .Cell(0).Text("Progress:")
                .Cell(1).ProgressBar(
                    valueIndex: 0,
                    maxValueIndex: 1,
                    style: ProgressBarStyle.Dash,
                    colorMode: ProgressBarColorMode.ThreeColor)
                .Cell(2).Text(" [Value]{2,5:F1}%[/] {0:F1}/{1:F1}MB")
                .Values(0.0, Math.Max(1, totalBytes) / BytesPerMb, 0.0))
            .AddRow("time", row => row
                .Cell(0).Text("Elapsed:")
                .Cell(1, span: 2).Text("[Muted]{0}[/]").Align(CliTextAlignment.Left)
                .Values(FormatDuration(TimeSpan.Zero)))
            .Build();
    }

    internal static void ReportProgress(ActivityContext context, FileCopyProgress progress, TimeSpan elapsed)
    {
        var total = Math.Max(1, progress.TotalBytes);
        context.SetValues(
            "bytes",
            progress.BytesCopied / BytesPerMb,
            total / BytesPerMb,
            100.0 * progress.BytesCopied / total);
        context.SetValues("time", FormatDuration(elapsed));
    }

    private static FileCopyExitCode ReportCompleted(
        TigerCliSettings settings,
        string source,
        string destination,
        long length,
        TimeSpan elapsed)
    {
        TigerConsole.MarkupLine(settings.E(
            "[Success]Copied[/] {0} ({1} MB) [Success]to[/] {2} [Muted]in {3}.[/]",
            source,
            (length / BytesPerMb).ToString("F1", CultureInfo.InvariantCulture),
            destination,
            FormatDuration(elapsed)));
        return FileCopyExitCode.Ok;
    }

    private static FileCopyExitCode ReportCancelled(TigerCliSettings settings, string message)
    {
        TigerConsole.MarkupErrorLine(settings.E("[Warning]{0}[/]", message));
        return FileCopyExitCode.Cancelled;
    }

    private static FileCopyExitCode ReportFailed(TigerCliSettings settings, Exception? exception)
    {
        TigerConsole.MarkupErrorLine(settings.E(
            "[Error]Copy failed:[/] {0}", exception?.Message ?? "unknown error"));
        return FileCopyExitCode.CopyFailed;
    }

    private static string FormatDuration(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : elapsed.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
