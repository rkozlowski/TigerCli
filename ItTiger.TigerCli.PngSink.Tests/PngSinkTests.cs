using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using SkiaSharp;

namespace ItTiger.TigerCli.PngSink.Tests;

public sealed class PngSinkTests
{
    [Fact]
    public void Options_Validate_PositiveTerminalSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PngSink(new PngSinkOptions { Columns = 0, Rows = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PngSink(new PngSinkOptions { Columns = 1, Rows = 0 }));
    }

    [Fact]
    public void Sink_ExposesTerminalConstraints()
    {
        var sink = new PngSink(new PngSinkOptions { Columns = 12, Rows = 4 });

        Assert.Equal(12, sink.SoftMaxWidth);
        Assert.Equal(4, sink.SoftMaxHeight);
        Assert.Equal(12, sink.MaxWidth);
        Assert.Equal(4, sink.MaxHeight);
    }

    [Fact]
    public void Options_DefaultTitleFont_UsesBundledCascadiaMono()
    {
        var options = new PngSinkOptions { Columns = 1, Rows = 1 };

        Assert.Same(PngFontSource.BundledCascadiaMono, options.TitleFont);
        Assert.Same(PngFontSource.BundledCascadiaMono, options.TerminalFont);
    }

    [Fact]
    public void Options_DefaultTitleChrome_UsesLargerCascadiaTitleAndGray15Background()
    {
        var options = new PngSinkOptions { Columns = 1, Rows = 1 };

        Assert.Equal(16, options.TitleFontSize);
        Assert.Equal(18, options.TerminalFontSize);
        Assert.Equal(CliColor.Gray15, options.TitleBackground);
    }

    [Fact]
    public void FrameAndTitle_DefaultTitleFont_RendersBrailleSpinnerFrame()
    {
        var sink = new PngSink(new PngSinkOptions
        {
            Columns = 4,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = "\u2816 App"
        });

        sink.Write(new CliTextSegment("OK", new CliCharStyle(CliColor.White)));

        AssertPngSignature(sink.ToBytes());
    }

    [Fact]
    public void ToBytes_ReturnsValidPng()
    {
        var sink = new PngSink(new PngSinkOptions { Columns = 4, Rows = 1 });
        sink.Write(new CliTextSegment("Hi", new CliCharStyle(CliColor.White)));

        var bytes = sink.ToBytes();

        AssertPngSignature(bytes);
        using var bitmap = Decode(bytes);
        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
    }

    [Fact]
    public void ImageDimensions_AreDerivedFromCells()
    {
        var one = RenderText("A", new PngSinkOptions { Columns = 1, Rows = 1 });
        var two = RenderText("A", new PngSinkOptions { Columns = 2, Rows = 2 });

        using var oneBitmap = Decode(one);
        using var twoBitmap = Decode(two);

        Assert.Equal(oneBitmap.Width * 2, twoBitmap.Width);
        Assert.Equal(oneBitmap.Height * 2, twoBitmap.Height);
    }

    [Fact]
    public void Flush_IsRepeatableAndDoesNotFinalizeOutput()
    {
        var sink = new PngSink(new PngSinkOptions { Columns = 3, Rows = 1 });
        sink.Write(new CliTextSegment("A", new CliCharStyle(CliColor.White)));
        sink.Flush();
        var before = NormalizedPixelHash(sink.ToBytes());
        sink.Flush();
        sink.Flush();
        var after = NormalizedPixelHash(sink.ToBytes());

        Assert.Equal(before, after);
    }

    [Fact]
    public void BasicTextRendering_ChangesPixelsFromBackground()
    {
        var bytes = RenderText("A", new PngSinkOptions
        {
            Columns = 1,
            Rows = 1,
            DefaultForeground = CliColor.White,
            TerminalBackground = CliColor.Black
        });

        using var bitmap = Decode(bytes);
        var background = ToSkColor(CliColor.Black);
        Assert.Contains(AllPixels(bitmap), pixel => pixel != background);
    }

