## CliGrid measurement ownership

TigerCli layout, wrapping, truncation, padding, spans, and rendered width are owned by `CliGrid`.

Code outside the regular `CliGrid` measurement and rendering path must not attempt to predict rendered size.

### Rules

Do not call `CliGrid.Measure()` as an ad-hoc sizing helper outside the normal measurement/rendering pipeline.

Do not pre-calculate rendered size by manually splitting text on line endings.

Do not implement simplified local versions of measurement, line splitting, wrapping, truncation, padding, span handling, or grid sizing.

Do not create component-specific mini layout engines inside controls, dialogs, widgets, or higher-level abstractions.

Controls and widgets should describe their content and constraints using `CliGrid`, `CliCellStyle`, `CliGridColumnDefinition`, `CliGridRowDefinition`, `Width`, `MinWidth`, `MaxWidth`, `SoftMaxWidth`, wrapping, padding, and spans.

Then they must let `CliGrid` measure and render the result through the normal pipeline.

### Why

Manual sizing logic almost always misses part of the real layout contract: markup, Unicode width, padding, wrapping, truncation, column spans, scrollable cells, overlays, style ownership, or parent grid composition.

This creates bugs where plain text output appears correct, but styled rendering is wrong.

### Correct pattern

Build the grid with the intended content and constraints.

Let `CliGrid` measure it.

Let `CliGrid` render it.

Use style-preserving test tools such as `HtmlSink` when validating ownership of background, foreground, or styled spans.

### Incorrect pattern

Do not write code like this:

```csharp
var lines = text.Split('\n');
var width = lines.Max(line => line.Length);
```

Do not write code like this:

```csharp
grid.Measure(new TextSegmentLinesSink());
var width = grid.MeasuredWidth;
```

as a private sizing shortcut inside a control or dialog.

### Correct responsibility split

`CliGrid` owns layout.

Controls and widgets own state and content.

Dialogs own composition.

Higher-level components must not reimplement grid measurement.
