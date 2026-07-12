using System.Text;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using SkiaSharp;

namespace ItTiger.TigerCli.PngSink;

/// <summary>
/// An <see cref="ICliRenderSink"/> that captures TigerCli text segments into a fixed-size terminal
/// grid and materializes the result as a PNG image.
/// </summary>
/// <remarks>
/// The sink exposes the configured columns and rows through the render-sink width/height properties so
/// grids can measure against the same dimensions that will be rasterized. Writes are stateful; call
/// <see cref="Reset"/> to reuse an instance for a new frame.
/// </remarks>
public sealed class PngSink : ICliRenderSink
{
    private const int FrameThickness = 1;
    private const int TitleHorizontalPadding = 8;
    private const int TitleVerticalPadding = 4;
    private const int TitleIconTextSpacing = 6;
    private const int TitleControlsSpacing = 12;

    private readonly PngSinkOptions _options;
    private readonly Cell[,] _cells;
    private int _column;
    private int _row;

    /// <summary>Creates a PNG sink using the supplied options.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured dimension or font size is not positive.</exception>
    public PngSink(PngSinkOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _cells = new Cell[_options.Rows, _options.Columns];
        WindowTitle = SanitizeTitle(_options.Title);
    }

    /// <inheritdoc />
    public int? SoftMaxWidth => _options.Columns;
    /// <inheritdoc />
    public int? SoftMaxHeight => _options.Rows;
    /// <inheritdoc />
    public int? MaxWidth => _options.Columns;
    /// <inheritdoc />
    public int? MaxHeight => _options.Rows;

    /// <summary>The current sanitized window title, from options or the latest <see cref="SetWindowTitle"/> call.</summary>
    public string? WindowTitle { get; private set; }

    /// <inheritdoc />
    public void Write(CliTextSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);

