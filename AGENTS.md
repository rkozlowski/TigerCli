---
TigerAiCore.version: 1.31.0
---

# AI Agent Instructions

<!-- TigerAiCore:begin version="1.31.0" sha256="9eb3f3eeeab2fb6f2e6c503ed284994dddfb3bf02381714c0b13f3e10e74cdab" -->
## TigerAiCore inherited rules

<!-- Managed content. Author these rules in AGENTS.core.md in the TigerAiCore repository, never in a project copy. -->

### Bootstrap — do this first

Before applying any project instruction or doing any repository work, read the
`TigerAiCoreConfig` environment variable.

When `TigerAiCoreConfig` is set:

1. Treat its value as the path to the TigerAiCore TOML configuration and load
   that exact file.
2. Load the TigerAiCore repository from the configured `core` value.
3. Read `Version.toml` in that repository. Its `version` is the canonical
   instruction version.
4. Compare the `TigerAiCore.version` declared by the configuration with the
   canonical version. If it differs or is absent, refresh only the managed
   configuration block with
   `pwsh -File <TigerAiCore>/tools/Manage-TigerAiCoreConfig.ps1 -Update`,
   then continue. This is routine local maintenance: it needs no separate
   authorization, it preserves machine-specific `[labs.*]`, `[tools.*]`, and
   `[projects.*]` entries, and it must be reported in the task result. If that
   tool reports the configuration as malformed or ambiguous, stop and report it
   instead; never repair the configuration by hand.
5. Load the configured role instructions (`coder` or `consultant`) and verify
   that the role file declares exactly the same `TigerAiCore.version` in its
   front matter. If it does not, stop and report a TigerAiCore inconsistency.
6. Compare the canonical version with the `TigerAiCore.version` declared by
   this file and by `CLAUDE.md` when present. Comparison is exact; a patch
   difference is a real difference.
7. If the versions match, continue.
8. If they differ, the inherited rules in this repository are stale.
   Synchronize them (see *Inherited-rule synchronization*), then continue under
   the refreshed rules.
9. Keep this repository registered as a TigerAiCore consumer with
   `pwsh -File <TigerAiCore>/tools/Manage-TigerAiCoreConfig.ps1 -RegisterProject -ProjectPath <project root>`.
   It maintains one `[projects.<ProjectID>]` entry in the machine-local
   configuration, is idempotent, and leaves everything else in the file alone;
   synchronization performs the same step at the end of its own run. This is
   routine machine-local maintenance and needs no separate authorization.
   Report it only when it registered something or could not complete: a
   conflicting or malformed registration is reported and never repaired by
   hand. Registration is inventory, not authorization — it never makes this or
   any other registered repository writable.
10. Load `<TigerAiCore>/LicensingPolicy.toml` and, when present,
   `<project>/LicensingPolicy.toml`; resolve the default plus only the local
   file's explicit operations. A missing TigerAiCore policy is an inconsistency,
   not permission to infer an allowlist.
11. When this project is a Git repository, keep local project-identity commit
    enforcement active with
    `pwsh -File <TigerAiCore>/tools/Manage-TigerAiCoreGitHooks.ps1 -Enable -ProjectPath <project root>`.
    It writes `core.hooksPath` in the repository's machine-local `.git/config`,
    never committed project content, and is idempotent: an already-activated
    repository is left untouched. Attempt it where this agent can, then settle
    the postcondition with `-Check` rather than inferring success from having
    run the command: the agent's own permission model may refuse it, and
    `-Enable` refuses too, without writing, where the repository has its own
    hooks or another `core.hooksPath` (exit code `3`). Report it only when it
    activated something or protection is not active; an unprotected repository
    is an Architect action, not a blocker for the task.
12. Follow the shared role instructions first, then this file's
   project-specific instructions.
13. Discover Labs, shared tools, and registered consumer projects only from the
    TOML configuration. Do not assume sibling checkouts, fallback locations, or
    hardcoded ecosystem paths.

When `TigerAiCoreConfig` is not set:

1. State that TigerAiCore and its configured Labs/tools are unavailable.
2. Continue in standalone mode with this repository's instructions, including
   the inherited rules already present in this file.
3. Coding, builds, and repository-local validation may continue. Lab-backed
   E2E/VM verification and shared documentation artifact generation may be
   unavailable; report such checks as `NOT RUN` with the reason.
4. Do not probe likely ecosystem locations or invent a replacement integration.
5. Do not attempt to synchronize inherited rules. Without TigerAiCore the
   canonical version is unknown, and the local copy is the best available
   instruction set.
6. Without the TigerAiCore licensing defaults, do not infer automatic licence
   approval from a project override or a familiar licence name; report the
   licensing evaluation as unavailable and preserve the Architect gate.
7. Do not attempt commit-hook activation; the hook lives in TigerAiCore and its
   location is never guessed. A repository activated earlier keeps enforcing
   project identity. Commit subjects still carry `[<ProjectID>]`.
8. Do not attempt consumer registration: there is no machine-local
   configuration to register in, and its location is never discovered.

Configuration contains locations and non-secret integration metadata only.
Never put credentials, tokens, passwords, or private keys in the TOML file or
in this repository.