    [Fact]
    public void ForegroundAndBackground_UseCliColorPalette()
    {
        var sink = new PngSink(new PngSinkOptions
        {
            Columns = 1,
            Rows = 1,
            TerminalBackground = CliColor.Black
        });
        sink.Write(new CliTextSegment(" ", new CliCharStyle(CliColor.White, CliColor.OceanBlue)));

        using var bitmap = Decode(sink.ToBytes());

        Assert.Equal(ToSkColor(CliColor.OceanBlue), bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void Defaults_AreUsedForMissingForegroundAndBackground()
    {
        var sink = new PngSink(new PngSinkOptions
        {
            Columns = 1,
            Rows = 1,
            DefaultForeground = CliColor.Yellow,
            TerminalBackground = CliColor.DarkRed
        });
        sink.Write(new CliTextSegment(" ", new CliCharStyle(null)));

        using var bitmap = Decode(sink.ToBytes());

        Assert.Equal(ToSkColor(CliColor.DarkRed), bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void Overflow_ThrowsByDefault()
    {
        var sink = new PngSink(new PngSinkOptions { Columns = 1, Rows = 1 });

        Assert.Throws<InvalidOperationException>(() => sink.Write(new CliTextSegment("AB", new CliCharStyle(CliColor.White))));
    }

    [Fact]
    public void Overflow_Clip_IgnoresContentOutsideViewport()
    {
        var sink = new PngSink(new PngSinkOptions
        {
            Columns = 1,
            Rows = 1,
            OverflowMode = PngOverflowMode.Clip,
            TerminalBackground = CliColor.Black
        });

        sink.Write(new CliTextSegment("AB", new CliCharStyle(CliColor.White, CliColor.Red)));

        using var bitmap = Decode(sink.ToBytes());
        Assert.Equal(ToSkColor(CliColor.Red), bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void Title_IsStoredAndSanitized()
    {
        var sink = new PngSink(new PngSinkOptions { Columns = 2, Rows = 1, Title = "Old" });

        sink.SetWindowTitle("A\u001bB");

        Assert.Equal("AB", sink.WindowTitle);
    }

    [Fact]
    public void FrameAndTitle_AddsChromeAndDrawsFrameColor()
    {
        var plain = RenderText("A", new PngSinkOptions { Columns = 2, Rows = 1 });
        var chrome = RenderText("A", new PngSinkOptions
        {
            Columns = 2,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = "TigerCli",
            FrameColor = CliColor.Red,
            TitleBackground = CliColor.DarkBlue
        });

        using var plainBitmap = Decode(plain);
        using var chromeBitmap = Decode(chrome);

        Assert.True(chromeBitmap.Width > plainBitmap.Width);
        Assert.True(chromeBitmap.Height > plainBitmap.Height);
        Assert.Equal(ToSkColor(CliColor.Red), chromeBitmap.GetPixel(0, 0));
        Assert.Equal(ToSkColor(CliColor.DarkBlue), chromeBitmap.GetPixel(1, 1));
    }

    [Fact]
    public void FrameAndTitle_DefaultTitleBarAssets_UseAssetsPathsAndNativeDimensions()
    {
        Assert.Equal("Assets/tc_term_ico.png", PngTitleBarIcon.Default.Path);
        Assert.Equal("Assets/tc_window_symbols.png", PngTitleBarSymbols.Default.Path);

        using var icon = DecodeEmbeddedAsset(PngTitleBarIcon.Default.Path!);
        using var symbols = DecodeEmbeddedAsset(PngTitleBarSymbols.Default.Path!);

        Assert.Equal(20, icon.Width);
        Assert.Equal(20, icon.Height);
        Assert.Equal(72, symbols.Width);
        Assert.Equal(20, symbols.Height);
    }

    [Fact]
    public void FrameAndTitle_LongTitle_DoesNotBleedIntoWindowControls()
    {
        using var baseline = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 40,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = " ",
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.Yellow
        }));
        using var longTitle = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 40,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = new string('W', 120),
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.Yellow
        }));

        Assert.Equal(RightTitleBarRegionHash(baseline), RightTitleBarRegionHash(longTitle));
    }

