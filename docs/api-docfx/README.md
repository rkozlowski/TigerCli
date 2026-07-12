# Local API Reference Generation (DocFX)

This folder holds the DocFX setup that generates a local, browsable API
reference from the TigerCli public API and its XML documentation comments.

```
C# source + XML comments -> DocFX -> local generated API reference
```

## Scope

Local generation only. This setup does not publish anywhere and does not manage
the conceptual Markdown docs. The generated API reference and
[`docs/reference/api-map.md`](../reference/api-map.md) replace the retired
manual contracts reference for public API orientation.

Assemblies included:

- `ItTiger.TigerCli`
- `ItTiger.Core`
- `ItTiger.TigerCli.PngSink`

## Prerequisites

DocFX is pinned as a dotnet local tool (`.config/dotnet-tools.json`). On a
clean checkout, restore it once from the repository root:

```powershell
dotnet tool restore
```

## Generate

From the repository root:

```powershell
dotnet docfx docs/api-docfx/docfx.json
```

This extracts API metadata into `docs/api-docfx/api/` (generated YAML) and
builds the static site into `docs/api-docfx/_site/`. Both are generated and
git-ignored — never edit or commit them.

To generate and preview in a browser in one step:

```powershell
dotnet docfx docs/api-docfx/docfx.json --serve
```

Then open <http://localhost:8080>.

## Compact API map

The DocFX metadata under `api/` is also the source for the committed, compact
type index at [`docs/reference/api-map.md`](../reference/api-map.md) (namespaces →
public types → one-line summary → source path → local API page). It is generated,
never hand-maintained. After running DocFX above, regenerate it with:

```powershell
dotnet run --project internal/DocSamples -- api-map
```

`dotnet run --project internal/DocSamples -- api-map check` reports drift without
writing.

## Notes

- The library projects emit XML documentation files
  (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`) and compile with
  CS1591 (missing XML comment) enabled.
- DocFX warnings about invalid cross-references (`InvalidCref`) point at XML
  comments referencing types/members that don't resolve — fix them in the
  source `///` comments, not here.