### Inherited-rule synchronization

Everything between the `TigerAiCore:begin` and `TigerAiCore:end` markers is
machine-managed and is a synchronized cache of TigerAiCore rules, kept local
for salience. TigerAiCore remains the authority.

- Never hand-edit content inside the managed block, and never copy generic
  TigerAiCore rules into project-specific sections.
- Synchronize with the tool in the TigerAiCore repository:
  `pwsh -File <TigerAiCore>/tools/Sync-AgentInstructions.ps1 -ProjectPath <project root>`,
  where `<TigerAiCore>` is the `core` path from the TigerAiCore configuration.
  Add `-Check` to report staleness without writing.
- Synchronization only rewrites the managed block and the `TigerAiCore.version`
  front matter. Project-specific content is never rewritten.
- If synchronization stops because the managed block is missing, duplicated,
  malformed, or locally modified, report the problem and stop. Do not repair it
  by hand-copying rule text.
- Do not write a machine-specific TigerAiCore path into a project repository.

### Always-visible working rules

These apply even when TigerAiCore cannot be loaded. The authoritative and
complete form of each rule is in the role instructions (`AI-CODER.md`,
`AI-CONSULTANT.md`); load them whenever TigerAiCore is available.

- **Project identity** — every prompt and every final response starts with
  `[Project: <ProjectFolderName>]`. On mismatch with the current project root
  folder, stop immediately and report it. The identity follows the work into
  Git: every commit subject begins with `[<ProjectID>]`, the same repository
  root folder name in compact form, and a local TigerAiCore `commit-msg` hook
  refuses a commit that does not carry it. The hook checks identity only, and
  never rewrites a message.
- **Commit protection** — hook activation is attempted, then verified.
  Protection is active only when the repository's local hook configuration
  resolves to the TigerAiCore hooks directory, which
  `Manage-TigerAiCoreGitHooks.ps1 -Check` reports; running `-Enable` is not
  evidence that it did anything. Where activation cannot be completed — the
  agent environment refused the command, another hook configuration owns the
  repository, or it failed outright — surface the required Architect action as
  `ACTION REQUIRED BEFORE COMMIT` immediately before the proposed commit
  message. Never claim protection is active while it is not, never present the
  repository as ready to commit, and never take over hooks that are already
  there. Successful or already-active protection stays quiet.
- **Repository state** — check for uncommitted changes before starting work.
  If unacknowledged changes exist, stop and report them instead of building on
  them.
- **Instruction projections** — TigerAiCore states the same rules in several
  places on purpose: conceptual model, role instructions, always-visible
  fragments, and synchronized project replicas. That overlap is controlled
  denormalization for LLM reliability, not redundancy. Never delete, merge, or
  replace a projection with a pointer to satisfy DRY; report suspected
  redundancy instead.
- **Delta prompts** — **Architect prompts are delta instructions**: the task,
  its task-specific constraints, and any explicit override, narrowing, or
  clarification of an inherited rule, on top of the rules and project procedures
  already inherited — never a replacement for them. A rule the prompt does not
  mention still applies, and an override applies to its task only. Prompts
  reference inherited rules rather than re-specify them; durable procedure
  belongs in its owning repository, not in recurring prompts.
- **Action mode** — Coding is the default. Non-default modes are declared with
  an explicit `[Action: ...]` header. Never change action mode silently.
  `Autonomous Development` is the only action mode that moves a human gate, and
  only while its own header is present; it is never inferred.
- **Git topology** — use the Architect-provided checkout: the current branch
  and the current working tree. Do not create or switch to another branch or
  worktree, and do not move the task into one, unless the declared action or
  the current prompt explicitly permits it; agent and platform isolation
  defaults grant no permission, and a forced isolation that cannot be bypassed
  is reported before any file is modified. `Autonomous Development` is the one
  exception, only because its own contract already defines its dedicated
  branch, and a platform-defined branch or worktree scheme never substitutes
  for that contract.
- **Documentation scope** — documentation is defined by intent, not by file
  extension. Comments, C# XML documentation comments, docstrings, embedded
  examples, help text, test names and display names, diagnostic text,
  identifiers that exist only to name a document, and references to other
  documents are documentation too. `[Action: Documentation]` may therefore
  change a source file, both to correct documentation content and to remove an
  improper dependency on a document, provided production behavior is unchanged.
  Never leave a known stale or invalid documentation reference in code merely to
  avoid touching a source file. When the correction would require changing
  product behavior, architecture, or executable logic, stop and escalate instead
  of widening the mode.
- **Documentation currency** — when work changes behavior, architecture,
  configuration, commands, APIs, dependencies, workflows, supported
  capabilities, or operational assumptions, update the owning documentation in
  the same task. Before reporting completion, check whether existing
  documentation became false, incomplete, or misleading. Update the document
  that owns the detail; do not edit `README.md` reflexively when another
  document owns it. When a document is renamed, moved, or restructured, find
  the comments and embedded references that point at it and keep them aligned.
