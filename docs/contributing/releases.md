# Preparing And Publishing A Release

TigerCli publishes `ItTiger.Core`, `ItTiger.TigerCli`, and `ItTiger.TigerCli.PngSink` as NuGet
packages, in that dependency order. All three use the shared version and repository metadata in
`Version.props`; package-specific descriptions, readmes, and embedded icons remain in their project
files.

For 0.9.1, the manual **Publish NuGet packages** GitHub Actions workflow is the primary publishing
path. It builds, validates, and packs once, pauses at the protected `release` environment, then
publishes the exact validated package files to GitHub Packages first and NuGet.org second. It does
not run on ordinary pushes.

## Prepare And Validate

Set the release version in `Version.props`, update current-version installation examples, then run
each release validation command separately from the repository root:

```powershell
dotnet build TigerCli.sln -c Release
dotnet test TigerCli.sln -c Release --no-build
dotnet pack ItTiger.Core/ItTiger.Core.csproj -c Release --no-build
dotnet pack ItTiger.TigerCli/ItTiger.TigerCli.csproj -c Release --no-build
dotnet pack ItTiger.TigerCli.PngSink/ItTiger.TigerCli.PngSink.csproj -c Release --no-build
```

The release gate is:

- Release build: 0 warnings and 0 errors, with no active analyzer messages for touched projects.
- Release tests: all passing.
- Package inspection: all three `.nupkg` files have the expected version, metadata, README, icon,
  assemblies, and dependencies. `ItTiger.TigerCli` must depend on the matching `ItTiger.Core`
  version, and `ItTiger.TigerCli.PngSink` must depend on the matching `ItTiger.TigerCli` version.
- Local-feed smoke tests: a minimal TigerCli command must restore and run, and PngSink must restore
  and render a PNG, using only the release candidates for every `ItTiger.*` dependency.
- DocSamples generation/check and `DocExamplesDriftTests`: required when documentation artifacts
  are affected.
- API map regeneration/check: required when public API changes.
- DocFX: required when XML comments, public API, DocFX configuration, or API metadata are affected;
  run it only in the normal/non-sandbox environment, as required by `AGENTS.md`.
- `git diff --check`: passing.
- Process audit: no validation-owned `dotnet`, MSBuild/build-host, DocFX, test-host, or verifier
  processes left behind.

Pack produces an `.nupkg` and `.snupkg` for each package. Keep all six files together as the
reviewed release candidate even when a destination does not consume the symbol packages.

## Inspect The Packages

Treat packages as immutable after validation. Inspect all three `.nupkg` archives before smoke testing or
publishing and confirm:

- package ID and version are correct;
- repository URL, license, description, and dependency metadata are correct;
- `README.md` and the configured package icon are present at package root;
- the icon metadata names that physical icon file;
- the expected `net10.0` assembly and XML documentation are present; and
- `ItTiger.TigerCli` depends on `ItTiger.Core` at the release version; and
- `ItTiger.TigerCli.PngSink` depends on `ItTiger.TigerCli` at the release version; and
- the PngSink package contains its expected PNG assets, bundled fonts, and font license/source files.

If inspection fails, fix the source metadata, rebuild and repack all three release candidates, then repeat
the full gate. Do not patch a package archive by hand.

## Smoke-Test The Local Packages

Before publishing, restore the freshly packed 0.9.1 packages from a temporary local feed. The
temporary NuGet configuration maps `ItTiger.*` exclusively to that feed while allowing third-party
dependencies to come from NuGet.org. This proves that all three Tiger packages, including TigerCli's
dependency on Core 0.9.1 and PngSink's dependency on TigerCli 0.9.1, resolve from the release
candidate rather than a remote source.

Run from the repository root:

