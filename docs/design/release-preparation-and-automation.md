# Release Preparation Contract And Automation Target

This note defines the repository contract behind a short release-preparation prompt and the desired
future publishing workflow. The preparation contract is guidance for agents now. The automation
section is a target design only: it does not describe the current workflow as already implemented.
For the current commands and publishing procedure, see
[Preparing and publishing a release](../contributing/releases.md).

## Short-Prompt Release Preparation Contract

The intended future prompt is:

```text
Prepare TigerCli <version> release. Do not commit.
```

For example, preparing the next release should require only `Prepare TigerCli 0.9.5 release. Do not
commit.` rather than a release-specific checklist in the prompt.

An agent receiving that prompt must treat it as authorization to prepare and validate the working
tree for the named release, but not to publish it. Before editing, the agent must inspect the current
repository conventions rather than assume that the previous release checklist is unchanged. At a
minimum, inspect `Version.props`, the current release guide and workflow, the packable project
metadata and package READMEs, current-version installation examples, sample version assertions,
generated-documentation instructions, and recent release-shaped changes when useful.

Preparation includes all version-dependent changes required by the conventions found during that
inspection. This normally means:

- bumping the shared package, assembly, and file versions in `Version.props`;
- updating the root and package READMEs, current install commands, current release documentation,
  publishing-workflow input defaults, sample version assertions, and other documentation that
  intentionally describes the current version;
- preserving historical version references that are examples of past behavior rather than current
  release metadata; and
- regenerating and checking generated documentation or artifacts when the kind of change requires
  it. Generated files such as `docs/reference/api-map.md` must be produced by their owner tool, not
  edited by hand.

The agent must then run the complete release gate documented in the current release guide. Commands
that build, test, generate documentation, run DocFX, verify generated content, pack, or inspect must
follow the repository's process-safety rules and be run separately where required. The release is not
prepared until the agent has:

- completed the required Release build, tests, documentation drift checks, API-map check, DocFX run,
  diff check, and process audit applicable to the change;
- packed every release package (`ItTiger.Core`, `ItTiger.TigerCli`, and
  `ItTiger.TigerCli.PngSink`), including their symbol packages;
- inspected package IDs, versions, repository and license metadata, descriptions, dependency
  versions, READMEs, icons, assemblies, XML documentation, symbols, and package-specific assets;
- confirmed that package dependencies use the requested release version; and
- run the local-feed restore/run/render smoke tests when appropriate, with every `ItTiger.*`
  dependency resolved from the freshly packed release candidates.

If a check fails, the agent fixes the source, regenerates or repacks as necessary, and repeats the
affected gate; package archives must never be patched by hand. If the gate cannot be completed, the
agent leaves the working tree in a reviewable state and reports the incomplete validation precisely.

The phrase `Do not commit` is an explicit release boundary. At handoff, all preparation changes stay
uncommitted for human review. The agent must publish no package, create or push no tag, create no
GitHub release, and make no commit or push. Its final report should identify the changed files,
release-gate results, package inspection and smoke-test results, and any risk or uncertainty.

## Current Human Handoff

After preparation and review, the current process still requires the human to commit the prepared
changes to `dev`, promote `dev` to `main` with a merge that preserves `dev`'s commits, start
the publish workflow from `main`, approve its protected publishing stage, wait for publication, create the Git
tag, and create the GitHub release. The existing workflow and release guide remain authoritative
until the target below is deliberately implemented and reviewed.

## Target Post-Preparation Automation

The desired human interaction after a prepared release is only:

1. Review and commit the prepared changes on `dev`, then promote `dev` to `main` with a merge that
   preserves `dev`'s commits.
2. Manually start one GitHub Actions workflow and supply the release version.

Starting that workflow is the publication approval. There must be no second manual approval or
protected-stage reviewer prompt. The workflow must remain `workflow_dispatch`-only; it must never
publish from an ordinary push, pull request, schedule, or tag event.

### Target Flow

One workflow run should perform the following ordered flow:

1. Require `refs/heads/main`, resolve the dispatch to its immutable commit, check out exactly that
   commit, and retain the commit SHA as the provenance for packages, tag, and release.
2. Validate the requested version format and require the shared version in `Version.props` to match
   it exactly.
3. Refuse to continue if `v<version>` already exists, or if that version of any release package
   already exists on NuGet.org. Any additional package destination retained by the publishing
   workflow should receive an equivalent preflight check.
4. Run the complete release validation gate on the intended Windows runner, including generated
   documentation, DocSamples, DocFX, API-map drift, tests, and all other checks defined by the current
   release guide. Any drift or validation failure stops the run before publication.
5. Pack all release `.nupkg` and `.snupkg` files in that run, inspect their contents and metadata,
   and run the required local-feed smoke tests against those packages.
6. Upload the inspected packages as workflow artifacts. Record the requested version, source commit,
   expected filenames, and hashes so later jobs can verify provenance and integrity.
7. In the publishing job, download only the artifacts produced by that same workflow run, verify the
   recorded hashes and package set, and publish those files in dependency order to the configured
   package destinations. The publishing job must not rebuild, repack, accept packages from another
   run, or silently skip a pre-existing version.
8. After every required package publish succeeds, create and push the annotated tag `v<version>` at
   the exact validated commit.
9. Only after tag creation succeeds, create the GitHub release for that tag. The release may attach
   the same-run `.nupkg` and `.snupkg` artifacts.

The workflow should use least-privilege job permissions and short-lived trusted-publishing
credentials. Validation and package construction should not have publishing or tag-writing
permissions; grant those capabilities only to the jobs that need them.

### Failure And Ordering Guarantees

All refusal checks are fail-closed: a branch, commit, version, existing tag, existing package,
generated-content drift, package-inspection problem, or validation failure ends the run without
publishing, tagging, or releasing. Package publication is the irreversible boundary. A failure after
partial publication must stop before tag and GitHub-release creation and require explicit human
recovery; it must not be hidden by duplicate-skipping behavior.

The ordering invariant is:

```text
validate commit and version
  -> validate, pack, inspect, smoke-test, and upload same-run packages
  -> publish those exact packages successfully
  -> create annotated tag at the validated commit
  -> create GitHub release
```

This target must be implemented as a separate, reviewed workflow change. Until then, do not infer
these capabilities from this design note or bypass the current release procedure.