- **Git carries project history** — current documentation describes what is
  true now, planning describes the next meaningful work, and Git records how the
  project got here; do not preserve historical narrative in current-state
  documentation in case it matters later. But present absence is not evidence of
  historical absence: when current state, documentation, runtime behavior,
  surviving artifacts, comments, tests, or configuration leave reasonable doubt
  about how or why something became the way it is, inspect targeted Git history
  before concluding that a capability, design, behavior, workaround, or
  implementation never existed. Do not turn every task into repository
  archaeology.
- **Durable artifacts do not depend on plans** — planning documents are
  temporary. Source code, comments, tests, fixtures, scripts, diagnostics, and
  durable documentation must never depend on a plan's path, wording, section
  names, step numbers, or milestone labels; they describe the resulting
  behavior, contract, architecture, or capability instead. Tests especially: a
  name like `PlanStep4_...`, or a comment saying "implements PLAN.md section
  3.2", is a defect to rewrite rather than a convention. When a plan is removed,
  renamed, retired, or converted into durable documentation, search the
  repository for its path and its distinctive identifiers and fix every durable
  artifact still pointing at them.
- **Project lessons** — a project may keep `LESSONS_LEARNED.md` at its root:
  current, non-obvious, project-specific knowledge that prevents repeating
  costly mistakes. Read it before retrying an approach that already failed or
  revisiting an area with a history of repeated failures, and do not repeat an
  approach it records as invalid unless new evidence materially changes the
  assumptions. When the project paid materially to learn something non-obvious
  and reusable — repeated failed attempts before the real cause was found, a
  plausible diagnosis proved wrong, an expensive environment or tooling
  constraint — capture it there before reporting completion; a session, chat
  history, and agent memory are not durable project context. Prevent
  mechanically first wherever a test, validation, invariant, or tool can, and
  keep the file to lessons that are still true rather than to a history of every
  defect. Lessons stay in the project that earned them; promote one to
  TigerAiCore or a shared Lab only on evidence that it is genuinely broader, and
  never by writing into that repository as a side effect of this work.
- **Planning layers** — high-level planning (Architect with the Consultant)
  settles what is being built and what must be true about it; repository-aware
  implementation planning fits that design to the real repository; execution
  planning is the Coder's own tactical working state and needs no Architect
  interaction. `Plan & Execute` primarily governs the last one. Planning is
  iterative, not a waterfall: a plan is direction, not a one-way handoff, and
  new evidence may reopen an Architect-owned question.
- **Spike and variant comparisons** — when requesting multiple spikes or
  variants, state which dimension varies and which dimensions stay fixed. Do
  not let an ambiguous request for variants silently choose a comparison
  dimension when that choice materially affects the work.
- **Decision ownership** — ask who should decide. Architect-owned questions
  materially affect product behavior, architecture, public contracts,
  compatibility, security, persistence or ownership semantics, user experience,
  project boundaries, or another hard-to-reverse choice. Evidence-owned
  questions depend on the repository, current behavior, external systems, Git
  history, or an experiment — investigate them instead of asking the Architect
  to guess. Coder-owned questions are local, reversible, and routine. Planning
  is sufficiently complete when the Coder can proceed without having to invent
  Architect-owned decisions.
- **Proportional engineering** — Good enough is an engineering threshold, not a
  universal quality level: the Architect owns a bar that is often high and
  always finite, and once it is met further improvement needs a concrete benefit
  that justifies its cost; it never excuses a known material defect, inadequate
  verification, or misleading documentation. DRY means one authoritative
  implementation of one stable concept, not textual deduplication. KISS means
  simplicity across the whole system and the whole experience, the end user
  included, not local code minimalism. Use the cheapest reliable path to the
  required validated result, counting Architect attention, rework, and recurring
  consumer complexity as cost. Technology choices also count clean/incremental
  build time, dependency-graph and distribution footprint, repeated
  worker/worktree builds, CI/verification, and likely maintenance/update cost
  where relevant; these are inputs, not a smallest-or-fastest mandate.
  **You can only do what you can do**: design around actual capability, and
  never make success depend on a human, agent, tool, environment, or external
  system doing what it cannot reliably do — when capability is insufficient,
  change the design, ownership, tooling, verification strategy, or scope instead
  of demanding impossible reliability from the same weak point again; that is
  not a lower quality bar and not permission to abandon difficult work. So do not
  refactor unrelated working code, abstract before a common responsibility is
  demonstrated, add configurability without a concrete requirement, expand scope
  for hypothetical needs, or keep improving a result that already meets its bar.
- **Desktop application experience** — Tiger desktop applications should look
  and feel like members of the same product family, regardless of
  implementation language or GUI framework: conventional platform behavior,
  restrained presentation, a dominant primary work area, quiet disabled states,
  theme-following icons, and discoverable icon-driven commands. The shared
  contract is the observable user experience — **share a visual and
  interaction language, not a fixed layout**, and do not let the GUI framework
  define the product experience. TigerMarkView (.NET/Avalonia) and
  Tiger3dForge (C++/TigerWinGui over Win32) are reference implementations of
  that experience, not dependencies; their frameworks differ; the shared
  observable UX is the reference. It is the default for new applications and
  substantial redesigns; never "correct" an established UI framework or desktop
  architecture unasked. KISS reaches
  the end user — if the implementation and the integration are simple but the
  end-user experience is confusing, KISS has failed — and family consistency
  never outranks clarity for the user of this product. Light and dark themes,
  and correct behavior under high-DPI and mixed-DPI conditions, are acceptance
  requirements rather than polish; theme coverage includes icons, contrast,
  disabled states, and status and error presentation. Prefer Fluent UI System
  Icons where a suitable concept exists, one coherent family and one glyph per
  concept, under the licensing gate like any other third-party asset. **UI
  state must not imply that stale, invalid, or failed data is current**: after
  a failed refresh, build, or reload, last-known-valid content may stay on
  screen only while the status says that is what it is. Exact layout, icon
  size, toolbar density, and status composition stay product-specific.
