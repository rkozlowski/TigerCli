# Preparing And Publishing A Release

TigerCli publishes `ItTiger.Core` and `ItTiger.TigerCli` as NuGet packages. Both packages use the
shared version and repository metadata in `Version.props`; package-specific descriptions, readmes,
and embedded icons remain in their project files.

For 0.8.1, GitHub Packages is the primary distribution channel. Build, inspect, and smoke-test the
packages locally, then publish those exact `.nupkg` files to GitHub Packages. Do not rebuild between
publishing `ItTiger.Core` and `ItTiger.TigerCli` unless a failed validation requires a new release
candidate.

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

## Publish 0.8.1 To GitHub Packages

GitHub Packages is the preferred manual publishing path for 0.8.1. Its NuGet source is:

```text
https://nuget.pkg.github.com/rkozlowski/index.json
```

For a local publication, authenticate with a GitHub personal access token (classic) that has
`write:packages` permission and belongs to an account allowed to publish to the `rkozlowski`
namespace. GitHub Actions can instead use `GITHUB_TOKEN` with `packages: write`. See GitHub's
[NuGet registry authentication documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

Keep the token outside the repository and supply it through a protected environment variable or
secret store. Do not put it in `NuGet.Config`, command text, shell history, captured terminal output,
or logs. The commands below deliberately reference an environment variable rather than showing a
token value:

```powershell
dotnet nuget push ItTiger.Core/bin/Release/ItTiger.Core.0.8.1.nupkg `
  --source https://nuget.pkg.github.com/rkozlowski/index.json `
  --api-key $env:GITHUB_PACKAGES_TOKEN `
  --skip-duplicate `
  --no-symbols

dotnet nuget push ItTiger.TigerCli/bin/Release/ItTiger.TigerCli.0.8.1.nupkg `
  --source https://nuget.pkg.github.com/rkozlowski/index.json `
  --api-key $env:GITHUB_PACKAGES_TOKEN `
  --skip-duplicate `
  --no-symbols
```

Publish and verify `ItTiger.Core` first because `ItTiger.TigerCli` depends on Core 0.8.1. Publish
TigerCli only after the Core package is visible and restorable. Use the same inspected `.nupkg`
files for both pushes.

GitHub's NuGet registry documentation does not document `.snupkg` symbol-server support. For 0.8.1,
use `--no-symbols`, do not push the `.snupkg` files to the GitHub package endpoint, and retain them
with the reviewed release artifacts. Revisit this instruction only after GitHub documents compatible
NuGet symbol-package support.

## Optional NuGet.org Distribution

NuGet.org is a separate, optional future distribution channel, not the default publishing path for
0.8.1. Do not perform a local NuGet.org API-key push as part of the normal 0.8.1 release unless the
human release owner explicitly approves that additional publication.

If NuGet.org publication is approved later, use the same validated package files and keep the API key
outside the repository:

```powershell
dotnet nuget push ItTiger.Core/bin/Release/ItTiger.Core.0.8.1.nupkg `
  --source https://api.nuget.org/v3/index.json `
  --api-key $env:NUGET_API_KEY `
  --skip-duplicate

dotnet nuget push ItTiger.TigerCli/bin/Release/ItTiger.TigerCli.0.8.1.nupkg `
  --source https://api.nuget.org/v3/index.json `
  --api-key $env:NUGET_API_KEY `
  --skip-duplicate
```

NuGet.org supports `.snupkg` publication and normally publishes an adjacent symbol package when its
matching `.nupkg` is pushed. Confirm the push output. If symbols were not published automatically,
push the two `.snupkg` files explicitly, Core first, using the same source, API-key environment
variable, and `--skip-duplicate`. See NuGet's
[symbol-package documentation](https://learn.microsoft.com/nuget/create-packages/symbol-packages-snupkg).

## Recommended Automation Follow-Up

Add package automation as a separate, reviewed change after the first manual GitHub Packages
publication establishes the desired ownership and visibility. Prefer a `workflow_dispatch` workflow
that:

1. requires an explicit version input and a protected release environment with approval when
   configured;
2. grants only `contents: read` and `packages: write`;
3. restores, builds, tests, packs, inspects, and smoke-tests the requested version;
4. uploads `.nupkg` and `.snupkg` files as reviewable workflow artifacts;
5. publishes only from a separately approved job;
6. publishes `ItTiger.Core` before `ItTiger.TigerCli`;
7. publishes to GitHub Packages first with `GITHUB_TOKEN`; and
8. treats any later NuGet.org publication as a separate reviewed step using a protected
   repository/environment secret.

Do not trigger package publication on ordinary pushes.
