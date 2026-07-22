# IdleCloud Unity -> Web App Migration Plan

## 1. Project analysis snapshot

This migration plan is based on direct inspection of the current repository structure, architecture docs, and representative code in each runtime layer.

### Top-level composition

- Assets (~946 files): all runtime/editor game code, art, scenes, prefabs, tests.
- docs (~43 files): architecture, policies, roadmap, plans, testing notes.
- ProjectSettings (~26 files): Unity platform and rendering settings.
- Packages (2 files): Unity dependency manifest and lock.
- Root design docs: gameplay loop, roadmap, battle and world notes.

### Assets folder subsystem map

- Assets/Scripts/Data
  - Pure state and content definitions, repositories, and contracts.
  - Key migration value: this is the safest source of truth for web domain models.
- Assets/Scripts/Core
  - Headless deterministic simulation: combat math, activity snapshots, offline progression, drops, progression.
  - Key migration value: direct logic-port candidate to TypeScript.
- Assets/Scripts/Managers
  - Session orchestration, save/load, content bootstrapping, combat/gather coordinators.
  - Key migration value: maps to web application service/store layer.
- Assets/Scripts/View
  - Isometric spatial view, pathfinding integration, combat feedback, scene orchestration.
  - Key migration value: split into browser rendering and optional server-authoritative movement.
- Assets/Scripts/UI
  - Unity panel system and runtime-built HUD.
  - Key migration value: maps to React components and route-driven panel architecture.
- Assets/Scripts/Tests
  - Extensive EditMode logic tests and PlayMode smoke tests.
  - Key migration value: convert EditMode logic tests to Vitest/Jest first.
- Assets/Iso, Assets/Scenes, Assets/Prefabs, Assets/Art
  - Unity rendering/presentation content and authoring assets.
  - Key migration value: convert data-bearing assets and selected sprites, reimplement render rules.
- Assets/Editor
  - Build/generation tools for prefabs and UI bake.
  - Key migration value: replace with content export and validation scripts.

### docs folder value for migration

- docs/ARCHI.md and docs/guardrails/PROJECT.md: canonical architecture and gameplay flow contracts.
- docs/offline-progression-policy.md: exact expected behavior for offline calculations.
- docs/world-scene-structure.md: scene ownership and initialization responsibilities.
- docs/4-unit-tests and docs/3-code-review: quality and risk history.

## 2. Core ideas to preserve (non-negotiable)

1. Deterministic simulation for economy/combat outcomes.
2. Snapshot-based offline progression (bulk calculation, no tick replay).
3. Data-driven content contracts and validation.
4. Multi-character account with shared bank and consistent save schema evolution.
5. Strict architecture direction: data/content -> simulation -> orchestration -> presentation.

## 3. Target web architecture

## 3.1 Runtime layout

- webapp/src/domain
  - Pure TypeScript models and simulation logic (no React imports).
- webapp/src/content
  - JSON/TS content registries equivalent to RuntimeContent provider outputs.
- webapp/src/state
  - Session orchestration and command handlers (Zustand now, replaceable later).
- webapp/src/ui (currently in App.tsx, should be split next)
  - React presentation and interaction.
- Optional future: webapp/server
  - API, persistence, and anti-cheat authority for multiplayer or trusted progression.

## 3.2 Mapping from Unity to web

- Assets/Scripts/Data -> webapp/src/domain/types + webapp/src/content
- Assets/Scripts/Core -> webapp/src/domain/simulation
- Assets/Scripts/Managers -> webapp/src/state and future server services
- Assets/Scripts/UI + Assets/Scripts/View -> React UI + canvas/webgl presentation module
- Assets/Scripts/Tests/EditMode -> Vitest unit suite
- SaveManager schema versioning -> JSON schema migration pipeline in web storage/backend

## 4. Delivery plan

### Phase 0: groundwork (done in this change set)

- Create webapp scaffold (Vite + React + TypeScript).
- Add domain contracts reflecting Unity state concepts.
- Add deterministic-style offline simulation seed implementation.
- Add state store and interactive UI to exercise loop concepts.

### Phase 1: deterministic parity for offline + progression

- Port formulas from Core/Offline.cs and Core/Activity.cs line-by-line.
- Port progression math from Progression.cs and Character helper rules.
- Add deterministic RNG contract matching OfflineSeed.cs behavior.
- Build golden-file tests using existing save fixtures and expected outputs.

### Phase 2: active combat vertical slice parity

- Port ActiveSim command/event contracts (manual vs auto command ingestion).
- Port AutoCombatPolicy and skill timing/cooldown handling.
- Recreate combat events feed and loot flow in web UI.
- Keep spatial target info abstracted so renderer can vary.

### Phase 3: content pipeline and save compatibility

- Export ScriptableObject/content assets from Unity to JSON snapshots.
- Build content validators matching ContentValidator/CoreValidation checks.
- Implement save migration versions equivalent to SaveManager schema behavior.
- Add import path for existing player save data where possible.