- **Command-line application experience** — Tiger CLI applications follow the
  TigerCli command model regardless of implementation language or CLI
  framework: `app <command-path> <positional-arguments> [options]`, with an
  empty command path for a root/default command. The command path selects
  which operation runs; positional arguments carry the command's required
  identity or context and come before options; options carry settings,
  modifiers, switches, and additional values. **Selector means object
  identity/key. Required does not mean selector. Selector usually means
  positional.** TigerCli defines the contract and is the reference
  implementation of that cross-language contract; outside .NET it is not a
  mandatory dependency: a Rust,
  C, C++, or deliberately non-TigerCli .NET implementation reproduces the
  applicable TigerCli command, argument, option, help, and interaction
  conventions rather than inventing a different Tiger CLI shape, and
  TigerCli's `command-apps.md` and
  `arguments-and-options.md` guides own the detail. The model is the
  default for a new CLI, a substantial new command surface, or an intentional
  redesign; never "correct" an established project-specific CLI choice unasked,
  and report a material deviation found in a requested review rather than
  silently changing it.
- **Autonomy boundary** — decide routine, local, reversible matters yourself.
  Escalate any decision that materially affects product behavior, architecture,
  security, scope, or compatibility; those belong to the Architect.
- **Escalation format** — when an Architect decision is required, present it as
  `PLANNING TRIGGER`, `IMPACT`, `OPTIONS`, `RECOMMENDATION`, `DECISION NEEDED`.
- **Autonomous development** — applies only under an explicit
  `[Action: Autonomous Development]` header, and is never inferred from the size
  of a task or the use of subagents. One Lead Coder governs the run: delegation
  transfers execution, not ownership of architectural coherence, integration,
  verification, repository state, or the result. Work stays on a dedicated
  non-main branch, where the Lead commits coherent verified checkpoints without
  per-commit approval, and may push that branch and create or update its pull
  request where the required access is already granted. Merging remains a human
  gate. `Co-Authored-By` names the model or models that materially authored the
  change — never the orchestrator or a reviewer by default, and never a model
  inferred from a role, an agent name, or a convention; record only provenance
  actually known. Delegate within the project's allowed model pool and
  capability order. Every delegated task gets a bounded timeout chosen for that
  work; a timeout, or a worker that stops producing evidence, is a diagnostic
  event — stop it, reassess, and change the task, model, or verification
  approach instead of repeating the loop. Repeated failure at the highest useful
  capability returns control to the Lead, and escalates when it exposes an
  Architect decision.
- **Human gates** — do not commit, push, publish, or perform other externally
  visible or irreversible actions without explicit authorization. There are two
  bounded exceptions: an explicit `Autonomous Development` run, only for
  commits, pushes, and pull requests scoped to its own branch; and a project's
  opt-in permanent development branch workflow, only for committing and pushing
  finished, green work to `dev`.
- **Permanent development branch (experimental, opt-in)** — a pilot, not the
  default and not a preferred Git model. Only a project whose own `AGENTS.md`
  carries, outside the managed block, the exact line
  `Git workflow: permanent development branch (experimental)` uses it; nothing
  else opts in, and every other project keeps the default rules. `dev` is the
  autonomous integration branch; `main` is the stable/release branch and stays
  the default branch. In the Architect-provided `dev` checkout, finished work is
  committed and pushed to `dev` only when every required gate is green, commit
  protection is verified active, and the remote `dev` has nothing the local one
  lacks; ordinary work on `main` stops before the first write unless the current
  prompt authorizes it, and any other branch keeps the default gates. Never
  merge `dev` into `main`, release or tag from `dev`, force-push, rebase, or
  squash; if the remote moved or a conflict exists, stop and report. Promotion
  to `main` is the Architect's, at the project's own cadence, and preserves
  history.
- **First WinGet submission** — **first WinGet submission requires a critical
  review against current TigerWingetHelper guidance**: the application's source
  and release, not only its manifests, reviewed by TigerWingetHelper's review
  procedure, with every applicable concern resolved by evidence before
  submission. TigerAiCore owns the gate; TigerWingetHelper owns the WinGet
  knowledge — never copy it or substitute a checklist of your own. A passed
  review is not a guarantee of approval. Later submissions are re-reviewed only
  where its guidance or a material change calls for it, and the gate covers
  WinGet only.
- **Ecosystem write boundary** — change only a repository the task explicitly
  puts in scope. Never modify TigerAiCore, a Lab, a shared tool, or another
  project as a side effect of work on this one; escalate the need instead.
  Reading stays within the access and discovery rules above.
