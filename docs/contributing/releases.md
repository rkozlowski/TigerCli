# Preparing And Publishing A Release

TigerCli publishes `ItTiger.Core` and `ItTiger.TigerCli` as NuGet packages. Both packages use the
shared version and repository metadata in `Version.props`; package-specific descriptions, readmes,
and embedded icons remain in their project files.

## Prepare And Validate

Set the release version in `Version.props`, update current-version installation examples, then run
the release validation commands from the repository root:

```powershell
dotnet build TigerCli.sln -c Release
dotnet test TigerCli.sln -c Release --no-build
dotnet pack ItTiger.Core/ItTiger.Core.csproj -c Release --no-build
dotnet pack ItTiger.TigerCli/ItTiger.TigerCli.csproj -c Release --no-build
```

Inspect both `.nupkg` archives before publishing. Confirm the package version, repository and license
metadata, root `README.md`, root package icon, framework assemblies, and dependency versions. Also run
the documentation and repository checks described in `AGENTS.md`.

## NuGet.org

Keep the NuGet.org API key outside the repository. After human review, commit, tag, and push, publish
the already-validated package files explicitly:

```powershell
dotnet nuget push ItTiger.Core/bin/Release/ItTiger.Core.<version>.nupkg `
  --source https://api.nuget.org/v3/index.json `
  --api-key $env:NUGET_API_KEY
dotnet nuget push ItTiger.TigerCli/bin/Release/ItTiger.TigerCli.<version>.nupkg `
  --source https://api.nuget.org/v3/index.json `
  --api-key $env:NUGET_API_KEY
```

## GitHub Packages

GitHub Packages can receive the same validated `.nupkg` files; no package rebuild or alternate
metadata is required. The repository URL in `Version.props` associates the packages with this
repository. Use a personal access token with `write:packages` for a local push, or the workflow
`GITHUB_TOKEN` with `packages: write` in GitHub Actions. Never store either token in the repository.

```powershell
dotnet nuget push ItTiger.Core/bin/Release/ItTiger.Core.<version>.nupkg `
  --source https://nuget.pkg.github.com/rkozlowski/index.json `
  --api-key $env:GITHUB_TOKEN
dotnet nuget push ItTiger.TigerCli/bin/Release/ItTiger.TigerCli.<version>.nupkg `
  --source https://nuget.pkg.github.com/rkozlowski/index.json `
  --api-key $env:GITHUB_TOKEN
```

Publish `ItTiger.Core` first because `ItTiger.TigerCli` depends on the matching version. GitHub
Packages is a separate destination from NuGet.org, so publishing to one does not publish to the
other.

## Recommended Automation Follow-Up

Add package automation as a separate, reviewed change after the first manual GitHub Packages
publication establishes the desired ownership and visibility. Prefer a `workflow_dispatch` workflow
that:

1. requires an explicit version input and a protected release environment;
2. grants only `contents: read` and `packages: write`;
3. restores, builds, tests, packs, and validates the requested version;
4. uploads `.nupkg` and `.snupkg` files as reviewable workflow artifacts;
5. publishes only from a separately approved job;
6. pushes `ItTiger.Core` before `ItTiger.TigerCli`; and
7. uses `GITHUB_TOKEN` for GitHub Packages and a repository/environment secret for NuGet.org.

Do not trigger package publication on ordinary pushes.