### Phase 4: rendering and map interaction

- Replace Unity scene split with route/state split:
  - bootstrap
  - persistent account shell
  - map view module
- Implement 2D/isometric rendering using Canvas/WebGL (PixiJS or Phaser) while keeping UI in React.
- Mirror click-to-move and tile/path constraints using web pathfinding module.

### Phase 5: platform hardening

- Storage strategy:
  - local-first IndexedDB for offline-capable single-player
  - optional backend sync for cross-device and anti-tamper integrity
- telemetry and balance tuning hooks
- CI gates: build + unit tests + deterministic replay checks

## 5. Risks and mitigation

- Risk: formula drift from C# to TypeScript.
  - Mitigation: parity tests generated from the same fixtures and seeds.
- Risk: content drift between Unity assets and web JSON.
  - Mitigation: Unity export script plus schema checksum in web runtime.
- Risk: rendering rewrite delay.
  - Mitigation: keep simulation and UI shell independent of final renderer.
- Risk: save incompatibility.
  - Mitigation: explicit versioned migrations and fallback recovery path.

## 6. Definition of done for migration

1. Web simulation returns deterministic offline/combat outcomes equal to Unity for the same snapshot.
2. Save schema migrations are versioned, tested, and reversible with backups.
3. Content validators block invalid IDs and malformed drop tables before runtime.
4. Core loops (combat, gathering, offline claim, inventory/bank updates) are fully playable in browser.
5. Unity no longer required for daily gameplay iteration or balance testing.

## 7. Immediate next implementation tasks

Completed in the browser migration workspace:

1. Progression, weighted/tertiary drop behavior, versioned browser persistence, and deterministic replay coverage.
2. Unity PascalCase account import, including character inventory, activities, skill progress, and bank stacks.
3. Deterministic tile-pattern resolution with Unity-compatible tile/actor ordering and target caps.
4. Content-boundary validation plus a Unity Editor command that exports validated, browser-shaped JSON snapshots.

The remaining delivery phases require tools or product decisions outside this workspace pass:

- Open the Unity project and run **IdleCloud/Web/Export Content Snapshot** to create the generated content payload.
- Install Node.js/npm, then execute the web build and Vitest suite.
- Select a browser renderer (Canvas/WebGL implementation) and storage authority (local-only versus backend sync) before implementing map rendering, pathfinding, or anti-tamper services.

## 8. Progress update (implemented)

- Added active-combat web domain contracts and tick reducer in webapp:
  - command queue ingestion (`SelectTarget`, `TriggerSkill`, `MoveIntent`)
  - target validation and movement requests for out-of-range situations
  - manual skill cooldown and mana handling
  - deterministic seeded damage roll with critical hits
  - auto-attack cadence and enemy retaliation
  - combat events (`TargetSelected`, `SkillResolved`, `AttackResolved`, `EnemyDefeated`, etc.)
- Added a browser combat sandbox in the state store and UI to exercise the contract.
- Added first unit tests for active combat flow and a test script (`vitest`) in the webapp package.
- Replaced offline-level placeholder curve with parity progression helpers (`xpToNext` and level carry rules).
- Added save schema migration scaffold for persisted web state (versioned Zustand persistence + legacy normalization path).
- Added shared drop-system module for both chance-based and weighted-table loot paths, including expected-value offline bulk rolls and tertiary chance handling.
- Integrated weighted drop rewards into both offline simulation and active combat kill resolution, with dedicated unit tests.
- Added scheduled skill-impact support in active combat (cast start, scheduled execution, cancellation path, cooldown start events) with queued effect state.
- Added richer auto-skill fallback logic (first eligible auto-enabled skill by readiness/mana/target rules) plus persistence migration normalization for newly introduced combat state fields.
- Added transient modifier lifecycle in active combat (apply, scheduled expiry, deterministic operation ordering semantics scaffold, and expiry event emission).
- Added status runtime lifecycle in active combat (apply/refresh, scheduled tick processing, duration completion expiry, and status event emission).
- Added command/action sequence metadata propagation for combat commands/effects/events and persisted combat-state counter normalization.
- Added auto-skill diagnostics fields (`lastAutoSkillId`, `lastAutoSkillFallbackReason`) for parity-style traceability in runtime state.
- Added deterministic combat replay helper/tests that compare sequence-annotated event streams from fixed command/timestamp scripts.
- Added fixture-driven import coverage for Unity PascalCase legacy account snapshots, translating them into the web save schema.
- Added character-rich PascalCase legacy fixture conversion for inventories, activities, skill progress, and bank stacks.
- Added Unity-compatible tile-pattern resolver tests (anchor/ring/row ordering, same-floor target selection, deterministic actor ID ordering, and caps).
- Added browser content validation/import boundaries and the Unity `IdleCloud/Web/Export Content Snapshot` Editor command.
- Corrected combat action sequence propagation so a cast, its modifier, and its statuses share one action sequence ID.
