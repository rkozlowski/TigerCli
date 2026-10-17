# Releasing TigerCli

TigerCli is released through the Tiger release model (TigerAiCore `docs/release-model.md`): one
release commit, one build of it, a record of the exact bytes, and publication of those bytes. This
page is TigerCli's side of that model: what a release consists of, how the stages run here, and what
to do when one fails. The tooling is in `eng/release/`.

TigerCli uses the permanent development branch workflow (`AGENTS.md`): development and release
preparation happen on `dev`, and a release is made from the commit that promoting `dev` to `main`
produces. Nothing is released or tagged from `dev`.

## What A Release Is

The release of version `<v>` is the **release set** that the **Release TigerCli** workflow
(`.github/workflows/publish-packages.yml`) builds from the release commit. It is published to the
package feeds and attached to the draft GitHub Release `TigerCli <v>` at the annotated tag `v<v>`:

| File | Kind | What it is |
|---|---|---|
| `ItTiger.Core.<v>.nupkg`, `.snupkg` | `NuGetPackage`, `NuGetSymbols` | shared core helpers, and their symbols |
| `ItTiger.TigerCli.<v>.nupkg`, `.snupkg` | `NuGetPackage`, `NuGetSymbols` | the framework; depends on `ItTiger.Core` `<v>` |
| `ItTiger.TigerCli.PngSink.<v>.nupkg`, `.snupkg` | `NuGetPackage`, `NuGetSymbols` | the PNG sink; depends on `ItTiger.TigerCli` `<v>` |
| `LICENSE` | `License` | the licence that applies to this release: the exact bytes the release commit holds for `LICENSE` |
| `release-artifacts.json` | record | the version, the commit, and each file's kind, length and SHA-256 (schema 1) |
| `SHA256SUMS.txt` | record | the same hashes in `sha256sum` format |

- **The set is closed.** A directory, workflow artifact, or draft that holds a file the record does
  not name, or lacks one it does name, is refused at every stage.
- **The tag vouches for the record.** `v<v>` is created only by the release workflow, as
  `github-actions[bot]`, at the release commit. Its message names the SHA-256 of that release's
  `release-artifacts.json`.
- **Every package names its commit.** Each package's `nuspec` names the release commit as its
  repository commit.
- **The notes are checked in.** They come from `.github/release-notes/<v>.md`.

TigerCli releases libraries, not an application: it ships no privacy statement, as the release model
expects for libraries. The sample apps in this repository are not released.

The packages go to two feeds, in this order:

1. GitHub Packages (`https://nuget.pkg.github.com/rkozlowski/index.json`), packages only. Its NuGet
   documentation describes no symbol-package support.
2. nuget.org, with each symbol package after its package. Publication there is irreversible.

nuget.org serves a repository-signed copy, so its file hash differs from the record. That copy is
the recorded package's entries byte for byte, plus `.signature.p7s`, and the tooling identifies it
by that content.

Nothing else is the release. **A local candidate**, built on a developer machine with
`Build-ReleaseArtifacts.ps1 -Rehearsal` or by `dotnet pack`, is evidence for preparation and is never
published. **The workflow artifact** only carries the set between the workflow's jobs and expires
after 30 days.

## The Stages

| | Who | What |
|---|---|---|
| 1 | coder | prepare on `dev`: version, references, notes, documentation, the full gate, the release rehearsal |
| 2 | Architect | review the release pull request and promote `dev` to `main` with a merge commit |
| 3 | Architect | start **Release TigerCli** on `main`; the workflow does the rest |
| 4 | coder | release validation of the draft and the feeds |
| 5 | Architect | review and publish the draft |

### 1. Prepare

"Prepare TigerCli `<v>` release" means:

- **Version.** Set it in `Version.props` (`Version`, and `<v>.0` for `AssemblyVersion` and
  `FileVersion`).
- **Version references.** Update the current-version install examples. The prerequisites gate
  checks them in `README.md`, the three package READMEs, and `docs/reference/png-sink.md`. Also
  update the sample-app version assertions in the tests, the `default` of the workflow's `version`
  input, and any documentation that intentionally names the current version. Keep historical
  version references that describe earlier behavior.
- **Notes.** Write `.github/release-notes/<v>.md`: a `# TigerCli <v>` heading and at least two `##`
  sections of real, user-facing content.
- **Documentation.** Update the documentation the release changes. Regenerate generated files only
  through their owner tools.
