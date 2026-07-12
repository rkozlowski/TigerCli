# Documentation Artifacts

TigerCli's public screen renders are generated from real TigerCli rendering. Do not create manual
screenshots or hand-edit generated HTML, PNG, WebP, sidecar, or index files for public documentation.
This keeps examples tied to current layout, styling, wrapping, and interaction behavior.

## Ownership And Formats

`internal/DocSamples` owns documentation examples and rendered artifacts under `docs/examples/`.
It uses the production rendering pipeline and the appropriate output sink:

- `HtmlSink` produces inspectable, diffable HTML.
- `PngSink` produces framed PNG terminal renders for embedding in Markdown.
- Deterministic PNG frames are assembled into WebP animations where motion is the subject, such as
  spinners, progress bars, and the Folder Copy activity.

HTML and PNG outputs for a sample come from the same measured TigerCli content. WebP frames use the
same rendering and framing model. Recorded terminal captures and manually composed screenshots are
not part of the public artifact workflow.

## One Documentation Terminal

Public screen renders use the convention defined by `internal/DocSamples/DocTerminal.cs`:

- 120 columns,
- visible terminal frame and title bar,
- height derived from content for static renders,
- one visual model across HTML, PNG, and WebP.

Animation frames keep a fixed canvas for the run. Content that exceeds the documentation terminal
fails generation instead of being silently clipped. A sample whose subject is width-dependent
layout may explicitly emulate a narrower content width, while the public terminal convention remains
the surrounding visual model.

## Generation Modes

Run DocSamples modes from the repository root:

```text
dotnet run --project internal/DocSamples
dotnet run --project internal/DocSamples -- check
dotnet run --project internal/DocSamples -- spinners
dotnet run --project internal/DocSamples -- progress-bars
dotnet run --project internal/DocSamples -- folder-copy
dotnet run --project internal/DocSamples -- themes [name]
```

- The default mode regenerates the committed HTML, CSS, PNG, and PNG sidecar set, including the
  built-in theme pages.
- `check` regenerates that set in memory and reports missing or drifting artifacts.
- `spinners`, `progress-bars`, and `folder-copy` generate WebP animations using `img2webp`.
- `themes` renders all built-in themes, or one named theme, to the current console; the committed
  theme HTML pages are owned by the default mode.

Generated artifacts must be regenerated with these modes, not edited by hand.

## Drift Checks

Artifacts are drift-checked where practical:

- HTML, CSS, and PNG sidecars are compared after newline normalization; volatile environment lines
  in sidecars are masked.
- PNG existence is checked everywhere, and PNG bytes are compared on the canonical Windows 11
  generation platform because font rasterization varies by operating system.
- WebP bytes are not drift-checked when encoder-version variance makes byte comparison unsuitable.
  Where possible, a representative static PNG and sidecar from the same rendered subject should be
  part of the checked default artifact set; Folder Copy follows this pattern.

DocSamples checks run in a child process so process-global registrations from other tests cannot
change the generated truth set.

## Publishing

Git hosting sites do not render linked HTML artifacts inline. Public Markdown therefore embeds the
PNG companion and may link to the HTML page for inspection. WebP is reserved for behavior where
motion materially improves the explanation.

See [HTML sink](../reference/html-sink.md), [PNG sink](../reference/png-sink.md), and the
[generated examples](../examples/README.md).
