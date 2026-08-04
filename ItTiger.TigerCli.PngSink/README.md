# ItTiger.TigerCli.PngSink

`ItTiger.TigerCli.PngSink` renders TigerCli output to deterministic PNG images. It is an optional
companion to [`ItTiger.TigerCli`](https://www.nuget.org/packages/ItTiger.TigerCli/) for generated
documentation artifacts, examples, visual review, and project documentation.

The package implements TigerCli's rendering-sink model with SkiaSharp and bundled fonts, so PNG
output uses the same resolved text segments, terminal palette, and structured rendering pipeline as
the console and HTML sinks.

## Installation

```bash
dotnet add package ItTiger.TigerCli.PngSink --version 0.9.3
```

`ItTiger.TigerCli` is installed transitively.

## Basic usage

Create or obtain a TigerCli grid, then render it to a file with explicit terminal dimensions:

```csharp
using ItTiger.TigerCli.PngSink;
using ItTiger.TigerCli.Rendering;

var grid = new CliGrid(1, 1);
grid.Set(0, 0, "Hello from TigerCli");

PngRenderer.RenderGridToFile(
    grid,
    "tigercli-output.png",
    new PngSinkOptions
    {
        Columns = 40,
        Rows = 5,
        Chrome = PngWindowChrome.FrameAndTitle,
        Title = "TigerCli example"
    });
```

`PngRenderer` can also write to a stream or return PNG bytes. For lower-level control, use
`PngSink` directly as an `ICliRenderSink` and call `Save` or `ToBytes` after rendering.

## Links

- [Project page](https://www.ittiger.net/projects/tigercli/)
- [PNG sink guide](https://github.com/rkozlowski/TigerCli/blob/main/docs/reference/png-sink.md)
- [Generated examples](https://github.com/rkozlowski/TigerCli/tree/main/docs/examples)
- [API reference](https://rkozlowski.github.io/TigerCli/index.html)
- [GitHub repository](https://github.com/rkozlowski/TigerCli)
- [MIT license](https://github.com/rkozlowski/TigerCli/blob/main/LICENSE)

An open-source project by [IT Tiger](https://www.ittiger.net/).
