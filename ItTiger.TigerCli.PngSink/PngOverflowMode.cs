namespace ItTiger.TigerCli.PngSink;

/// <summary>
/// Controls how <see cref="PngSink"/> handles text written beyond the configured grid dimensions.
/// </summary>
public enum PngOverflowMode
{
    /// <summary>Throw an exception when output would exceed the configured rows or columns.</summary>
    Throw,

    /// <summary>Ignore cells outside the configured rows or columns.</summary>
    Clip
}