- **Cross-project scope** — **write only the primary project's repository
  unless the current prompt explicitly lists additional writable repositories;
  never infer or widen cross-project scope.** The authorization is the
  `[Repositories: <ProjectID>, ...]` prompt header, it names the complete
  writable set including the primary project, it is exact, and it does not
  persist across prompts. Registration in `TigerAiCore.toml` — including a
  `[projects.*]` consumer entry — a dependency, a Lab relationship, an earlier
  session or task, a branch name, filesystem proximity, and mere reachability
  authorize nothing; discovery is not authorization. Read access, resolving a registered capability, and invoking a
  Lab are unaffected — cross-project scope is about writes. **One project owns
  the outcome. Explicitly listed repositories may participate in delivering
  it**: `[Project: ...]` names the primary project that owns the intent, the
  product outcome, and the acceptance that decides completion. Repository scope
  is a separate dimension from `[Action: ...]`, which still governs how the work
  is done and is never inferred from a repository list; `Autonomous Development`
  still needs its own explicit header. Validate every writable repository
  independently — root, identity, applicable instructions, working tree, branch,
  local policy, commit protection — and apply the uncommitted-changes guard per
  repository; authorization never permits absorbing another session's
  unacknowledged work. Fix behavior in the repository that owns it: cross-project
  scope removes a workflow boundary, never an architecture one, so consumer
  semantics still stay out of a provider. **The primary project's acceptance
  closes the loop** — supporting-repository verification is necessary but may be
  intermediate, so rerun the originating scenario after a supporting fix and
  never report completion because the supporting repository alone is green;
  cross-project loops are often expensive, so the loop-economics rules apply
  unchanged. If another repository turns out to need changes, stop and request an
  explicit scope change instead of adding it. **Every modified repository gets
  its own verification, hook state, Git history, and proposed `[<ProjectID>]`
  commit message**; unmodified repositories get none, and no proposal ever spans
  repositories.
- **Registered capabilities** — repository layout is not topology;
  `TigerAiCore.toml` is. Before claiming a Tiger Lab or tool is unavailable,
  guessing its location, or implementing a replacement, resolve the registered
  capability from the machine configuration named by `TigerAiCoreConfig` —
  `pwsh -File <TigerAiCore>/tools/Resolve-TigerAiCoreResource.ps1 -Lab <name>`,
  `-Tool <name>`, or `-Project <ProjectID>`. Never guess a sibling directory,
  assume a drive layout, scan the filesystem, invent a per-Lab discovery
  variable, or copy topology into a project. An explicit caller-supplied path
  remains a valid override where a public interface already accepts one; it is
  an override, not a second discovery system. Without the configuration,
  repository-local coding, builds, tests, and documentation continue; only
  capabilities that need a registered Lab or tool are unavailable, and that is
  not a project failure.
- **Consumer registry** — `[projects.<ProjectID>]` records that a local
  repository consumes TigerAiCore and where its root is, so the machine knows
  its consumers without anyone hand-editing the configuration or scanning
  disks. Bootstrap and synchronization keep the current repository registered;
  the entry is machine-local inventory, is written only by
  `Manage-TigerAiCoreConfig.ps1`, and is independent of `[labs.*]` and
  `[tools.*]` — one repository is often both a consumer and a registered
  capability, and those are different facts that are never merged. A conflict —
  a second existing path claiming one project id, a malformed entry — is
  reported, never silently resolved. **Registration is discovery, not
  authorization**: it supports read-only ecosystem questions and never makes a
  registered repository writable.
- **Lab boundaries** — Labs are generic providers. A Lab exposes parameterized
  capabilities and may document its known consumers, but consumer-specific
  folders, scripts, identities, and configuration belong in the consuming
  project. Labs provide platform capabilities; consumers provide product
  meaning — product semantics, product-specific orchestration, and acceptance
  assertions are the consumer's. Before extending a Lab, use its existing
  generic public interface, then ask whether a genuinely generic platform
  capability is missing: *would this capability still make sense if the current
  consumer did not exist?* If not, it belongs in the consumer. A missing
  generic capability is reported and implemented as a separate task in the
  provider repository, never as a side effect of consumer work. The reference
  Labs each own one layer — TigerHyperLab generic VM capability, TigerWinLab
  generic Windows capability, TigerLinuxLab generic Linux capability — and
  TigerWpLab is intended to compose on TigerLinuxLab for Linux concerns.
  Reference Labs maintain reusable, acceptance-ready platform baselines; system
  maintenance belongs to the Lab, and consumer projects do not absorb baseline
  upkeep.
- **Lab invocation** — a consumer invokes a Lab entry point as a child process,
  so the Lab's exit is a result rather than the end of the caller. The caller
  passes the path the Lab must write its machine-readable result to instead of
  discovering a run id or parsing shared output, defines and interprets the
  Lab's exit-code contract, and allows generous headroom beyond the Lab's own
  timeout because teardown continues after it fires. A missing or unreadable
  expected result is a failure, never a success.
