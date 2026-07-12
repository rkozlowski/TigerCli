using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tui.Abstractions
{
    /// <summary>
    /// Base class for renderable modal dialogs that expose a result and optional payload.
    /// </summary>
    public abstract class DialogBase : CliRenderableComponent, ICliDialog
    {
        /// <inheritdoc />
        public abstract DialogResultKind Result { get; }

        /// <inheritdoc />
        public abstract object? Payload { get; }

        /// <inheritdoc />
        public abstract bool HandleKey(KeyEvent key);        
    }
}
