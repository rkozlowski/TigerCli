using ItTiger.TigerCli.Enums;
namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Style information applied to grid cells, rows, columns, and table bands.
/// </summary>
/// <remarks>
/// Non-null properties participate in the style cascade. Width/height families validate fixed,
/// minimum, and maximum values against each other; character decorations are additive when styles
/// merge.
/// </remarks>
public class CliCellStyle
{
    private int? _width;
    private int? _minWidth;
    private int? _maxWidth;
    private int? _height;
    private int? _minHeight;
    private int? _maxHeight;

    /// <summary>Fixed cell width. Must be compatible with <see cref="MinWidth"/> and <see cref="MaxWidth"/>.</summary>
    public int? Width
    {
        get => _width;
        set
        {
            if (value is int w)
            {
                if (_minWidth is int min && w < min)
                    throw new ArgumentOutOfRangeException(nameof(Width), $"Width {w} < MinWidth {min}.");
                if (_maxWidth is int max && w > max)
                    throw new ArgumentOutOfRangeException(nameof(Width), $"Width {w} > MaxWidth {max}.");
            }
            _width = value;
        }
    }

    /// <summary>Minimum cell width. Must not exceed <see cref="MaxWidth"/> or an existing fixed <see cref="Width"/>.</summary>
    public int? MinWidth
    {
        get => _minWidth;
        set
        {
            if (value is int min)
            {
                if (_maxWidth is int max && min > max)
                    throw new ArgumentOutOfRangeException(nameof(MinWidth), $"MinWidth {min} > MaxWidth {max}.");
                if (_width is int w && w < min)
                    throw new ArgumentOutOfRangeException(nameof(MinWidth), $"Current Width {w} < MinWidth {min}.");
            }
            _minWidth = value;
        }
    }

    /// <summary>Maximum cell width. Must not be less than <see cref="MinWidth"/> or an existing fixed <see cref="Width"/>.</summary>
    public int? MaxWidth
    {
        get => _maxWidth;
        set
        {
            if (value is int max)
            {
                if (_minWidth is int min && max < min)
                    throw new ArgumentOutOfRangeException(nameof(MaxWidth), $"MaxWidth {max} < MinWidth {min}.");
                if (_width is int w && w > max)
                    throw new ArgumentOutOfRangeException(nameof(MaxWidth), $"Current Width {w} > MaxWidth {max}.");
            }
            _maxWidth = value;
        }
    }

    /// <summary>Effective minimum width: <see cref="MinWidth"/>, then <see cref="Width"/>, then <c>0</c>.</summary>
    public int EffectiveMinWidth => _minWidth ?? _width ?? 0;

    /// <summary>Effective maximum width: <see cref="MaxWidth"/>, then <see cref="Width"/>, then <see cref="int.MaxValue"/>.</summary>
    public int EffectiveMaxWidth => _maxWidth ?? _width ?? int.MaxValue;

    /// <summary>Returns whether a locked width is compatible with this style's width constraints.</summary>
    public bool IsWidthCompatible(int width) =>
        width >= EffectiveMinWidth && width <= EffectiveMaxWidth &&
        (!_width.HasValue || _width.Value == width);

    /// <summary>Fixed cell height. Must be compatible with <see cref="MinHeight"/> and <see cref="MaxHeight"/>.</summary>
    public int? Height
    {
        get => _height;
        set
        {
            if (value is int h)
            {
                if (_minHeight is int min && h < min)
                    throw new ArgumentOutOfRangeException(nameof(Height), $"Height {h} < MinHeight {min}.");
                if (_maxHeight is int max && h > max)
                    throw new ArgumentOutOfRangeException(nameof(Height), $"Height {h} > MaxHeight {max}.");
            }
            _height = value;
        }
    }