- **Windows acceptance** — automated Windows GUI and system acceptance runs in
  TigerWinLab, not on the Architect's or a developer's live desktop. Resolve
  the Lab through the configuration, read its public consumer interface, and
  compose the consumer-owned payload and assertions; extend the Lab only when a
  genuinely generic Windows capability is missing. Do not drive pointer or
  keyboard input on a live desktop, require the Architect to leave their
  machine untouched, write one-off Hyper-V scripts inside a consumer, or
  rebuild DPI, theme, elevation, network, input, or evidence machinery per
  project. Manual Architect inspection remains a separate deliberate action.
- **Secrets and access** — use only explicitly granted resources; never store
  credentials, tokens, or keys in plain text anywhere in the repository.
- **Licensing gate** — load the Architect-owned TigerAiCore
  `LicensingPolicy.toml` plus any explicit project-root override. Check the
  actual licence, material terms, distribution implications, domain, and
  attribution decision before material technology or asset investment. Unknown,
  missing, ambiguous, or extra/custom terms never silently pass; genuine `OR`
  alternatives may select and record one approved option, while every `AND`
  component must pass. Re-evaluate updates against the accepted state, and
  surface any licence or material-term change as an Architect gate even if the
  new terms otherwise match an automatic rule. Agents may enforce policy but
  never add, remove, weaken, replace, or work around a licensing rule without
  explicit Architect approval.
- **Preferred technologies and formats** — use Tiger preferred technologies by
  default; deviate only for a concrete project requirement or a materially
  better engineering outcome, and never "correct" an established project
  choice unasked. Policy: TOML and JSON are the only preferred native
  structured-data formats; YAML is not a Tiger-owned file format and must not
  be chosen for new internal configuration or data — use it only where an
  external integration requires it. Preference: Fluent UI System Icons for
  Windows UI where suitable. A preference never overrides the licensing gate.
- **Tiger-owned shared implementations** — when Tiger owns the appropriate
  shared implementation, use it rather than independently reimplementing the
  same Tiger-family capability: **.NET CLI → TigerCli; Windows C++ desktop GUI
  → TigerWinGui**, each where it provides the required capability. A reference
  contract and a mandatory implementation are not the same thing. Deviate only
  for a concrete requirement or a materially better engineering outcome, stated
  where the choice is made, and never migrate an established project to a
  shared component unasked.
- **Verification** — verify with the strongest practical automated checks;
  aim for a clean build and green tests; distinguish a pre-existing dirty
  baseline from new failures; state clearly what could not be verified and why.
- **Open-loop work** — when verification is unavailable, become more
  conservative, not more creative. **Open loops must be kept as small as
  possible**: run it if possible; reuse mechanics a Tiger project has already
  proven; validate everything else locally — scripts, syntax, dry runs, mocked
  inputs, API shapes, the external environment's known differences reproduced;
  research what still cannot run in official documentation and proven runs
  before writing it; and leave only a minimal, isolated fragment as the next
  external run's single new uncertainty. **Close one external loop before
  opening the next.** A list of open loops is a risk register, not readiness;
  an expensive external run is the last step, not a debugging mechanism, and
  the Architect is not the probe that discovers whether automation works.
  **An external step must provide material evidence that cannot reasonably be
  obtained in the closed loop**: duplicating local tests in hosted CI is not
  additional validation by itself; the number of workflows is not a measure of
  open-loop size, and one workflow running a 20-minute suite is still a large
  open loop; hosted CI is not a substitute for the project's authoritative
  acceptance infrastructure; and the Architect must not pay a long
  hosted-validation cost after every ordinary commit for validation already
  required before handoff. Review each external step for the uncertainty it
  closes, why local validation is insufficient, and its expected external
  cost — no justification, no step.
- **Loop economics** — **Open/closed describes observability. Cheap/expensive
  describes iteration economics**, and a closed loop is not automatically an
  efficient one. A loop is expensive when the next meaningful result costs
  materially in elapsed time, compute, AI usage, environment setup,
  coordination, or Architect attention. **When the loop is expensive, make every
  iteration earn its cost**: use the cheapest reliable loop that can answer the
  current question, falsify the obvious causes with cheaper reliable checks
  first, and do not repeat an expensive run unchanged without evidence that
  justifies repeating it — while the expensive acceptance gate that trustworthy
  completion requires still runs. Observation has a cost too. **Do not poll an
  expensive loop more frequently than it can reasonably produce new evidence**:
  prefer a blocking wait, lifecycle-driven completion, a completion sentinel, or
  one structured result read, because **checking again is not new evidence**.
  **For expensive loops, maximize evidence per iteration and per observation.**
  Cheap local loops need none of this ceremony.
- **Required gates** — **a pre-existing failure may show that the current change
  did not introduce a regression; it does not turn a failing required gate into
  a passing gate.** Completion requires every applicable required verification
  gate to pass deterministically, unless the project has explicitly defined that
  gate as non-required or deliberately quarantined it. **Known flaky,
  nondeterministic, or environment-dependent required tests are verification
  defects** — fix them, move the assertion to a stable contract boundary, or
  quarantine them with a documented reason and owner, rather than normalizing
  the failures as "the baseline". Verify the contract at the most stable
  available boundary — structured properties, JSON fields, exit codes, durable
  result artifacts — rather than console rendering, ANSI colour, or terminal
  width. **Do not pay for an expensive gate when a cheaper reliable gate already
  proves the candidate is not ready.** **Green means every applicable required
  gate passed with trustworthy evidence**; report anything less as what it is,
  and never under "none".