    [Fact]
    public void FrameAndTitle_RendersCustomTitleBarAssetsAtNativeDimensions()
    {
        using var bitmap = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 16,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = " ",
            TitleBarIcon = PngTitleBarIcon.FromBytes(CreateSolidPngBytes(20, 20, SKColors.Red)),
            TitleBarSymbols = PngTitleBarSymbols.FromBytes(CreateSolidPngBytes(72, 20, SKColors.Green)),
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.White
        }));

        Assert.Equal(20 * 20, CountPixels(AllPixels(bitmap), pixel => pixel == SKColors.Red));
        Assert.Equal(72 * 20, CountPixels(AllPixels(bitmap), pixel => pixel == SKColors.Green));
    }

    [Fact]
    public void FrameAndTitle_RendersDefaultTitleBarIcon()
    {
        using var bitmap = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 16,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = " ",
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.Yellow
        }));

        var titleBackground = ToSkColor(CliColor.DarkBlue);
        Assert.True(CountPixels(TitleIconRegion(bitmap), pixel => pixel != titleBackground) > 20);
    }

    [Fact]
    public void FrameAndTitle_RendersDefaultTitleBarSymbols()
    {
        using var bitmap = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 16,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = " ",
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.Yellow
        }));

        var titleBackground = ToSkColor(CliColor.DarkBlue);
        Assert.True(CountPixels(TitleSymbolsRegion(bitmap, 72, 20), pixel => pixel != titleBackground) > 20);
    }

    [Fact]
    public void FrameAndTitle_TitleTextStartsAfterIcon()
    {
        using var withIcon = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 20,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = "Title",
            TitleBarIcon = PngTitleBarIcon.Default,
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.Yellow
        }));
        using var withoutIcon = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 20,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = "Title",
            TitleBarIcon = PngTitleBarIcon.None,
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.Yellow
        }));

        int withIconTitleX = FirstTitleForegroundX(withIcon);
        int withoutIconTitleX = FirstTitleForegroundX(withoutIcon);

        Assert.True(withIconTitleX > withoutIconTitleX + 12,
            $"Title should be shifted past the icon. With icon: {withIconTitleX}; without icon: {withoutIconTitleX}.");
    }

    [Fact]
    public void FrameAndTitle_RendersCustomTitleBarIcon()
    {
        using var bitmap = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 16,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = " ",
            TitleBarIcon = PngTitleBarIcon.FromBytes(CreateSolidPngBytes(20, 20, SKColors.Red)),
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.White
        }));

        Assert.True(CountPixels(TitleIconRegion(bitmap), pixel => pixel.Red > 180 && pixel.Green < 50 && pixel.Blue < 50) > 150);
    }

    [Fact]
    public void FrameAndTitle_RendersCustomTitleBarSymbols()
    {
        using var bitmap = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 16,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = " ",
            TitleBarSymbols = PngTitleBarSymbols.FromBytes(CreateSolidPngBytes(72, 20, SKColors.Green)),
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.White
        }));

        Assert.Equal(72 * 20, CountPixels(TitleSymbolsRegion(bitmap, 72, 20), pixel => pixel == SKColors.Green));
    }

    [Fact]
    public void FrameAndTitle_TitleBarIconCanBeDisabled()
    {
        using var bitmap = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 16,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = " ",
            TitleBarIcon = PngTitleBarIcon.None,
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.Yellow
        }));

        var titleBackground = ToSkColor(CliColor.DarkBlue);
        Assert.Equal(0, CountPixels(TitleIconRegion(bitmap), pixel => pixel != titleBackground));
    }

    [Fact]
    public void FrameAndTitle_TitleBarSymbolsCanBeDisabled()
    {
        using var bitmap = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 16,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            Title = " ",
            TitleBarSymbols = PngTitleBarSymbols.None,
            TitleBackground = CliColor.DarkBlue,
            TitleForeground = CliColor.Yellow
        }));

        var titleBackground = ToSkColor(CliColor.DarkBlue);
        Assert.Equal(0, CountPixels(TitleSymbolsRegion(bitmap, 72, 20), pixel => pixel != titleBackground));
    }

    [Fact]
    public void TitleBarAssets_AreIgnoredWithoutChrome()
    {
        var withIcon = RenderText(" ", new PngSinkOptions
        {
            Columns = 2,
            Rows = 1,
            Chrome = PngWindowChrome.None,
            TitleBarIcon = PngTitleBarIcon.Default,
            TitleBarSymbols = PngTitleBarSymbols.Default,
            TerminalBackground = CliColor.Black
        });
        var withoutIcon = RenderText(" ", new PngSinkOptions
        {
            Columns = 2,
            Rows = 1,
            Chrome = PngWindowChrome.None,
            TitleBarIcon = PngTitleBarIcon.None,
            TitleBarSymbols = PngTitleBarSymbols.None,
            TerminalBackground = CliColor.Black
        });

        Assert.Equal(NormalizedPixelHash(withoutIcon), NormalizedPixelHash(withIcon));
    }

    [Fact]
    public void FrameAndTitle_IconDoesNotChangeImageDimensions()
    {
        using var defaultIcon = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 8,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            TitleBarIcon = PngTitleBarIcon.Default
        }));
        using var customIcon = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 8,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            TitleBarIcon = PngTitleBarIcon.FromBytes(CreateSolidPngBytes(20, 20, SKColors.Red))
        }));
        using var noIcon = Decode(RenderText(" ", new PngSinkOptions
        {
            Columns = 8,
            Rows = 1,
            Chrome = PngWindowChrome.FrameAndTitle,
            TitleBarIcon = PngTitleBarIcon.None
        }));

        Assert.Equal(defaultIcon.Width, customIcon.Width);
        Assert.Equal(defaultIcon.Height, customIcon.Height);
        Assert.Equal(defaultIcon.Width, noIcon.Width);
        Assert.Equal(defaultIcon.Height, noIcon.Height);
    }

    [Fact]
    public void MissingGlyph_FailsLoudly()
    {
        var sink = new PngSink(new PngSinkOptions { Columns = 1, Rows = 1 });
        sink.Write(new CliTextSegment("\uE000", new CliCharStyle(CliColor.White)));

        var ex = Assert.Throws<InvalidOperationException>(() => sink.ToBytes());
        Assert.Contains("U+E000", ex.Message);
    }

    [Fact]
    public void MissingFont_FailsLoudly()
    {
        var sink = new PngSink(new PngSinkOptions
        {
            Columns = 1,
            Rows = 1,
            TerminalFont = PngFontSource.FromFile("missing-font-file.ttf")
        });
        sink.Write(new CliTextSegment("A", new CliCharStyle(CliColor.White)));

        var ex = Assert.Throws<InvalidOperationException>(() => sink.ToBytes());
        Assert.Contains("regular font", ex.Message);
    }

    [Fact]
    public void Helpers_ReturnValidPngBytes()
    {
        var grid = new CliGrid(1, 1);
        grid.Set(0, 0, "Hi");

        var bytes = PngRenderer.RenderGridToBytes(grid, new PngSinkOptions { Columns = 4, Rows = 1 });

        AssertPngSignature(bytes);
    }

    [Theory]
    [InlineData(CliTextDecoration.None)]
    [InlineData(CliTextDecoration.Bold)]
    [InlineData(CliTextDecoration.Italic)]
    [InlineData(CliTextDecoration.Bold | CliTextDecoration.Italic)]
    public void TerminalBundledCascadiaMono_ResolvesAllStaticFaces(CliTextDecoration decorations)
    {
        var sink = new PngSink(new PngSinkOptions
        {
            Columns = 4,
            Rows = 1,
            TerminalFont = PngFontSource.BundledCascadiaMono
        });

        sink.Write(new CliTextSegment("Ab", new CliCharStyle(CliColor.White, decorations: decorations)));

        AssertPngSignature(sink.ToBytes());
    }

    [Fact]
    public void TerminalBundledCascadiaMono_CoversDocumentationGlyphs()
    {
        var text = "┌┐└┘─│├┤┬┴┼░▒▓█⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏→›▲▼✓✗Zażółć gęślą jaźń";
        var sink = new PngSink(new PngSinkOptions
        {
            Columns = text.EnumerateRunes().Count(),
            Rows = 1,
            TerminalFont = PngFontSource.BundledCascadiaMono
        });

        sink.Write(new CliTextSegment(text, new CliCharStyle(CliColor.White)));

        AssertPngSignature(sink.ToBytes());
    }

    // ---- Box-drawing / block glyph cell tiling ----
    //
    // Box-drawing and block glyphs are stretched onto the integer pixel cell (the fractional font
    // design cell rounds up), so adjacent glyphs must join without seams. These checks are
    // coverage-based, not byte snapshots: a seam shows up as a dim pixel row/column at a cell
    // boundary (pre-fix values were below 200), while clean output stays at ~255.
    private const byte MinInkBrightness = 240;

    [Fact]
    public void FullBlockGrid_HasNoSeamsBetweenCells()
    {
        using var bitmap = Decode(RenderRows(["████", "████", "████"]));

        int min = 255;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                min = Math.Min(min, bitmap.GetPixel(x, y).Red);

        Assert.True(min >= MinInkBrightness,
            $"A grid of full blocks must fill every pixel; dimmest pixel was {min}.");
    }

    [Fact]
    public void HorizontalFrameLine_IsContinuousAcrossCells()
    {
        using var bitmap = Decode(RenderRows(["────────"]));

        for (int x = 0; x < bitmap.Width; x++)
        {
            int columnMax = 0;
            for (int y = 0; y < bitmap.Height; y++)
                columnMax = Math.Max(columnMax, bitmap.GetPixel(x, y).Red);

            Assert.True(columnMax >= MinInkBrightness,
                $"Horizontal frame run has a gap at pixel column {x} (max brightness {columnMax}).");
        }
    }

    [Fact]
    public void VerticalFrameLine_IsContinuousAcrossRows()
    {
        using var bitmap = Decode(RenderRows(["│", "│", "│", "│"]));

        for (int y = 0; y < bitmap.Height; y++)
        {
            int rowMax = 0;
            for (int x = 0; x < bitmap.Width; x++)
                rowMax = Math.Max(rowMax, bitmap.GetPixel(x, y).Red);

            Assert.True(rowMax >= MinInkBrightness,
                $"Vertical frame run has a gap at pixel row {y} (max brightness {rowMax}).");
        }
    }

    [Fact]
    public void HorizontalDoubleFrameLine_HasNoBoundaryTicksBetweenCells()
    {
        // Cascadia's line glyphs overshoot the cell; without clipping, adjacent glyphs
        // double-blend on antialiased edge rows, producing a brighter tick at every cell
        // boundary column (pre-fix: 174 vs 133 on the same row). Every pixel row must be
        // uniform across the full run.
        using var bitmap = Decode(RenderRows(["════════"]));

        for (int y = 0; y < bitmap.Height; y++)
        {
            int rowMin = 255, rowMax = 0;
            for (int x = 0; x < bitmap.Width; x++)
            {
                int v = bitmap.GetPixel(x, y).Red;
                rowMin = Math.Min(rowMin, v);
                rowMax = Math.Max(rowMax, v);
            }

            Assert.True(rowMax - rowMin <= 1,
                $"Double-frame row y={y} is not uniform across cells (min {rowMin}, max {rowMax}); boundary ticks visible.");
        }
    }

    [Fact]
    public void VerticalDoubleFrameLine_HasNoBoundaryTicksBetweenRows()
    {
        using var bitmap = Decode(RenderRows(["║", "║", "║", "║"]));

        for (int x = 0; x < bitmap.Width; x++)
        {
            int colMin = 255, colMax = 0;
            for (int y = 0; y < bitmap.Height; y++)
            {
                int v = bitmap.GetPixel(x, y).Red;
                colMin = Math.Min(colMin, v);
                colMax = Math.Max(colMax, v);
            }

            Assert.True(colMax - colMin <= 1,
                $"Double-frame column x={x} is not uniform across rows (min {colMin}, max {colMax}); boundary ticks visible.");
        }
    }

    [Fact]
    public void BoxGlyphStretching_KeepsExactCellGeometry()
    {
        // Stretched glyphs must not change the cell raster: a frame render is exactly
        // (columns x cell width) by (rows x cell height), same as plain text.
        using var reference = Decode(RenderText("A", new PngSinkOptions { Columns = 1, Rows = 1 }));
        using var frame = Decode(RenderRows(["┌─┬─┐", "├─┼─┤", "└─┴─┘"]));

        Assert.Equal(reference.Width * 5, frame.Width);
        Assert.Equal(reference.Height * 3, frame.Height);
    }

    private static byte[] RenderRows(string[] rows)
    {
        var sink = new PngSink(new PngSinkOptions
        {
            Columns = rows.Max(static r => r.Length),
            Rows = rows.Length,
            TerminalBackground = CliColor.Black,
        });
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0)
                sink.NewLine();
            sink.Write(new CliTextSegment(rows[i], new CliCharStyle(CliColor.White)));
        }

        return sink.ToBytes();
    }

    private static byte[] RenderText(string text, PngSinkOptions options)
    {
        var sink = new PngSink(options);
        sink.Write(new CliTextSegment(text, new CliCharStyle(CliColor.White)));
        return sink.ToBytes();
    }

    private static SKBitmap Decode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        var bitmap = SKBitmap.Decode(data);
        Assert.NotNull(bitmap);
        return bitmap;
    }

    private static void AssertPngSignature(byte[] bytes)
    {
        Assert.True(bytes.Length >= 4);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    private static IEnumerable<SKColor> AllPixels(SKBitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
                yield return bitmap.GetPixel(x, y);
        }
    }

    private static string NormalizedPixelHash(byte[] bytes)
    {
        using var bitmap = Decode(bytes);
        var hash = new HashCode();
        hash.Add(bitmap.Width);
        hash.Add(bitmap.Height);
        foreach (var pixel in AllPixels(bitmap))
            hash.Add(pixel);
        return hash.ToHashCode().ToString("X8");
    }

    private static IEnumerable<SKColor> TitleIconRegion(SKBitmap bitmap)
    {
        const int x = 9;
        const int width = 20;
        const int height = 20;
        int y = TitleBarAssetY(bitmap, height);

        return BitmapRegion(bitmap, x, y, width, height);
    }

    private static IEnumerable<SKColor> TitleSymbolsRegion(SKBitmap bitmap, int width, int height)
    {
        int x = bitmap.Width - 1 - 8 - width;
        int y = TitleBarAssetY(bitmap, height);

        return BitmapRegion(bitmap, x, y, width, height);
    }

    private static int TitleBarAssetY(SKBitmap bitmap, int assetHeight)
    {
        var titleBackground = bitmap.GetPixel(1, 1);
        int titleBarHeight = 0;

        for (int y = 1; y < bitmap.Height; y++)
        {
            if (bitmap.GetPixel(1, y) != titleBackground)
                break;

            titleBarHeight++;
        }

        return 1 + ((titleBarHeight - assetHeight) / 2);
    }

    private static IEnumerable<SKColor> BitmapRegion(SKBitmap bitmap, int x, int y, int width, int height)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width && column < bitmap.Width; column++)
                yield return bitmap.GetPixel(column, row);
        }
    }

    private static int FirstTitleForegroundX(SKBitmap bitmap)
    {
        for (int x = 1; x < bitmap.Width; x++)
            for (int y = 1; y < Math.Min(bitmap.Height, 28); y++)
                if (LooksLikeYellowText(bitmap.GetPixel(x, y)))
                    return x;

        throw new InvalidOperationException("No title foreground pixel found.");
    }

    private static bool LooksLikeYellowText(SKColor pixel)
        => pixel.Red > 150 && pixel.Green > 150 && pixel.Blue < 120;

    private static int CountPixels(IEnumerable<SKColor> pixels, Func<SKColor, bool> predicate)
        => pixels.Count(predicate);

    private static string RightTitleBarRegionHash(SKBitmap bitmap)
    {
        var hash = new HashCode();
        int startX = Math.Max(0, bitmap.Width - 50);
        int endY = Math.Min(bitmap.Height, 28);

        for (int y = 1; y < endY; y++)
            for (int x = startX; x < bitmap.Width - 1; x++)
                hash.Add(bitmap.GetPixel(x, y));

        return hash.ToHashCode().ToString("X8");
    }

    private static SKBitmap DecodeEmbeddedAsset(string path)
    {
        var resourcePath = path.Replace('\\', '.').Replace('/', '.');
        var assembly = typeof(PngSink).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("." + resourcePath, StringComparison.Ordinal));
        if (resourceName is null)
            throw new InvalidOperationException($"Embedded asset '{path}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded asset '{path}' could not be opened.");
        return SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException($"Embedded asset '{path}' could not be decoded.");
    }

    private static byte[] CreateSolidPngBytes(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("SkiaSharp failed to encode test icon.");
        return data.ToArray();
    }

    private static SKColor ToSkColor(CliColor color)
    {
        var (r, g, b) = CliColorPalette.GetRgb(color);
        return new SKColor(r, g, b);
    }
}