    /// <summary>Minimum cell height. Must not exceed <see cref="MaxHeight"/> or an existing fixed <see cref="Height"/>.</summary>
    public int? MinHeight
    {
        get => _minHeight;
        set
        {
            if (value is int min)
            {
                if (_maxHeight is int max && min > max)
                    throw new ArgumentOutOfRangeException(nameof(MinHeight), $"MinHeight {min} > MaxHeight {max}.");
                if (_height is int h && h < min)
                    throw new ArgumentOutOfRangeException(nameof(MinHeight), $"Current Height {h} < MinHeight {min}.");
            }
            _minHeight = value;
        }
    }

    /// <summary>Maximum cell height. Must not be less than <see cref="MinHeight"/> or an existing fixed <see cref="Height"/>.</summary>
    public int? MaxHeight
    {
        get => _maxHeight;
        set
        {
            if (value is int max)
            {
                if (_minHeight is int min && max < min)
                    throw new ArgumentOutOfRangeException(nameof(MaxHeight), $"MaxHeight {max} < MinHeight {min}.");
                if (_height is int h && h > max)
                    throw new ArgumentOutOfRangeException(nameof(MaxHeight), $"Current Height {h} > MaxHeight {max}.");
            }
            _maxHeight = value;
        }
    }

    /// <summary>Effective minimum height: <see cref="MinHeight"/>, then <see cref="Height"/>, then <c>0</c>.</summary>
    public int EffectiveMinHeight => _minHeight ?? _height ?? 0;

    /// <summary>Effective maximum height: <see cref="MaxHeight"/>, then <see cref="Height"/>, then <see cref="int.MaxValue"/>.</summary>
    public int EffectiveMaxHeight => _maxHeight ?? _height ?? int.MaxValue;

    /// <summary>Returns whether a locked height is compatible with this style's height constraints.</summary>
    public bool IsHeightCompatible(int height) =>
        height >= EffectiveMinHeight && height <= EffectiveMaxHeight &&
        (!_height.HasValue || _height.Value == height);

    /// <summary>Optional horizontal alignment for cell content.</summary>
    public CliTextAlignment? HorizontalAlignment { get; set; }

    /// <summary>Optional vertical alignment for cell content.</summary>
    public CliVerticalAlignment? VerticalAlignment { get; set; }

    /// <summary>Optional horizontal cell padding.</summary>
    public CliCellPadding? Padding { get; set; }

    /// <summary>Optional wrapping/truncation policy.</summary>
    public CliWrapping? Wrapping { get; set; }

    /// <summary>Optional formatting mode used before markup parsing.</summary>
    public CliFormattingMode? FormattingMode { get; set; }

    /// <summary>Text rendered when a cell value is <c>null</c>.</summary>
    public string? NullDisplayValue { get; set; }

    /// <summary>Optional value formatter used by raw-formatting paths.</summary>
    public CliFormatter? Formatter { get; set; }

    /// <summary>Optional character style for foreground, background, decorations, and hyperlinks.</summary>
    public CliCharStyle? CharStyle { get; set; }

    /// <summary>
    /// Marks the cell's visible value as a hyperlink: when <c>true</c>, the render pipeline derives a
    /// hyperlink target once from the cell's full visible text (see <see cref="ItTiger.TigerCli.Rendering.CliGrid"/>)
    /// unless a text segment already carries an explicit <see cref="CliCharStyle.HyperlinkTarget"/>
    /// (explicit always wins). Nullable so it participates in the style cascade like other properties.
    /// This is metadata only — it never changes colours, layout, or the visible text.
    /// </summary>
    public bool? IsHyperlink { get; set; }

    /// <summary>
    /// Creates a cell style with an optional character style.
    /// </summary>
    public CliCellStyle(CliCharStyle? charStyle = null)
    {
        CharStyle = charStyle;
    }

