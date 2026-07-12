using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Exceptions;


/// <summary>
/// Exception raised for TigerCli framework failures that should identify the render or execution stage
/// where the failure occurred.
/// </summary>
public class TigerCliException : Exception
{
    /// <summary>The framework stage associated with the failure.</summary>
    public TigerCliRenderStage Stage { get; }

    /// <summary>Creates a TigerCli exception with a message and stage classification.</summary>
    public TigerCliException(string message, TigerCliRenderStage stage)
        : base(message)
    {
        Stage = stage;
    }
}