        foreach (var rune in segment.Text.EnumerateRunes())
        {
            if (rune.Value == '\r')
                continue;
            if (rune.Value == '\n')
            {
                NewLine();
                continue;
            }

            WriteRune(rune, segment.Style);
        }
    }

    /// <inheritdoc />
    public void NewLine()
    {
        _column = 0;
        _row++;
    }

    /// <inheritdoc />
    public void Flush()
    {
    }

    /// <inheritdoc />
    public void Reset()
    {
        _column = 0;
        _row = 0;
        Array.Clear(_cells);
    }

    /// <inheritdoc />
    public void SetWindowTitle(string title)
    {
        WindowTitle = SanitizeTitle(title);
    }

    /// <summary>Renders the captured grid to PNG bytes.</summary>
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        Save(stream);
        return stream.ToArray();
    }

    /// <summary>Renders the captured grid as PNG and writes it to <paramref name="stream"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var terminalFonts = PngTypefaceSet.Load(_options.TerminalFont);
        using var titleFonts = PngTypefaceSet.Load(_options.TitleFont);
        using var symbolFonts = PngTypefaceSet.Load(PngFontSource.BundledNotoSansSymbols2);
        using var terminalRegular = new SKFont(terminalFonts.Regular, _options.TerminalFontSize);
        terminalRegular.Edging = SKFontEdging.Antialias;
        terminalRegular.Hinting = SKFontHinting.Full;

        var metrics = terminalRegular.Metrics;
        int cellWidth = Math.Max(1, (int)Math.Ceiling(terminalRegular.MeasureText("W")));
        int cellHeight = Math.Max(1, (int)Math.Ceiling(metrics.Descent - metrics.Ascent + metrics.Leading));
        float terminalBaseline = -metrics.Ascent;

        using var titleRegular = new SKFont(titleFonts.Regular, _options.TitleFontSize);
        titleRegular.Edging = SKFontEdging.Antialias;
        titleRegular.Hinting = SKFontHinting.Full;

        int titleBarHeight = _options.Chrome == PngWindowChrome.FrameAndTitle
            ? Math.Max(1, (int)Math.Ceiling(titleRegular.Metrics.Descent - titleRegular.Metrics.Ascent) + (2 * TitleVerticalPadding))
            : 0;

        int contentWidth = checked(_options.Columns * cellWidth);
        int contentHeight = checked(_options.Rows * cellHeight);
        int imageWidth = contentWidth;
        int imageHeight = contentHeight;
        int contentX = 0;
        int contentY = 0;

        if (_options.Chrome == PngWindowChrome.FrameAndTitle)
        {
            imageWidth = checked(contentWidth + (2 * FrameThickness));
            imageHeight = checked(contentHeight + titleBarHeight + (2 * FrameThickness));
            contentX = FrameThickness;
            contentY = FrameThickness + titleBarHeight;
        }

        using var bitmap = new SKBitmap(imageWidth, imageHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(ToSkColor(_options.TerminalBackground));

        if (_options.Chrome == PngWindowChrome.FrameAndTitle)
        {
            using var titleIcon = LoadTitleBarAssetBitmap(
                _options.TitleBarIcon.Kind,
                _options.TitleBarIcon.Path,
                _options.TitleBarIcon.Bytes,
                _options.TitleBarIcon.DisplayName,
                "title-bar icon");
            using var titleSymbols = LoadTitleBarAssetBitmap(
                _options.TitleBarSymbols.Kind,
                _options.TitleBarSymbols.Path,
                _options.TitleBarSymbols.Bytes,
                _options.TitleBarSymbols.DisplayName,
                "title-bar symbols");
            DrawChrome(canvas, imageWidth, imageHeight, titleBarHeight, titleRegular, titleIcon, titleSymbols);
        }

        DrawTerminal(canvas, terminalFonts, titleFonts, symbolFonts, cellWidth, cellHeight, terminalBaseline, contentX, contentY);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("SkiaSharp failed to encode PNG output.");
        data.SaveTo(stream);
    }

    private void WriteRune(Rune rune, CliCharStyle style)
    {
        if (_row >= _options.Rows || _column >= _options.Columns)
        {
            if (_options.OverflowMode == PngOverflowMode.Clip)
            {
                _column++;
                return;
            }

            throw new InvalidOperationException(
                $"PNG terminal content exceeds the configured {nameof(PngSinkOptions.Columns)} x {nameof(PngSinkOptions.Rows)} viewport ({_options.Columns} x {_options.Rows}).");
        }

        _cells[_row, _column] = new Cell(
            rune.ToString(),
            style.Foreground,
            style.Background,
            style.Decorations);
        _column++;
    }

    private void DrawTerminal(
        SKCanvas canvas,
        PngTypefaceSet fonts,
        PngTypefaceSet fallbackFonts,
        PngTypefaceSet symbolFallbackFonts,
        int cellWidth,
        int cellHeight,
        float baseline,
        int offsetX,
        int offsetY)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        for (int row = 0; row < _options.Rows; row++)
        {
            for (int column = 0; column < _options.Columns; column++)
            {
                var cell = _cells[row, column];
                float x = offsetX + (column * cellWidth);
                float y = offsetY + (row * cellHeight);

                paint.Color = ToSkColor(cell.Background ?? _options.TerminalBackground);
                canvas.DrawRect(x, y, cellWidth, cellHeight, paint);

                if (string.IsNullOrEmpty(cell.Text))
                    continue;

                using var font = CreateFont(fonts, cell.Decorations, _options.TerminalFontSize);
                using var fallbackFont = CreateFont(fallbackFonts, cell.Decorations, _options.TerminalFontSize);
                using var symbolFallbackFont = CreateFont(symbolFallbackFonts, CliTextDecoration.None, _options.TerminalFontSize);
                var drawFont = ResolveGlyphFont(
                    font,
                    fallbackFont,
                    symbolFallbackFont,
                    cell.Text,
                    fonts.DisplayName,
                    fallbackFonts.DisplayName,
                    symbolFallbackFonts.DisplayName);

                paint.Color = ToSkColor(cell.Foreground ?? _options.DefaultForeground);
                if (IsCellTiledGlyph(cell.Text))
                    DrawCellTiledGlyph(canvas, paint, drawFont, cell.Text, x, y, cellWidth, cellHeight, baseline);
                else
                    canvas.DrawText(cell.Text, x, y + baseline, SKTextAlign.Left, drawFont, paint);

                if ((cell.Decorations & CliTextDecoration.Underline) != 0)
                {
                    paint.StrokeWidth = 1;
                    canvas.DrawLine(x, y + cellHeight - 2, x + cellWidth, y + cellHeight - 2, paint);
                    paint.StrokeWidth = 0;
                }
            }
        }
    }

    private void DrawChrome(
        SKCanvas canvas,
        int imageWidth,
        int imageHeight,
        int titleBarHeight,
        SKFont titleFont,
        SKBitmap? titleIcon,
        SKBitmap? titleSymbols)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        paint.Color = ToSkColor(_options.FrameColor);
        canvas.DrawRect(0, 0, imageWidth, imageHeight, paint);

        paint.Color = ToSkColor(_options.TitleBackground);
        canvas.DrawRect(FrameThickness, FrameThickness, imageWidth - (2 * FrameThickness), titleBarHeight, paint);

        paint.Color = ToSkColor(_options.TitleForeground);
        var title = WindowTitle ?? string.Empty;
        string titleText = string.IsNullOrEmpty(title) ? "TigerCli" : title;
        float symbolsWidth = titleSymbols?.Width ?? 0;
        float titleLeft = FrameThickness + TitleHorizontalPadding;
        float symbolsSpacing = titleSymbols is null ? 0 : TitleControlsSpacing;
        float titleRight = imageWidth - FrameThickness - TitleHorizontalPadding - symbolsWidth - symbolsSpacing;
        float baseline = FrameThickness + TitleVerticalPadding - titleFont.Metrics.Ascent;

        if (titleIcon is not null)
        {
            // Integer division: assets are drawn 1:1 with nearest sampling, so a fractional
            // centering offset would shift them off the pixel grid and smear/clip a row.
            float iconX = FrameThickness + TitleHorizontalPadding;
            float iconY = FrameThickness + ((titleBarHeight - titleIcon.Height) / 2);
            DrawTitleBarAsset(canvas, titleIcon, iconX, iconY);
            titleLeft = iconX + titleIcon.Width + TitleIconTextSpacing;
        }

        canvas.Save();
        canvas.ClipRect(new SKRect(titleLeft, FrameThickness, Math.Max(titleLeft, titleRight), FrameThickness + titleBarHeight));
        canvas.DrawText(titleText, titleLeft, baseline, SKTextAlign.Left, titleFont, paint);
        canvas.Restore();

        if (titleSymbols is not null)
        {
            float symbolsX = imageWidth - FrameThickness - TitleHorizontalPadding - titleSymbols.Width;
            float symbolsY = FrameThickness + ((titleBarHeight - titleSymbols.Height) / 2);
            DrawTitleBarAsset(canvas, titleSymbols, symbolsX, symbolsY);
        }
    }

    private static void DrawTitleBarAsset(SKCanvas canvas, SKBitmap bitmap, float x, float y)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawBitmap(
            bitmap,
            new SKRect(x, y, x + bitmap.Width, y + bitmap.Height),
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
            paint);
    }

    private static SKBitmap? LoadTitleBarAssetBitmap(
        PngTitleBarAssetKind kind,
        string? path,
        byte[]? bytes,
        string displayName,
        string assetDescription)
    {
        switch (kind)
        {
            case PngTitleBarAssetKind.None:
                return null;

            case PngTitleBarAssetKind.EmbeddedResource:
            {
                var assembly = typeof(PngSink).Assembly;
                var resourcePath = path!.Replace('\\', '.').Replace('/', '.');
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith("." + resourcePath, StringComparison.Ordinal));
                if (resourceName is null)
                    throw new InvalidOperationException($"Could not load embedded {assetDescription} '{displayName}'.");

                using var stream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Could not open embedded {assetDescription} '{displayName}'.");
                return DecodeTitleBarAsset(stream, displayName, assetDescription);
            }

            case PngTitleBarAssetKind.File:
            {
                try
                {
                    using var stream = File.OpenRead(path!);
                    return DecodeTitleBarAsset(stream, displayName, assetDescription);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"Could not load {assetDescription} file '{path}'.", ex);
                }
            }

            case PngTitleBarAssetKind.Bytes:
            {
                using var stream = new MemoryStream(bytes!, writable: false);
                return DecodeTitleBarAsset(stream, displayName, assetDescription);
            }

            default:
                throw new InvalidOperationException($"Unsupported {assetDescription} source kind.");
        }
    }

    private static SKBitmap DecodeTitleBarAsset(Stream stream, string displayName, string assetDescription)
    {
        var bitmap = SKBitmap.Decode(stream);
        return bitmap ?? throw new InvalidOperationException($"Could not decode {assetDescription} '{displayName}'.");
    }

    /// <summary>
    /// Whether <paramref name="text"/> is a single glyph from the Box Drawing (U+2500–U+257F) or
    /// Block Elements (U+2580–U+259F) range. These glyphs are designed to tile the font's
    /// fractional design cell edge-to-edge, so they get cell-aware rendering (see
    /// <see cref="DrawCellTiledGlyph"/>).
    /// </summary>
    private static bool IsCellTiledGlyph(string text)
        => text.Length == 1 && text[0] >= '─' && text[0] <= '▟';

    /// <summary>
    /// Draws a box-drawing/block glyph stretched to exactly fill its integer pixel cell.
    /// The pixel cell is the ceiling of the font's fractional design cell (advance × line
    /// height), so drawing these glyphs at their natural size leaves sub-pixel seams between
    /// adjacent cells — visible as faint gaps in frame lines. The full block (U+2588) calibrates
    /// the design cell from its <b>outline</b> bounds (<see cref="SKFont.GetGlyphPath"/>):
    /// unlike <see cref="SKFont.MeasureText(string, out SKRect, SKPaint)"/>, outline bounds are
    /// not device-snapped by hinting, so mapping that rectangle onto the pixel cell makes every
    /// glyph in the range join seamlessly while keeping the font's real outlines.
    /// The draw is clipped to the pixel cell: Cascadia Mono's line glyphs deliberately overshoot
    /// the design cell (e.g. ═ extends ~0.9px past both advance edges) so they overlap in real
    /// terminals, but here each cell already meets its neighbor exactly, and the overshoot would
    /// double-blend on antialiased edge rows — visible as periodic bright ticks at every cell
    /// boundary. Clipping keeps the overshoot's guaranteed edge coverage inside the cell without
    /// bleeding into the neighbor.
    /// </summary>
    private static void DrawCellTiledGlyph(
        SKCanvas canvas,
        SKPaint paint,
        SKFont font,
        string text,
        float x,
        float y,
        int cellWidth,
        int cellHeight,
        float baseline)
    {
        var designCell = SKRect.Empty;
        var blockGlyphs = font.GetGlyphs("█");
        if (blockGlyphs.Length == 1 && blockGlyphs[0] != 0)
        {
            using var blockOutline = font.GetGlyphPath(blockGlyphs[0]);
            if (blockOutline is not null)
                designCell = blockOutline.Bounds;
        }

        if (designCell.Width <= 0 || designCell.Height <= 0)
            font.MeasureText("█", out designCell, paint);

        if (designCell.Width <= 0 || designCell.Height <= 0)
        {
            // The font has no usable full-block reference (never the case for the bundled
            // Cascadia Mono); fall back to plain glyph placement.
            canvas.DrawText(text, x, y + baseline, SKTextAlign.Left, font, paint);
            return;
        }

        canvas.Save();
        canvas.Translate(x, y);
        canvas.ClipRect(new SKRect(0, 0, cellWidth, cellHeight));
        canvas.Scale(cellWidth / designCell.Width, cellHeight / designCell.Height);
        canvas.DrawText(text, -designCell.Left, -designCell.Top, SKTextAlign.Left, font, paint);
        canvas.Restore();
    }

    private static SKFont CreateFont(PngTypefaceSet fonts, CliTextDecoration decorations, float size)
    {
        bool bold = (decorations & CliTextDecoration.Bold) != 0;
        bool italic = (decorations & CliTextDecoration.Italic) != 0;
        var typeface = fonts.Resolve(bold, italic);
        return new SKFont(typeface, size)
        {
            Edging = SKFontEdging.Antialias,
            Hinting = SKFontHinting.Full
        };
    }

    private static SKFont ResolveGlyphFont(
        SKFont font,
        SKFont fallbackFont,
        SKFont symbolFallbackFont,
        string text,
        string fontName,
        string fallbackFontName,
        string symbolFallbackFontName)
    {
        if (font.ContainsGlyphs(text))
            return font;

        if (fallbackFont.ContainsGlyphs(text))
            return fallbackFont;

        if (symbolFallbackFont.ContainsGlyphs(text))
            return symbolFallbackFont;

        foreach (var rune in text.EnumerateRunes())
        {
            var s = rune.ToString();
            if (!font.ContainsGlyphs(s) && !fallbackFont.ContainsGlyphs(s) && !symbolFallbackFont.ContainsGlyphs(s))
                throw new InvalidOperationException($"Bundled fonts '{fontName}', '{fallbackFontName}', and '{symbolFallbackFontName}' do not contain a glyph for U+{rune.Value:X4}.");
        }

        throw new InvalidOperationException($"Bundled fonts '{fontName}', '{fallbackFontName}', and '{symbolFallbackFontName}' do not contain all glyphs required for '{text}'.");
    }

    private static SKColor ToSkColor(CliColor color)
    {
        var (r, g, b) = CliColorPalette.GetRgb(color);
        return new SKColor(r, g, b);
    }

    private static string? SanitizeTitle(string? title)
    {
        if (title is null)
            return null;

        StringBuilder? builder = null;
        for (int i = 0; i < title.Length; i++)
        {
            if (char.IsControl(title[i]))
            {
                builder ??= new StringBuilder(title.Length).Append(title, 0, i);
            }
            else
            {
                builder?.Append(title[i]);
            }
        }

        return builder?.ToString() ?? title;
    }

    private readonly record struct Cell(
        string? Text,
        CliColor? Foreground,
        CliColor? Background,
        CliTextDecoration Decorations);

    private sealed class PngTypefaceSet : IDisposable
    {
        private PngTypefaceSet(
            string displayName,
            SKTypeface regular,
            SKTypeface? bold,
            SKTypeface? italic,
            SKTypeface? boldItalic)
        {
            DisplayName = displayName;
            Regular = regular;
            Bold = bold;
            Italic = italic;
            BoldItalic = boldItalic;
        }

        public string DisplayName { get; }
        public SKTypeface Regular { get; }
        private SKTypeface? Bold { get; }
        private SKTypeface? Italic { get; }
        private SKTypeface? BoldItalic { get; }

        public static PngTypefaceSet Load(PngFontSource source)
        {
            return new PngTypefaceSet(
                source.DisplayName,
                LoadRequired(source, source.Regular, "regular"),
                LoadOptional(source, source.Bold, "bold"),
                LoadOptional(source, source.Italic, "italic"),
                LoadOptional(source, source.BoldItalic, "bold italic"));
        }

        public SKTypeface Resolve(bool bold, bool italic)
        {
            return (bold, italic) switch
            {
                (false, false) => Regular,
                (true, false) => Bold ?? throw MissingStyle("bold"),
                (false, true) => Italic ?? throw MissingStyle("italic"),
                (true, true) => BoldItalic ?? throw MissingStyle("bold italic"),
            };
        }

        public void Dispose()
        {
            Regular.Dispose();
            Bold?.Dispose();
            Italic?.Dispose();
            BoldItalic?.Dispose();
        }

        private InvalidOperationException MissingStyle(string style)
            => new($"Font '{DisplayName}' does not provide a bundled static {style} face. Synthetic font styles are not used by PngSink v1.");

        private static SKTypeface LoadRequired(PngFontSource source, string name, string role)
            => LoadTypeface(source, name, role)
            ?? throw new InvalidOperationException($"Could not load {role} font '{name}' for '{source.DisplayName}'.");

        private static SKTypeface? LoadOptional(PngFontSource source, string? name, string role)
            => name is null ? null : LoadTypeface(source, name, role);

        private static SKTypeface? LoadTypeface(PngFontSource source, string name, string role)
        {
            try
            {
                return source.Kind switch
                {
                    PngFontSourceKind.EmbeddedResource => LoadEmbedded(name),
                    PngFontSourceKind.File => SKTypeface.FromFile(name),
                    _ => throw new InvalidOperationException($"Unsupported font source kind for {role} font.")
                };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Could not load {role} font '{name}' for '{source.DisplayName}'.", ex);
            }
        }

        private static SKTypeface? LoadEmbedded(string fileName)
        {
            var assembly = typeof(PngSink).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(".Fonts." + fileName, StringComparison.Ordinal));

            if (resourceName is null)
                return null;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            return stream is null ? null : SKTypeface.FromStream(stream);
        }
    }
}