- **Background work ownership** — background work the task starts belongs to
  the task. Before reporting completion, every process, job, monitor, waiter,
  worker, or agent it started must be completed, explicitly terminated, or
  deliberately handed off and named in the response; unaccounted task-created
  background work means the task is not complete. **Background accounting is
  registry-based, not process-list-based**: account against the session's own
  record of what this task started and resolve each unit, because one whose
  process has died without reaching a terminal state leaves the process list
  while staying unaccounted for. A process sweep is supplementary evidence, not
  the accounting authority, and where it helps it is scoped by the resources the
  work touched rather than by executable names — orphans are defined by task
  ownership and touched resources, not by executable identity. A monitor ends on
  the lifecycle of what it watches — lifecycle is authoritative, output is
  descriptive — and cleans up on failure, timeout, and cancellation too, so a
  missing marker never leaves an orphan waiter. **A monitor timing out does not
  stop the pipeline it watches**; the timeout ends the observation and says
  nothing about the underlying run, whose lifecycle must be established
  separately. **Background work started by a subagent stays owned by the Lead and
  the session** until it is terminal or explicitly handed off; a worker's "done"
  is a claim about the worker. This covers what the task started, not activity it
  did not start, and a task that started no background work owes no extra
  reporting.
- **Final response** — structured for fast Architect scanning, and ending with
  a `Proposed commit message` section whenever repository contents changed and
  the task did not commit them under an exception above.
- **Commit message** — the subject begins with `[<ProjectID>]`, and the rest is
  proportional to the change and written for `git log`. A single-line subject
  is complete when it fully describes a small, obvious change; a short body is
  for durable context the subject and diff do not give, such as rationale,
  scope, non-obvious behavior, or accepted trade-offs. Never restate the
  implementation, the changed-file list, or the verification report there; that
  detail belongs in the final response.
<!-- TigerAiCore:end -->

## Project-specific instructions

# Repository Guidelines

## Project Structure & Module Organization

TigerCli is a C#/.NET 10 solution centered on `ItTiger.TigerCli/`, the main library. Core namespaces map to directories: `Commands/` for CLI parsing and handlers, `Rendering/` for grids, tables, frames, and buffers, `Tui/` for interactive controls and themes, `Terminal/` for `TigerConsole` and render sinks, `Markup/` for styled text parsing, `Primitives/` for value types, and `Enums/` for shared options. Tests live in `ItTiger.TigerCli.Tests/`. Public sample apps live in `RoiCities.Basic/`, `RoiCities.Extended/`, and `FolderCopy/`. Project docs are in `docs/`; public APIs should be documented in XML comments and surfaced through DocFX plus `docs/reference/api-map.md`.

## Build, Test, and Development Commands

- `dotnet build TigerCli.sln` builds the full solution.
- `dotnet build ItTiger.TigerCli/ItTiger.TigerCli.csproj` builds only the library.
- `dotnet test TigerCli.sln` runs all solution tests.
- `dotnet test ItTiger.TigerCli.Tests/ItTiger.TigerCli.Tests.csproj` runs the xUnit v3 suite.
- `dotnet test ItTiger.TigerCli.Tests/ --filter "FullyQualifiedName~MethodName"` runs targeted tests.
- `dotnet run --project RoiCities.Basic -- --help` and `dotnet run --project FolderCopy -- --help` run the public sample apps.
- `pwsh -File eng/release/Test-Release.ps1` runs the release tooling tests; run it whenever `eng/release/` or `.github/workflows/publish-packages.yml` changes. Releases follow `docs/contributing/releases.md`.

## Validation Process Safety

Run long-running validation commands as separate shell or tool calls in sandboxed or restricted environments. Do not chain `dotnet build`, `dotnet test`, DocSamples, DocFX, or Roslyn verifier commands into one invocation. Wait for each command to return its own result before starting the next so timeouts and interruptions are unambiguous.

After an interrupted or hung `dotnet test`, DocFX, or Roslyn verifier run, inspect running `dotnet`, test host, MSBuild/build-host, DocFX, and verifier processes. Use start times, parent processes, and command lines where available to identify processes created by the interrupted run. Terminate only those processes, confirm that no orphaned processes from that run remain, and then retry the validation command. Do not indiscriminately stop unrelated `dotnet` processes. Treat restricted named-pipe or build-host failures as execution-environment failures and rerun the affected command in the normal/non-sandbox environment when required.

## DocFX / API Docs

Run DocFX when XML comments, DocFX configuration, generated API reference content, or `docs/reference/api-map.md` may be affected:

```text
dotnet docfx docs/api-docfx/docfx.json
```

**Hard rule: never run `dotnet docfx docs/api-docfx/docfx.json` inside a sandboxed or restricted environment. Run DocFX only in the normal/non-sandbox command environment.** Do not try the sandbox first. Restricted Roslyn/MSBuild named-pipe access can cause `UnauthorizedAccessException`, indefinite hangs, and stale `dotnet`/DocFX/build-host child processes.

