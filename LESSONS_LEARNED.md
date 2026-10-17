# Lessons Learned

Current, non-obvious, project-specific knowledge that prevents repeating costly mistakes.

## Regenerating doc examples off the canonical platform rewrites every PNG

`dotnet run --project internal/DocSamples` regenerates **all** of `docs/examples`, PNGs included.
PNG bytes are canonical only on Windows 11 (see `docs/design/doc-artifacts.md`); on any other
machine the run rewrites every committed PNG with different bytes even though nothing visual
changed. When a change only affects text artifacts (for example help output captured in
`docs/examples/roi-cities.html`), regenerate, keep the intended text files, `git restore` the
rest of `docs/examples`, then confirm with `dotnet run --project internal/DocSamples -- check`
(it compares text exactly and checks only PNG existence off-platform).

A change to any sample app's help output — such as a new framework option listed in help — drifts
those captured artifacts and fails `DocExamplesDriftTests`, so expect this whenever help changes.
