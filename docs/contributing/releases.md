# Preparing And Publishing A Release

TigerCli publishes `ItTiger.Core` and `ItTiger.TigerCli` as NuGet packages. Both packages use the
shared version and repository metadata in `Version.props`; package-specific descriptions, readmes,
and embedded icons remain in their project files.

For 0.8.1, the manual **Publish NuGet packages** GitHub Actions workflow is the primary publishing
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
```

The release gate is:

- Release build: 0 warnings and 0 errors, with no active analyzer messages for touched projects.
- Release tests: all passing.
- Package inspection: both `.nupkg` files have the expected version, metadata, README, icon,
  assemblies, and dependencies. In particular, `ItTiger.TigerCli` must depend on the matching
  `ItTiger.Core` version.
- DocSamples generation/check and `DocExamplesDriftTests`: required when documentation artifacts
  are affected.
- API map regeneration/check: required when public API changes.
- DocFX: required when XML comments, public API, DocFX configuration, or API metadata are affected;
  run it only in the normal/non-sandbox environment, as required by `AGENTS.md`.
- `git diff --check`: passing.
- Process audit: no validation-owned `dotnet`, MSBuild/build-host, DocFX, test-host, or verifier
  processes left behind.

Pack produces an `.nupkg` and `.snupkg` for each package. Keep all four files together as the
reviewed release candidate even when a destination does not consume the symbol packages.

## Inspect The Packages

Treat packages as immutable after validation. Inspect both `.nupkg` archives before smoke testing or
publishing and confirm:

- package ID and version are correct;
- repository URL, license, description, and dependency metadata are correct;
- `README.md` and the configured package icon are present at package root;
- the icon metadata names that physical icon file;
- the expected `net10.0` assembly and XML documentation are present; and
- `ItTiger.TigerCli` depends on `ItTiger.Core` at the release version.

If inspection fails, fix the source metadata, rebuild and repack both release candidates, then repeat
the full gate. Do not patch a package archive by hand.

## Smoke-Test The Local Packages

Before publishing, restore the freshly packed 0.8.1 packages from a temporary local feed. The
temporary NuGet configuration maps `ItTiger.*` exclusively to that feed while allowing third-party
dependencies to come from NuGet.org. This proves that both Tiger packages, including TigerCli's
dependency on Core 0.8.1, resolve from the release candidate rather than a remote source.

Run from the repository root:

```powershell
$repoRoot = (Get-Location).Path
$smokeRoot = Join-Path $env:TEMP ("TigerCli-0.8.1-smoke-" + [guid]::NewGuid().ToString("N"))
$localFeed = New-Item -ItemType Directory -Path (Join-Path $smokeRoot "packages")

Copy-Item "$repoRoot/ItTiger.Core/bin/Release/ItTiger.Core.0.8.1.nupkg" $localFeed
Copy-Item "$repoRoot/ItTiger.TigerCli/bin/Release/ItTiger.TigerCli.0.8.1.nupkg" $localFeed

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
dotnet add CoreSmoke/CoreSmoke.csproj package ItTiger.Core --version 0.8.1 --no-restore
dotnet restore CoreSmoke/CoreSmoke.csproj --configfile $nugetConfig --force --no-http-cache
Select-String CoreSmoke/obj/project.assets.json -Pattern '"ItTiger.Core/0.8.1"'

dotnet new console -n TigerCliSmoke -f net10.0
dotnet add TigerCliSmoke/TigerCliSmoke.csproj package ItTiger.TigerCli --version 0.8.1 --no-restore
'using ItTiger.TigerCli.Terminal; Console.WriteLine(typeof(TigerConsole).FullName);' |
  Set-Content TigerCliSmoke/Program.cs -Encoding utf8
dotnet restore TigerCliSmoke/TigerCliSmoke.csproj --configfile $nugetConfig --force --no-http-cache
Select-String TigerCliSmoke/obj/project.assets.json `
  -Pattern '"ItTiger.TigerCli/0.8.1"', '"ItTiger.Core/0.8.1"'
dotnet build TigerCliSmoke/TigerCliSmoke.csproj -c Release --no-restore

Pop-Location
```

Both restore commands, both asset checks, and the TigerCli smoke build must succeed. The temporary
directory may be removed after review.

## Configure The Publishing Workflow

The workflow is `.github/workflows/publish-packages.yml`. It has only a `workflow_dispatch` trigger
and one input:

- `version`: required package version, default `0.8.1`. It must match `Version.props`.

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

In the nuget.org account or organization that owns both packages, open **Trusted Publishing** and
add a GitHub Actions policy with:

- Repository owner: `rkozlowski`
- Repository: `TigerCli`
- Workflow file: `publish-packages.yml` (filename only, without `.github/workflows/`)
- Environment: `release`

The policy owner must own `ItTiger.Core` and `ItTiger.TigerCli`, and its profile username must match
the `NUGET_USER` repository variable. NuGet.org may initially make a policy temporarily active while
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
the package metadata connects both packages to this repository. The first GitHub Packages
publication is private by default; review package visibility and inherited repository access after
publishing. See GitHub's
[NuGet registry documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

## Run And Publish

From the repository's **Actions** tab, select **Publish NuGet packages**, choose **Run workflow**,
confirm the branch containing the reviewed release metadata, and enter the version. The workflow:

1. verifies the input against `Version.props`;
2. builds and tests Release;
3. checks DocSamples, `DocExamplesDriftTests`, DocFX, and the generated API map;
4. packs Core and TigerCli once;
5. inspects versions, READMEs, icons, symbols, and TigerCli's matching Core dependency;
6. uploads all four files as the immutable reviewed workflow artifact;
7. waits for `release` environment approval;
8. obtains a short-lived NuGet.org credential through OIDC;
9. publishes `ItTiger.Core`, then `ItTiger.TigerCli`, to GitHub Packages using `GITHUB_TOKEN`,
   `--skip-duplicate`, and `--no-symbols`; and
10. publishes Core and its `.snupkg`, then TigerCli and its `.snupkg`, to NuGet.org using the
    short-lived key and `--skip-duplicate`.

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
Core-then-TigerCli order, and revoke it immediately. Do not silently fall back from the workflow.

Never add a package publication trigger for ordinary pushes.
