# File Copy Sample

`FileCopy/` is a small menu-driven app showing TigerCli's single-file path prompts and activity UI.
It exposes two named commands:

- `copy-to-folder` prompts for an existing source with `[TigerCliFileOpen(Filter = "*.*")]`,
  then for a destination folder with `[TigerCliFolderSelect]`. It keeps the source file name and
  refuses to silently replace an existing destination file.
- `copy-to-file` uses the same source prompt, then `[TigerCliFileSave]` for a destination path.
  Its save attribute uses `Overwrite = TigerCliFileOverwrite.Prompt` and
  `OverwriteWhenOption = nameof(Force)`.

With no command, the app presents TigerCli's command menu. Missing paths are then collected in the
order described above. Both commands use `RunActivityAsync` and report byte progress while copying.

For scripts, supply the command, paths, and `--non-interactive`:

```text
file-copy copy-to-folder --source C:\Input\report.csv --destination C:\Archive --non-interactive
file-copy copy-to-file --source C:\Input\report.csv --destination C:\Archive\latest.csv --non-interactive
file-copy copy-to-file --source C:\Input\report.csv --destination C:\Archive\latest.csv --force --non-interactive
```

Non-interactive execution never browses or confirms. The source file and destination parent must
already exist. An existing `copy-to-file` destination fails under the prompt policy unless
`--force` is supplied; `--force` permits replacement without a warning. The sample does not create
directories, copy multiple files, or preserve file-system metadata.