    /// <summary>
    /// Returns a new style by applying the specified <paramref name="overrideStyle"/>
    /// on top of this style. Non-null values from <paramref name="overrideStyle"/> override this instance.
    /// </summary>
    protected internal CliCellStyle MergeWith(CliCellStyle? overrideStyle)
    {
        int? width = overrideStyle?.Width ?? this.Width;
        int? minWidth = overrideStyle?.MinWidth ?? this.MinWidth;
        int? maxWidth = overrideStyle?.MaxWidth ?? this.MaxWidth;
        bool overrideHasWidth = overrideStyle?.Width is not null;

        if (!overrideHasWidth && width is int w)
        {
            if (minWidth is int min && w < min)
                width = null;
            if (maxWidth is int max && w > max)
                width = null;
        }

        int? height = overrideStyle?.Height ?? this.Height;
        int? minHeight = overrideStyle?.MinHeight ?? this.MinHeight;
        int? maxHeight = overrideStyle?.MaxHeight ?? this.MaxHeight;
        bool overrideHasHeight = overrideStyle?.Height is not null;

        if (!overrideHasHeight && height is int h)
        {
            if (minHeight is int min && h < min)
                height = null;
            if (maxHeight is int max && h > max)
                height = null;
        }

        return new CliCellStyle
        {
            MinWidth = minWidth,
            MaxWidth = maxWidth,
            Width = width,

            MinHeight = minHeight,
            MaxHeight = maxHeight,
            Height = height,

            HorizontalAlignment = overrideStyle?.HorizontalAlignment ?? this.HorizontalAlignment,
            VerticalAlignment = overrideStyle?.VerticalAlignment ?? this.VerticalAlignment,

            Padding = overrideStyle?.Padding ?? this.Padding,

            Wrapping = overrideStyle?.Wrapping ?? this.Wrapping,
            FormattingMode = overrideStyle?.FormattingMode ?? this.FormattingMode,
            NullDisplayValue = overrideStyle?.NullDisplayValue ?? this.NullDisplayValue,
            Formatter = CliFormatter.Clone(overrideStyle?.Formatter ?? this.Formatter),

            IsHyperlink = overrideStyle?.IsHyperlink ?? this.IsHyperlink,

            CharStyle = new CliCharStyle(
                overrideStyle?.CharStyle?.Foreground ?? this.CharStyle?.Foreground,
                overrideStyle?.CharStyle?.Background ?? this.CharStyle?.Background,
                // Decorations are additive across the cascade: the override's flags OR onto this style's.
                (this.CharStyle?.Decorations ?? CliTextDecoration.None)
                    | (overrideStyle?.CharStyle?.Decorations ?? CliTextDecoration.None)
            )
            {
                // The resolved link target replaces (override wins), like foreground/background.
                HyperlinkTarget = overrideStyle?.CharStyle?.HyperlinkTarget ?? this.CharStyle?.HyperlinkTarget
            }
        };
    }

    /// <summary>
    /// Creates a deep copy of a style, or <c>null</c> when <paramref name="s"/> is <c>null</c>.
    /// </summary>
    public static CliCellStyle? Clone(CliCellStyle? s)
    {
        if (s is null) 
            return null;

        // Deep copy of sizing-related fields; add more fields as your style evolves.
        return new CliCellStyle
        {
            Width = s.Width,
            MinWidth = s.MinWidth,
            MaxWidth = s.MaxWidth,
            Height = s.Height,
            MinHeight = s.MinHeight,
            MaxHeight = s.MaxHeight,

            // copy other visual/style fields here (alignment, wrapping, colors, etc.)
            HorizontalAlignment = s.HorizontalAlignment,
            VerticalAlignment = s.VerticalAlignment,
            Padding = s.Padding,
            Wrapping = s.Wrapping,
            FormattingMode = s.FormattingMode,
            NullDisplayValue = s.NullDisplayValue,
            Formatter = CliFormatter.Clone(s.Formatter),
            IsHyperlink = s.IsHyperlink,
            CharStyle = CliCharStyle.Clone(s.CharStyle)
        };
    }

}