- **The verification gate.** Run each command separately, as `AGENTS.md` requires:
  - `dotnet build TigerCli.sln -c Release`: 0 warnings, 0 errors.
  - `dotnet test TigerCli.sln -c Release --no-build`: all passing.
  - `dotnet run --project internal/DocSamples -c Release --no-build -- check`.
  - DocFX and the API-map check, when XML comments or the public API changed.
  - The real-shell checks in [unit testing](unit-testing.md#real-shell-checks), when shell
    completion or console output changed.
  - `pwsh -File eng/release/Test-Release.ps1`, when `eng/release/` or the release workflow changed.
  - `git diff --check`, and the process audit.
- **The release rehearsal.** Run `pwsh -File eng/release/Build-ReleaseArtifacts.ps1 -Version <v>
  -Rehearsal`. It is the release build on a local candidate:
  - it packs the three packages as a continuous-integration build stamped with `HEAD`;
  - it inspects them: identity, version, commit, licence, URLs, README, icon, assemblies and XML
    documentation, dependencies at exactly `<v>`, PDBs, and PngSink's assets and embedded fonts;
  - it restores them from a local feed with a private package cache, and checks that every
    `ItTiger.*` dependency resolved to these exact bytes;
  - it runs a command app, shell completion, and a PngSink render from them;
  - it records the set into `artifacts/release-candidate/<v>`.
- **The prerequisites gate.** Run `pwsh -File eng/release/Assert-ReleaseCommitReady.ps1 -Version
  <v>`, the release workflow's own first gate. Before promotion, `commit/on-main` is BLOCKED and
  `dispatch` is NOT RUN. Everything else must pass.
- **Commit and pull request.** Commit and push the green result to `dev`, and open or update the
  release pull request from `dev` to `main`.

Preparation publishes, tags, and releases nothing.

### 2. Promote

The Architect reviews the release pull request and merges it with **Create a merge commit**. That
merge commit on `main` is the release commit.

- **Never squash or rebase.** Either one loses `dev`'s history. The prerequisites gate refuses the
  result.
- **Push nothing else to `main` before the release starts.**
- **Promote only what `dev` accepted.** The gate checks that the release commit's tree is exactly
  the tree of the accepted `dev` commit it merges. If `main` carried a change `dev` lacked, merge
  `main` into `dev`, accept the result there, and promote again.

The push to `main` also starts **Publish API documentation**, which republishes the API reference
site. It is independent of the release.

### 3. The Release Action

The Architect opens **Actions → Release TigerCli → Run workflow**, selects `main`, and confirms the
version the form is prefilled with. The workflow has three jobs:

1. **Prerequisites** (`Assert-ReleaseCommitReady.ps1`). It uses only git and an anonymous nuget.org
   read, builds nothing, and checks that:
   - the version is the one `Version.props`, the install examples, the workflow default, and the
     notes state;
   - the commit holds `LICENSE`;
   - the workflow was dispatched on `main`;
   - the commit is on `main` and is a history-preserving promotion of an accepted `dev` commit with
     an identical tree;
   - `v<v>` does not exist, and nuget.org has none of the packages at `<v>`.
2. **Build** (`Build-ReleaseArtifacts.ps1`). It packs once from a checkout of exactly the commit,
   inspects the packages and smoke-tests them from a local feed (the same checks as the rehearsal),
   and freezes `LICENSE` from the commit. It writes the record, proves that the build changed no
   committed file, and hands the set to the next job together with the record's SHA-256.
3. **Publish**, in the `release` environment, in two steps:
   - `Publish-Packages.ps1` proves that the set it received is the recorded one, then reads every
     feed before it pushes anything. It pushes each package that is absent, in dependency order:
     GitHub Packages first, then nuget.org with symbols, never with `--skip-duplicate`. A package
     already present must be this release's, or nothing is pushed.
   - `Publish-DraftRelease.ps1` creates the annotated tag and the draft release with the nine
     files.

Both publish scripts refuse to change anything outside this workflow's run for the commit on
`main`.

The run publishes the packages, which cannot be withdrawn from nuget.org. It does not publish the
GitHub Release, which stays a draft.

### 4. Release Validation

The coder runs `pwsh -File eng/release/Get-ReleaseArtifacts.ps1 -Version <v>`. `gh` must be logged
in, because drafts are not public. The script downloads the draft's files into
`artifacts/release/<v>` and proves:

- the release is named `TigerCli <v>`, and every file is GitHub's recorded asset digest;
- the set is closed and is the bytes the record names;
- `v<v>` is the workflow's annotated tag at the record's commit, naming that record;
- `LICENSE` is the commit's blob, and every package names that commit;
- nuget.org serves this release's packages: the same entries, apart from the signature;
- GitHub Packages does too. This check needs a token with `read:packages`
  (`gh auth refresh -s read:packages`) and is otherwise NOT RUN.

It reports READY TO PUBLISH, BLOCKED, or the failure. It does not repeat the product acceptance
that preparation ran, and the workflow already smoke-tested these exact packages before it pushed
them.

### 5. Publication

The Architect reviews the draft (the notes, the tag, and the nine files) and publishes it.

## Recovery

| What happened | What to do |
|---|---|
| **Prerequisites** failed | Nothing was built or published. Fix the cause on `dev`, promote again, and start the release again. The version is unchanged. |
| **Build** failed | Nothing was published. Fix the cause and release from a new release commit. The version is unchanged, because nothing exists yet. |
| **Publish** failed or was BLOCKED | Use **Re-run failed jobs**, which keeps the same built set. The publish step verifies what the earlier attempt published and pushes only what is missing. The tag and draft step accepts a tag and draft only when they are exactly what it would have created. |
| **Publish** is BLOCKED because nuget.org has not indexed a package yet | Wait a few minutes, then **Re-run failed jobs**. |
| A feed already has another build of `<v>` | Nothing more is pushed. The version cannot be released. Release the next patch version. |
| The workflow artifacts expired before a rerun | The set cannot be completed. Release the next patch version. |
| Release validation failed | Do not publish the draft; the Architect may delete it. Packages already on nuget.org can only be unlisted. The fix ships as the next patch version. |

- **A tag is never moved, and a version is released once.**
- **Never use "Re-run all jobs" after packages were published.** It would build a different set.
  The tooling refuses that set: the prerequisites refuse it when the tag or a nuget.org package
  exists, and the publish step refuses it when a feed already holds the earlier build.

## One-time Repository Configuration

These settings belong to the repository owner and are outside the repository's files.

### The release environment

- **Deployment branches: restrict the `release` environment to `main`.** This setting is the
  security boundary. A workflow started on another branch could otherwise run its own version of
  the workflow file with the `release` environment's Trusted Publishing identity.
- **Required reviewers: not needed.** Starting the release is the approval. Remove the reviewer
  requirement only after the branch restriction is in place.

### Merge settings

Disabling squash and rebase merging is recommended, so a promotion cannot be squashed or rebased by
accident. The prerequisites gate refuses such a promotion, but `main` would then need repair.

### nuget.org Trusted Publishing

In the nuget.org account that owns all three packages, a **Trusted Publishing** policy for GitHub
Actions names:

- repository owner `rkozlowski`;
- repository `TigerCli`;
- workflow file `publish-packages.yml`;
- environment `release`.

Keep the workflow file name and the environment name, because the policy names both. Set the
repository Actions variable `NUGET_USER` to that account's nuget.org **profile username**, not an
email address.

The publish job uses `NuGet/login@v1` to exchange GitHub's OIDC token for a short-lived API key. No
nuget.org API key is stored. See the
[Trusted Publishing documentation](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).

### GitHub Packages

The publish job pushes and reads with its own `GITHUB_TOKEN` (`packages: write`). The repository
URL in the package metadata links the packages to this repository. See GitHub's
[NuGet registry documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

## Tooling

| Script | Stage | Role |
|---|---|---|
| `eng/release/TigerCliRelease.psm1` | all | TigerCli's release facts, the record, the package inspection and smoke test, the feed, Git, tag, and release checks |
| `eng/release/Assert-ReleaseCommitReady.ps1` | 1, 3 | the prerequisites gate |
| `eng/release/Build-ReleaseArtifacts.ps1` | 3 (and 1 with `-Rehearsal`) | builds, inspects, smoke-tests, and records the release set |
| `eng/release/Publish-Packages.ps1` | 3 | publishes the recorded packages; `-PlanOnly` reads the feeds and changes nothing |
| `eng/release/Publish-DraftRelease.ps1` | 3 | the tag and the draft; `-PlanOnly` reports and changes nothing |
| `eng/release/Get-ReleaseArtifacts.ps1` | 4 | retrieves and proves a release's set and the feeds' copies |
| `eng/release/Test-Release.ps1` | 1 | the tooling's tests: synthetic repositories and stand-ins for `gh`, `dotnet`, and the feeds; no network |

Every gate reports `PASS`, `BLOCKED`, or `FAIL` and exits `0`, `2`, or `1`. A check that cannot run
where the gate runs is reported `NOT RUN`, together with where it runs instead, and is never counted
as a pass.

The release action is the only hosted work a release needs. The verification gate runs during
preparation and is not repeated on the runner.

## Emergency Local Publishing

Do not publish from a developer machine on the normal path. Publishing locally requires an
explicitly approved emergency and a separate, security-reviewed procedure:

- use a least-privilege, short-lived credential, and keep it out of the repository, shell history,
  and logs;
- publish only an already recorded set, with `Publish-Packages.ps1` logic, never with an ad hoc
  rebuild;
- revoke the credential immediately afterwards.

Never add a publication trigger for ordinary pushes.