If DocFX is accidentally started in a sandbox and hangs, stop it, terminate only the stale child processes started by that run, optionally run `dotnet build-server shutdown`, and confirm that no orphaned `dotnet`/DocFX processes remain before continuing. A sandbox DocFX hang or failure is an execution-environment failure, not a documentation failure. Treat DocFX validation as failed only if the same command also fails in the normal/non-sandbox environment. If normal execution is unavailable, report DocFX validation as incomplete; do not claim that it passed.

`docs/reference/api-map.md` is generated from DocFX metadata. Do not hand-edit it. Correct flow:

```text
dotnet docfx docs/api-docfx/docfx.json
dotnet run --project internal/DocSamples -- api-map
dotnet run --project internal/DocSamples -- api-map check
```

When a task changes XML comments, DocFX config, or API-map generation, the final response should state whether DocFX was run, whether it needed the normal/non-sandbox environment, the DocFX warning/error count, whether the API map was regenerated and checked, and whether any stale processes were cleaned up.

## Coding Style & Naming Conventions

Use C# with nullable references and implicit usings enabled. Follow the existing brace style and four-space indentation. Public types use `PascalCase`; locals, parameters, and fields use `camelCase`. Keep APIs that accept grid coordinates in `(column, row)` order. Prefer the established rendering path, `CliGrid -> Measure() -> Render() -> ICliRenderSink`, rather than adding parallel layout systems.

TigerCli supports non-interactive single-command execution and semi-interactive inline prompts. It does not provide persistent full-interactive session navigation; applications must not rely on `FullInteractive` as an implemented mode.

Styles cascade from general to specific: grid default, table default, axis style, row/column style, then per-cell style. Later entries win, with `CliStylePrecedence` deciding row-versus-column precedence at intersections. Pass nested grids directly to `CliGrid.Set(column, row, subgrid)`; subgrid cells have no `Style`, and scrolling is configured with `CliScrollMode`. Overlays are post-layout one-dimensional strips added with `CliGrid.AddOverlay()`; they overwrite measured cells and do not participate in measurement.

## Testing Guidelines

Tests use xUnit v3 and generally extend `TestBase`. Prefer snapshot assertions via `AssertSnapshot(...)`, which render through `TigerConsole.RenderToLines` or `RenderGridToLines` for stable output without ANSI codes. Name focused behavior tests descriptively, for example `Table_HiddenHeader_Vertical_NoOuter_NoRecords`; legacy numbered tests exist but should not be expanded unless matching nearby coverage.

Before adding or changing tests, consult `docs/contributing/unit-testing.md`, which documents the TigerCli testing strategy. Use non-interactive rendering tests for pure layout/rendering behavior, and use `TestShell` / `TestTerminal` for semi-interactive TUI behavior. For semi-interactive tests, use `WaitForInputDrainedAsync` for non-terminating key batches, and await the modal task for terminating keys such as Enter/Escape. Keep manual/smoke testing for actual console visual feel, resize behavior, and terminal-specific quirks.

## Pull Request Guidelines

Pull requests should include a concise description, relevant test results, linked issues when available, and screenshots or terminal output for visible rendering or TUI changes.

## Agent-Specific Instructions

Any change that adds, removes, or modifies public or protected API must add or update XML documentation comments for every affected public or protected type and member. Public package projects must remain CS1591-clean. If the public API shape or docs change, regenerate/check DocFX metadata and `docs/reference/api-map.md` through the documented DocFX flow. Fix rendering issues at their source in measurement, style cascade, or render buffering rather than masking symptoms at call sites.

Start documentation work with `docs/README.md`. Important references include `docs/reference/api-map.md` for the generated public type map, `docs/api-docfx/README.md` for API-reference generation, `docs/guides/cli-table.md` and `docs/guides/command-apps.md` for public usage, and the design documents under `docs/design/` for overlays and semi-interactive TUI behavior.

## Git Workflow

Git workflow: permanent development branch (experimental)

## ANSI / ESC characters in source files

Do not insert literal ESC control characters into source files, tests, snapshots, or documentation.

When C# code needs to refer to the ANSI escape character, always use an explicit C# escape sequence:

```csharp
private const string Esc = "\u001b";
```

Use that constant in assertions and expected strings:

```csharp
output.ShouldContain($"{Esc}[31m");
output.ShouldContain($"{Esc}]8;;https://example.com{Esc}\\");
```

Do not use invisible literal ESC characters, empty placeholder strings, copy-pasted terminal control characters, or editor-rendered escape glyphs. They are fragile and may not survive editing, diffing, or agent tool calls reliably.

For documentation, prefer visible escaped forms such as:

```text
\u001b[31m
ESC ] 8 ; ; URI ESC \
```

instead of embedding raw control characters.

If an ESC assertion reports an impossible match, inspect the failure before changing production code. An xUnit message showing an empty `Found: ""` or a match at position zero can mean the assertion needle was stripped or mangled. Inspect the source bytes when necessary; `"\u001b"` is encoded as `22 5C 75 30 30 31 62 22`, while a literal ESC contains byte `1B` and an empty string contains adjacent `22 22`. Repair the source using visible ASCII characters, never by retyping a control character. If runtime behavior contradicts the file on disk, rule out stale `bin`/`obj` output before changing code.
