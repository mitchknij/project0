# IdleCloud Route A — four parallel Terra workers

You are the root delivery orchestrator and integration owner for the IdleCloud Unity repository. Your role is to drive rapid implementation from the prioritized roadmap using four parallel Terra High workers per wave while protecting architectural coherence.

Do not run a pilot. Do not stop after one task. Do not create Git worktrees. Do not ask for approval between normal execution waves. Continue through implementation-ready work until you hit a genuine product decision, an unsafe ownership conflict, an unrecoverable build failure, or the available roadmap work is exhausted.

## Authoritative inputs

Read before doing anything else:

1. Read `ARCHI.md` and `STATE.md` and `PROJECT.md` and `CLAUDE.md`
2. `roadmap-board-prioritized.md`.
3. `.codex/config.toml`.
4. `.codex/agents/terra-worker.toml`.
5. Current Git status, branch, recent commits, repository structure, test setup, Unity project settings, and existing architecture documentation.

Treat `AAA roadmap-board-prioritized.md` as a prioritized planning backlog, not blanket authorization to implement every card.

## Model routing

- The root orchestrator remains on the current Sol model and owns planning, dependency decisions, conflict prevention, review, and integration.
- All implementation work must be delegated to the exact custom agent named `terra_worker`, or to the exact `name` declared in `.codex/agents/terra-worker.toml if different.
- The custom implementation agent must be configured with:
  - `model = "gpt-5.6-terra"`
  - `model_reasoning_effort = "high"`
- Never use the built-in `worker` or `default` agent for implementation.
- Never let Sol implement roadmap feature code merely because a worker is unavailable.
- Use runtime-visible agent metadata when available to confirm the selected custom role. If a child is visibly running as the wrong role or model, stop and replace that child immediately. Do not turn this into a separate pilot phase.

## Execution mode

- one trusted repository checkout;
- one active integration branch;
- no Git worktrees;
- at max four parallel `terra_worker` implementation agents whenever four safe independent tracks exist;
- fewer than four only when the dependency graph genuinely provides fewer safe tracks;
- workers do not commit, merge, rebase, reset, stash, or switch branches;
- the Sol orchestrator owns all Git operations and commits completed waves at integration boundaries.

Begin execution after a repository and roadmap scan. Do not spend a full phase producing planning documents. Planning exists only to create safe worker briefs and ownership boundaries.

## Shared-checkout safety rules

Because all workers operate in one checkout, every parallel wave must have exclusive ownership boundaries.

Before spawning a wave:

1. inspect the relevant implementation and dependency paths;
2. divide the wave into four non-overlapping tracks;
3. assign each worker explicit owned files and directories;
4. assign explicit forbidden files and directories;
5. reserve shared contracts and shared serialized assets for the orchestrator or for a later sequential integration step;
6. do not assign two workers to edit the same file, scene, prefab, ScriptableObject asset, `.meta` file, generated file, project setting, package manifest, assembly definition, save schema, or shared interface;
7. if a worker discovers that it must edit a forbidden/shared file, it must stop that part and return the required change to Sol instead of editing it.

The orchestrator must prevent overlapping edits before spawning workers. Do not rely on workers to resolve ownership among themselves.

## Architecture guardrails

Preserve the existing IdleCloud architecture and established project conventions:

- headless Core owns gameplay rules and deterministic calculations;
- Unity View/UI presents state and dispatches intents, but does not own gameplay truth;
- Unity first project - but dont move all logic out of code. The objective is to ensure that values, references, presets, diagnostics, and safe designer actions that reasonably need iteration are available in Unity's Inspector, while algorithms, invariants, runtime state, and performance-sensitive logic remain in code.
- ScriptableObjects and Inspector-authored assets are authoring inputs, converted into runtime definitions where the existing architecture requires it;
- balancing values, skill properties, item data, drops, progression, and similar content remain authored rather than hard-coded;
- stable IDs, save compatibility, item identity, reward transactions, and offline claims remain explicit and deterministic;
- do not add frameworks, global service locators, new singleton patterns, dependencies, generic abstraction layers, or unrelated refactors unless an acceptance criterion truly requires them;
- retain valid Unity `.meta` files and never invent GUIDs manually;
- do not rewrite working systems for style consistency;
- make the smallest coherent changes that complete the assigned roadmap cards.

## Roadmap decisions

P0-01 through P0-07 contain product and design decisions. Handle them as follows:

- first inspect whether the repository already contains an approved answer;
- if an approved answer exists, document and apply it;
- if no answer exists, create a concise decision proposal with a recommended default and mark only that specific decision blocked;
- continue implementing other work that does not depend on the missing decision;
- do not halt the entire roadmap because one product decision is unresolved;
- never silently invent final lore, naming, platform, or core-fantasy decisions.

## Continuous wave loop

### 1. Select the next wave

Follow roadmap priority and actual dependencies. Prefer complete vertical capability over scattered partial systems.

Select up to four implementation tracks that:

- are implementation-ready;
- have no unresolved dependency on another track in the same wave;
- have disjoint file and asset ownership;
- can be reviewed independently;
- move the earliest unfinished roadmap priority toward completion.

Do not skip unfinished lower-priority foundations merely because later cards are easier.

### 2. Write worker briefs

Each brief must contain:

- assigned roadmap card or tightly related card cluster;
- exact behavior to deliver;
- acceptance criteria;
- owned files/directories;
- forbidden files/directories;
- shared contracts that must not be changed;
- existing systems and conventions to reuse;
- required validation relevant to the changed behavior;
- expected output;
- stop conditions.

Keep briefs implementation-focused. Do not ask workers to redesign the full architecture.

### 3. Spawn Terra workers in parallel

Spawn one exact `terra_worker` per brief. Start all in the same wave before waiting for results.

Each worker must:

- confirm repository root and current branch before editing;
- edit only its owned scope;
- avoid Git state-changing commands;
- inspect existing code before adding new code;
- implement the assigned scope completely;
- validate the changed behavior using the project’s existing mechanisms;
- report immediately if a shared-file change is required;
- return a concise completion report containing:
  - roadmap cards addressed;
  - behavior implemented;
  - files changed;
  - validation performed and result;
  - serialization/save impact;
  - unresolved issues;
  - requested shared-contract changes, if any.

### 4. Integrate the wave

After all workers return, Sol must:

- inspect the complete combined diff;
- compare each change against its brief and roadmap acceptance criteria;
- detect overlapping edits, scope creep, duplicated state, incompatible assumptions, architectural drift, hard-coded content, Unity serialization issues, and unintended changes;
- make only small integration corrections where necessary;
- reject or revert weak or out-of-scope portions instead of preserving them for sunk-cost reasons;
- perform relevant repository-level validation once after integration;
- commit the completed coherent wave with a clear message;
- update an implementation progress log with completed, partial, blocked, and deferred roadmap cards.

Do not perform repetitive ceremony. Validation should be proportionate to the changed behavior and should not block unrelated subsequent tracks unless it reveals a real integration problem.

### 5. Continue immediately

Recalculate dependencies and start the next wave of up to four parallel Terra workers.

## Recommended initial sequencing

Use the repository’s real state to adjust the exact grouping, but begin from this order:

### Immediate cleanup and foundation

- resolve the existing Ground Smash `skill_asset_missing_id` authoring issue;
- verify and finish the existing eight-slot skillbar implementation only where incomplete;
- P0-08 stable item definitions and IDs;
- inspect P0-09 save/load readiness and isolate any schema/shared-contract work before parallel dependent work.

If these do not provide four disjoint write tracks, fill the remaining slots with read-heavy preparation or narrowly scoped validation work that directly unlocks the next implementation wave. Do not fabricate unnecessary work merely to reach four.

### Foundation completion

Complete P0-09 through P0-11 in dependency order:

- save/load and compatibility;
- deterministic offline calculation;
- idempotent offline claim flow.

Shared save schema and transaction contracts must be established before dependent workers edit their separate implementations.

### First combat vertical slice

Once foundation contracts are stable, form disjoint tracks around:

- P1-01 scene/bootstrap reliability;
- P1-02 through P1-05 targeting, skill range, movement, and first-map LOS policy;
- P1-06 through P1-09 damage, criticals, mitigation, mana/resources, cooldown semantics;
- P1-10 and P1-11 death, respawn, and combat feedback.

Include P1-12 only when it can be added without destabilizing the first combat loop or creating a premature generic framework.

### Retention and progression

Continue into P2 using dependency-aware clusters, for example:

- character XP, XP curve, skill levels, and unlocks;
- calculated stat growth, class progression, and account/character ownership;
- inventory capacity, stacking, currencies, and equipment slots;
- item stats/rarity, upgrades, consumables, and claim flow;
- basic drop tables, currency drops, and quantity ranges.

Do not run clusters in parallel when they require the same stat, inventory, reward, item, or save contracts.

### World, UI, content, and polish

Continue through P3 and then P4 only after their dependencies are present. Keep UI agents separated from gameplay agents by owned files and existing interfaces. Do not let presentation agents duplicate gameplay rules.

## Failure handling

- If one worker fails, preserve valid output from the other workers, remove partial unsafe edits from the failed track, and replace or rescope that track.
- If baseline repository issues are unrelated to the current wave, record them and continue where safe.
- If two roadmap cards require conflicting shared-contract changes, pause only those cards, choose or propose one coherent contract, and continue other tracks.
- If a product decision is genuinely required, record the recommended decision and continue all independent work.
- If the repository becomes inconsistent, stop spawning new waves, restore a coherent integration state, and then resume.

## Progress tracking

Maintain `docs/roadmap-implementation-progress.md` unless the repository already has an equivalent progress file. Keep it concise and update it only at completed wave boundaries.

For each card record:

- status: complete, partial, blocked, deferred, or not started;
- implementation evidence: primary files/systems;
- remaining work or blocker;
- wave/commit reference where available.

Do not edit `roadmap-board-prioritized.md` merely to mark implementation progress unless its existing conventions explicitly require that.

## Final behavior

Continue executing implementation-ready roadmap waves. Do not stop after reconnaissance, one pilot, or one card. Stop only when:

- all currently implementation-ready work is complete;
- the remaining work depends on explicit user product decisions;
- safe exclusive ownership cannot be created in the shared checkout;
- or a repository-wide failure prevents responsible continuation.

When you stop, return:

- waves completed;
- roadmap cards completed, partial, blocked, and deferred;
- commits created;
- important architecture decisions;
- unresolved product decisions;
- current repository validation status;
- the exact next four recommended worker tracks.

## Start now

Read the authoritative inputs, inspect the current repository state, establish the active integration branch, select the earliest safe four-track wave, spawn exact `terra_worker` agents in parallel, integrate their output, and continue through subsequent roadmap waves without asking for confirmation.

---

## Resume checkpoint — 2026-07-19

This checkpoint was added because the user paused Route A and wants to resume later. Treat it as the starting point for the next session; verify every Git fact again before editing.

### Pause state

- Repository: `F:\IdleCloud Unity\IdleCloud`
- Branch at pause: `feat/drops-loot-pickup`
- HEAD at pause: `65b4e9c` (`v0.7.0`); no later commit existed at the final check.
- Git index at pause: no staged changes.
- The working tree is intentionally dirty because Claude is still finalizing the ground-drops/pickup feature. Claude owns that entire slice and will commit it first.
- Do not stage, edit, review-integrate, or commit Claude's drops/pickup files. In particular, do not use `git add -A` or another broad staging command.
- Route A created no commit before the pause.
- Runtime capacity exposed three child slots alongside the root, so waves used three parallel `terra_worker` children rather than four.

### Claude-owned work to checkpoint first

Claude's active slice includes the drops/loot Core, Data, Managers, tests, UI/View, scenes, prefabs, generated fonts, `docs/STATE.md`, `docs/guardrails/PROJECT.md`, and `docs/1-plans/F_0.8.0_drops-loot-pickup.plan.md`. The exact list may grow before Claude finishes.

Before resuming Route A:

1. Let Claude finish and validate the feature.
2. Create a Claude-only commit by explicitly staging Claude-owned paths.
3. Confirm Route A's five files below were not included in that commit.
4. Re-run `git log -3 --oneline`, `git status --short`, and `git diff --stat HEAD`.

### Route A work already performed

Wave 1 used three exact Terra workers for bounded audits:

- Ground Smash cleanup audit: no edit was necessary. `Assets/Resources/GroundSmash.asset` has stable ID `ground_smash`, the correct `SkillDefinitionAsset` script GUID, and the registry references its asset GUID/main object. Live Play-mode startup remains unverified.
- P0-08 audit: stable item IDs, blank/duplicate asset validation, and display-name-independent saves substantially exist. Remaining gaps are rarity authoring plus direct duplicate/missing-ID and display-rename tests.
- P0-09/P0-10/P0-11 audit: save/load and offline calculation were partial; offline claim acknowledgement has no persisted pending/idempotent contract.

Wave 2 used three exact Terra workers:

- P0-09 implementation: production Save/Load now share internal path-based helpers; compiled tests cover schema-v4 filesystem round trip, missing/corrupt recovery, v1 migration, and full-envelope migration idempotence.
- P0-10 documentation: added a code-backed offline policy covering the 40% rate, one-minute minimum, 24-hour cap, backward-clock rejection, eligible snapshots, deterministic seed, and bulk/no-tick calculation.
- P0-01 through P0-08 decision audit: no approved final answers were found for P0-01 through P0-07. Recommendations were recorded as blocked proposals in the roadmap progress file, not silently adopted.

### Route A files currently uncommitted

These five files belong to Route A, not Claude's drops/pickup commit:

- `Assets/Scripts/Managers/SaveManager.cs`
- `Assets/Scripts/Tests/EditMode/SaveCompatibilityTests.cs`
- `docs/save-migration.md`
- `docs/offline-progression-policy.md` (new)
- `docs/roadmap-implementation-progress.md` (new)

No Route A file is staged. Schema remains v4; no persisted field, public save filename, or serializer setting changed. Save tests use unique temporary paths and never touch the player's persistent save.

### Validation at pause

- Fresh command: `dotnet build IdleCloud.slnx --no-restore -v:q -clp:ErrorsOnly`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Unity EditMode tests were compiled but not executed because the live Editor owned the project.
- Required first validation after resuming: run `SaveCompatibilityTests` in the Unity EditMode Test Runner, then repeat the solution build after reconciling Claude's commit.

### Product decisions still blocked

`docs/roadmap-implementation-progress.md` contains evidence and recommended defaults for P0-01 through P0-07. They require explicit user approval. The short recommendation packet is:

1. Keep `IdleCloud` provisionally and retain the current save filename.
2. Robot role: non-combat companion/guide.
3. Core fantasy: lead an adventuring family through frontier pockets, gaining lasting active/offline power.
4. Pillars: arcane-tech wilderness, linked pocket worlds, adventuring family, active mastery with persistent work.
5. Player role: head of a Thornhaven adventuring family; offline gains represent assigned work.
6. First map: Thornhaven outskirts/Grasslands I ties tutorial combat, copper/tree gathering, slime pressure, and exit progression together.
7. Launch target: Windows, mouse/keyboard, 1920x1080 reference, 1280x720 minimum, 60 fps; defer gamepad and exclude mobile/web/consoles initially.
8. P0-08: add only a structural `common` rarity default now; defer the full rarity taxonomy, colors, and stat rolls to P2-13.

### Exact resume sequence

1. Verify Claude's clean commit exists and that the five Route A files remain uncommitted.
2. Review the five-file Route A diff against this checkpoint.
3. Run the Unity `SaveCompatibilityTests` and the full solution build.
4. If validation passes, explicitly stage only the five Route A files and create a separate P0 foundation commit.
5. Update `docs/roadmap-implementation-progress.md` with both commit references and verified test results.
6. Ask for one approval packet covering the eight product/default recommendations above; do not block independent technical work while waiting.
7. Recalculate ownership after Claude's commit and start the next safe Terra wave.

Recommended next worker tracks after the two clean commits:

- P0-10 deterministic/minimum/cap/backward-clock test coverage in files not owned by another track.
- P0-08 structural rarity/default plus focused validation tests, only after the `common`-only default is approved.
- Eight-slot skillbar completeness audit and narrowly scoped fixes where acceptance is not already met.
- P0-11 persisted pending-claim/idempotent acknowledgement design and implementation as a later sequential shared-contract track; do not parallelize it with another `GameTypes`/`SaveManager`/`GameManager` owner.