```powershell
$repoRoot = (Get-Location).Path
$smokeRoot = Join-Path $env:TEMP ("TigerCli-0.9.1-smoke-" + [guid]::NewGuid().ToString("N"))
$localFeed = New-Item -ItemType Directory -Path (Join-Path $smokeRoot "packages")

Copy-Item "$repoRoot/ItTiger.Core/bin/Release/ItTiger.Core.0.9.1.nupkg" $localFeed
Copy-Item "$repoRoot/ItTiger.TigerCli/bin/Release/ItTiger.TigerCli.0.9.1.nupkg" $localFeed
Copy-Item "$repoRoot/ItTiger.TigerCli.PngSink/bin/Release/ItTiger.TigerCli.PngSink.0.9.1.nupkg" $localFeed

$nugetConfig = Join-Path $smokeRoot "NuGet.Config"
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-release" value="$($localFeed.FullName)" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-release">
      <package pattern="ItTiger.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfig -Encoding utf8

Push-Location $smokeRoot

dotnet new console -n CoreSmoke -f net10.0
dotnet add CoreSmoke/CoreSmoke.csproj package ItTiger.Core --version 0.9.1 --no-restore
dotnet restore CoreSmoke/CoreSmoke.csproj --configfile $nugetConfig --force --no-http-cache
Select-String CoreSmoke/obj/project.assets.json -Pattern '"ItTiger.Core/0.9.1"'

dotnet new console -n TigerCliSmoke -f net10.0
dotnet add TigerCliSmoke/TigerCliSmoke.csproj package ItTiger.TigerCli --version 0.9.1 --no-restore
@'
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

return await TigerCliApp.CreateBuilder()
    .AddDefaultCommand(static () =>
    {
        TigerConsole.MarkupLine("[Success]TigerCli package smoke test[/]");
        return TigerCliExitKind.Success;
    })
    .Build()
    .RunAsync(args);
'@ | Set-Content TigerCliSmoke/Program.cs -Encoding utf8
dotnet restore TigerCliSmoke/TigerCliSmoke.csproj --configfile $nugetConfig --force --no-http-cache
Select-String TigerCliSmoke/obj/project.assets.json `
  -Pattern '"ItTiger.TigerCli/0.9.1"', '"ItTiger.Core/0.9.1"'
dotnet run --project TigerCliSmoke/TigerCliSmoke.csproj -c Release --no-restore -- --non-interactive

dotnet new console -n PngSinkSmoke -f net10.0
dotnet add PngSinkSmoke/PngSinkSmoke.csproj package ItTiger.TigerCli.PngSink --version 0.9.1 --no-restore
@'
using ItTiger.TigerCli.PngSink;
using ItTiger.TigerCli.Rendering;

var grid = new CliGrid(1, 1);
grid.Set(0, 0, "TigerCli PNG smoke test");
PngRenderer.RenderGridToFile(
    grid,
    "smoke.png",
    new PngSinkOptions { Columns = 30, Rows = 2 });
'@ | Set-Content PngSinkSmoke/Program.cs -Encoding utf8
dotnet restore PngSinkSmoke/PngSinkSmoke.csproj --configfile $nugetConfig --force --no-http-cache
Select-String PngSinkSmoke/obj/project.assets.json `
  -Pattern '"ItTiger.TigerCli.PngSink/0.9.1"', '"ItTiger.TigerCli/0.9.1"', '"ItTiger.Core/0.9.1"'
dotnet run --project PngSinkSmoke/PngSinkSmoke.csproj -c Release --no-restore
if (-not (Test-Path -LiteralPath smoke.png -PathType Leaf)) {
    throw "PngSink smoke test did not create smoke.png."
}

Pop-Location
```

All restore and asset checks, the TigerCli command run, and the PngSink render must succeed. The
temporary directory may be removed after review.

## Configure The Publishing Workflow

The workflow is `.github/workflows/publish-packages.yml`. It has only a `workflow_dispatch` trigger
and one input:

- `version`: required package version, default `0.9.1`. It must match `Version.props`.

Both workflow jobs use GitHub-hosted Windows runners. Release validation must run on Windows because
the test suite includes Windows path semantics, and Windows is also the canonical platform for
DocSamples PNG drift comparison. Do not switch release validation to Linux or skip platform-sensitive
tests to make the gate pass.

Create a GitHub environment named `release`. Configure required reviewers and deployment branch/tag
restrictions appropriate for the repository. The workflow's validation job does not publish. After
it uploads the inspected package artifact, the publish job waits for the `release` environment's
approval.

Set the repository Actions variable `NUGET_USER` to the nuget.org **profile username** used for
Trusted Publishing, not an email address. This is not an API key or secret.

### NuGet.org Trusted Publishing Policy

In the nuget.org account or organization that owns all three packages, open **Trusted Publishing** and
add a GitHub Actions policy with:

- Repository owner: `rkozlowski`
- Repository: `TigerCli`
- Workflow file: `publish-packages.yml` (filename only, without `.github/workflows/`)
- Environment: `release`

The policy owner must own `ItTiger.Core`, `ItTiger.TigerCli`, and `ItTiger.TigerCli.PngSink`, and its
profile username must match the `NUGET_USER` repository variable. NuGet.org may initially make a
policy temporarily active while
it learns immutable GitHub repository IDs; complete the first approved publication within the
window shown by nuget.org.

The workflow grants the publish job `id-token: write` and uses the official `NuGet/login@v1` action
to exchange GitHub's OIDC token for a short-lived NuGet API key. No NuGet.org API-key secret is
created or stored. See the official
[Trusted Publishing documentation](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).

### GitHub Packages Access

The publish job grants `packages: write` and uses its built-in `GITHUB_TOKEN`; no PAT is stored. The
target source is:

```text
https://nuget.pkg.github.com/rkozlowski/index.json
```

Ensure repository Actions settings allow workflows read/write package access. The repository URL in
the package metadata connects all three packages to this repository. The first GitHub Packages
publication is private by default; review package visibility and inherited repository access after
publishing. See GitHub's
[NuGet registry documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

## Run And Publish

From the repository's **Actions** tab, select **Publish NuGet packages**, choose **Run workflow**,
confirm the branch containing the reviewed release metadata, and enter the version. The workflow:

1. verifies the input against `Version.props`;
2. builds and tests Release;
3. checks DocSamples, `DocExamplesDriftTests`, DocFX, and the generated API map;
4. packs Core, TigerCli, and PngSink once;
5. inspects versions, READMEs, icons, symbols, and matching package dependencies;
6. restores and runs the TigerCli and PngSink local-feed smoke tests;
7. uploads all six files as the immutable reviewed workflow artifact;
8. waits for `release` environment approval;
9. obtains a short-lived NuGet.org credential through OIDC;
10. publishes `ItTiger.Core`, then `ItTiger.TigerCli`, then `ItTiger.TigerCli.PngSink`, to GitHub
   Packages using `GITHUB_TOKEN`, `--skip-duplicate`, and `--no-symbols`; and
11. publishes Core, TigerCli, and PngSink in the same order, with each matching `.snupkg`, to
    NuGet.org using the short-lived key and `--skip-duplicate`.

GitHub Packages receives only `.nupkg` files because its NuGet documentation does not document
compatible `.snupkg` symbol-server support. NuGet.org receives the supported `.snupkg` files
explicitly after each matching `.nupkg`.

If any publish step fails after an earlier destination succeeded, inspect the registries before
rerunning. `--skip-duplicate` makes an approved retry safe for package versions already present, but
it does not replace verification.

## Emergency Local Publishing

Do not use local tokens or long-lived NuGet.org API keys for the normal release path. If GitHub
Actions or Trusted Publishing is unavailable during an explicitly approved emergency, treat local
publication as a separate security-reviewed procedure: use a least-privilege, short-lived credential,
keep it out of the repository, shell history, and logs, publish the already validated artifacts in
Core-then-TigerCli-then-PngSink order, and revoke it immediately. Do not silently fall back from the
workflow.

Never add a package publication trigger for ordinary pushes.
