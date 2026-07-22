# IdleCloud — Unity Inspector Usability Modernization Pass
## Claude Code Audit and Incremental Refactoring Plan

## 1. Executive recommendation

The objective is not to move all logic out of code. The objective is to ensure that values, references, presets, diagnostics, and safe designer actions that reasonably need iteration are available in Unity's Inspector, while algorithms, invariants, runtime state, and performance-sensitive logic remain in code.

The desired outcome is:

```text
Code owns behaviour and rules.
The Inspector owns safe configuration and tuning.
Editor tooling owns setup, validation, preview, and repetitive authoring tasks.
Runtime systems remain deterministic and protected from invalid configuration.
```

Claude Code should first audit the complete repository and produce a ranked modernization backlog. It should then implement improvements in small batches, beginning with high-value, low-risk systems. It must not mass-convert private fields into public fields or serialize all internal state.

---

## 2. Core problem to solve

IdleCloud has grown through several interconnected systems, including:

- isometric rendering and sorting;
- tile elevation and `floorIndex`;
- navigation, occupancy, and movement;
- procedural or builder-driven content;
- character animation and rendering;
- normal-mapped 2D lighting;
- sun orbit and projected shadows;
- gameplay configuration;
- UI and presentation systems.

Some configuration and tuning decisions may currently live as:

- hard-coded constants;
- private fields with no Inspector exposure;
- values embedded inside update methods;
- scattered magic numbers;
- direct scene lookups;
- implicit prefab assumptions;
- code-only setup sequences;
- unlabelled serialized fields;
- manually repeated prefab configuration;
- systems that provide no validation or Scene-view preview.

This makes iteration slower and increases the chance that only the original author understands how to configure a feature.

The modernization pass should make important systems easier to:

- discover;
- configure;
- preview;
- validate;
- debug;
- reuse;
- reset safely;
- configure through prefabs and ScriptableObjects;
- understand without reading implementation code.

---

## 3. What this initiative is not

This is not permission to:

- serialize every private field;
- make implementation details public;
- expose transient runtime state as editable input;
- replace sound code architecture with Inspector scripting;
- create a custom Editor for every component;
- introduce dozens of ScriptableObjects without a clear ownership model;
- move algorithms into data assets;
- rewrite working systems merely for aesthetic consistency;
- bypass validation because a value is now configurable;
- add reflection-heavy generic tooling;
- introduce runtime `FindObjectOfType` calls to make setup appear automatic;
- break prefab inheritance;
- alter saved scenes and prefabs in bulk without review;
- mix gameplay state with authoring configuration;
- make performance-sensitive code dependent on Editor-only systems.

The project should become easier to operate, not more abstract or more fragile.

---

## 4. Design principles

### 4.1 Expose intent, not implementation

Good Inspector fields represent decisions a designer or developer may legitimately tune:

```text
Movement Speed
Orbit Radius
Shadow Opacity
Sort Order Offset
Interaction Radius
Animation Timing
Minimum Spawn Distance
Debug Visualization
```

Poor Inspector fields expose internal machinery:

```text
_cachedTransform
_lastCalculatedSortKey
_currentPathIndex
_runtimeDictionary
_hasInitialized
_frameAccumulator
```

Internal caches and derived state should normally remain non-serialized and read-only.

### 4.2 Preserve a single source of truth

Do not expose the same effective setting in multiple places.

For each configurable value, determine whether its authoritative owner is:

- component instance;
- prefab;
- shared ScriptableObject;
- project-wide settings asset;
- scene-level coordinator;
- runtime save data.

Avoid situations where a value can be changed in a prefab, a manager, and a ScriptableObject with unclear precedence.

### 4.3 Prefer safe defaults and validation

Every exposed field should have:

- a sensible default;
- a clear label;
- a tooltip where the meaning is not obvious;
- reasonable range/minimum constraints;
- an explicit unit where relevant;
- validation for invalid combinations;
- predictable prefab override behaviour.

### 4.4 Separate configuration from runtime state

Use serialized configuration for authoring inputs.

Keep runtime state non-editable, but expose useful read-only diagnostics through:

- custom Inspector status blocks;
- gizmos;
- debug overlays;
- contextual validation messages;
- read-only serialized/debug fields only where justified.

### 4.5 Keep logic in code

Inspector usability should not compromise code ownership.

For example:

```text
Inspector configures:
- speed
- acceleration
- stopping distance
- debug visualization

Code owns:
- movement algorithm
- collision response
- path execution
- state transitions
- invariants
```

### 4.6 Make authoring actions explicit

Safe, repeatable actions are often better than permanently running editor automation.

Examples:

```text
Auto Assign References
Create Required Child Objects
Validate Configuration
Rebuild Preview
Reset Visual Defaults
Copy Settings From Selected
Bake Cached Data
```

These actions must support Undo, avoid duplicates, and be idempotent.

### 4.7 Modernize incrementally

Each batch should be independently reviewable and reversible.

Do not combine Inspector usability improvements with unrelated gameplay refactors.

---

## 5. Target Inspector quality standard

A production-facing IdleCloud component should normally provide:

```text
[Status / Configuration Health]
[Required References]
[Core Configuration]
[Optional Behaviour]
[Visual Tuning]
[Debug / Preview]
[Safe Actions]
[Read-only Runtime Diagnostics]
```

Not every component needs every section. Small components should remain small.

A user should be able to answer these questions without opening the source file:

1. What does this component do?
2. Which references are required?
3. Which settings are safe to tune?
4. What units and ranges do the values use?
5. What is currently misconfigured?
6. What happens if a value is missing?
7. Can I preview the result in Edit Mode?
8. Is this value shared, prefab-specific, scene-specific, or runtime state?
9. How do I reset this feature to a working baseline?
10. Which values are currently being used at runtime?

---

## 6. Modernization categories

Claude should classify every candidate improvement under one of these categories.

### A. Basic serialized configuration

Use `[SerializeField]` for genuine authoring inputs currently hard-coded in code.

Include:

- clear field names;
- headers or small logical groups;
- tooltips;
- `Min`, `Range`, or validation where appropriate;
- private fields rather than public mutable fields unless API access is required.

### B. Shared configuration assets

Recommend ScriptableObjects only when settings are genuinely shared across multiple instances or scenes.

Strong candidates may include:

- isometric sorting settings;
- lighting profiles;
- movement profiles;
- world-generation profiles;
- item/building definitions;
- animation timing profiles;
- global presentation settings.

Do not create a ScriptableObject merely to move five fields out of a component.

### C. Component validation

Use:

- `OnValidate` for cheap local normalization;
- editor validation methods;
- contextual warnings;
- explicit `Validate Configuration` actions;
- required-reference checks.

Avoid expensive project searches or asset mutation inside `OnValidate`.

### D. Safe setup automation

Add idempotent setup actions for repetitive hierarchy/reference work.

Examples:

- create required visual child;
- assign known sibling components;
- locate scene-level services when unambiguous;
- configure materials;
- establish default anchors;
- populate arrays from explicit child markers.

All asset and hierarchy modifications must support Unity Undo.

### E. Edit Mode preview

Use only when visual iteration materially benefits from it:

- lighting;
- shadow projection;
- ranges;
- spawn areas;
- paths;
- isometric bounds;
- interaction zones;
- sorting anchors;
- camera framing;
- tile footprints.

Preview logic must not run gameplay systems or continuously dirty assets.

### F. Gizmos and handles

Use gizmos for spatial understanding and handles where direct manipulation is safer than numeric entry.

Potential examples:

- foot/sort anchors;
- interaction radii;
- building footprints;
- path nodes;
- camera bounds;
- sun and shadow directions;
- spawn regions;
- tile occupancy;
- elevation indicators.

### G. Read-only runtime diagnostics

Expose useful runtime state without making it editable:

- current state;
- active target;
- resolved floor index;
- resolved sort order;
- current animation/sprite;
- current path status;
- initialization status;
- active configuration asset;
- pooled/active status.

Prefer a custom Inspector status block over editable serialized runtime fields.

### H. Presets and reset actions

Use named presets when a system has recognizable baselines:

```text
Default
Debug
Performance
JohnBrx Shadow Baseline
Day Lighting
Night Lighting
```

A reset must affect only its documented scope and must not clear references unexpectedly.

### I. Purpose-built Editor windows

Use an Editor window only for cross-object or batch workflows where a component Inspector is insufficient.

Potentially justified examples:

- project configuration health dashboard;
- missing-reference scanner;
- lighting/normal-map audit;
- prefab consistency audit;
- batch assignment with preview and explicit confirmation.

Do not create a general "IdleCloud God Tool" that owns unrelated workflows.

---

## 7. Audit methodology

Claude must perform an inspection-only phase before modifications.

### Step 1 — Repository inventory

Identify:

- runtime assemblies;
- Editor assemblies/folders;
- scenes;
- prefabs;
- ScriptableObjects;
- managers/coordinators;
- rendering and sorting systems;
- player and NPC systems;
- tile/building systems;
- input and camera systems;
- lighting and visual effects;
- navigation and occupancy;
- UI systems;
- save/runtime state systems;
- tests and validation tooling.

### Step 2 — Candidate detection

Search for likely usability candidates, including:

```text
numeric/string literals repeated in behaviour code
const values that are actually tuning inputs
private configuration with no serialization
public mutable fields without encapsulation
Find/GetComponent calls used for setup
magic child names
manual hierarchy assumptions
TODO comments about tuning/setup
components with many serialized fields but no grouping/tooltips
OnValidate methods with side effects
ExecuteAlways components
custom editors
context-menu actions
ScriptableObjects with unclear ownership
runtime state serialized unintentionally
```

Claude should not mechanically flag every literal or private field. It must understand context.

### Step 3 — Usage and ownership trace

For every serious candidate, determine:

- where the value originates;
- who reads/writes it;
- whether it is per-instance or shared;
- whether it changes at runtime;
- whether it belongs in save data;
- whether prefab overrides are desirable;
- whether exposing it could violate invariants;
- whether it is performance-sensitive;
- whether it can be validated locally.

### Step 4 — Rank candidates

Use the following priority model:

```text
Impact:
- frequency of tuning
- number of systems/users affected
- current authoring friction
- likelihood of configuration mistakes

Risk:
- gameplay sensitivity
- serialization migration complexity
- prefab/scene blast radius
- performance impact
- dependency on runtime builders

Effort:
- simple field exposure
- validation/custom Inspector
- shared config migration
- broad architecture changes
```

Recommended sequence:

```text
High impact + low risk first
High impact + medium risk second
Low impact or high risk only when justified
```

---

## 8. Required audit deliverables

Before any code changes, Claude must create an audit report containing:

### Executive summary

- current Inspector usability maturity;
- highest-friction areas;
- major architectural risks;
- recommended modernization sequence.

### System inventory

For each meaningful system:

```text
System name
Key files/components
Current configuration owner
Current Inspector usability
Main pain points
Recommended intervention
Risk level
Expected user benefit
```

### Ranked backlog

Each backlog item must include:

```text
ID
System
Problem
Evidence/files
Proposed Inspector/editor improvement
What remains in code
Serialization/prefab impact
Acceptance criteria
Estimated risk
Recommended batch
```

### Explicit “do not expose” list

Identify important fields and systems that should remain code-controlled, including why.

### Shared-settings recommendations

List only justified ScriptableObject/settings-asset candidates and define ownership/precedence.

### Migration risks

Identify:

- renamed serialized fields;
- prefab override risks;
- scene migration;
- builder/runtime overwrites;
- save-data interaction;
- custom editor compatibility;
- domain reload behaviour;
- Edit Mode side effects.

No implementation should begin until this report exists.

---

## 9. Suggested project-wide priority order

Claude must validate this against the actual repository, but the likely order is:

### Batch 1 — Visual and rendering configuration

Likely high-value candidates:

- `SunOrbitController`;
- projected shadows;
- normal-mapped lighting profiles;
- Sprite/Tilemap material validation;
- sorting and foot-anchor diagnostics;
- camera/pixel-perfect settings;
- visual debug toggles.

Reason: visually tunable systems benefit immediately from Inspector controls and Edit Mode preview.

### Batch 2 — Player and character presentation

Candidates:

- movement feel values if currently hard-coded;
- animation timing and directional presentation;
- renderer references;
- sprite/flip synchronization;
- interaction radii;
- character visual offsets;
- status diagnostics.

Keep state-machine logic and movement algorithms in code.

### Batch 3 — Isometric sorting and elevation diagnostics

Candidates:

- shared sort settings asset if not already present;
- read-only resolved sort/floor diagnostics;
- sort-anchor gizmos;
- configuration validation;
- safe debug overlays.

Do not expose arbitrary manual overrides that can bypass sort invariants.

### Batch 4 — Tile, building, and placement authoring

Candidates:

- footprints;
- anchors;
- occupancy dimensions;
- visual offsets;
- placement constraints;
- preview gizmos;
- setup validators;
- definition assets.

Keep occupancy algorithms and authoritative grid rules in code.

### Batch 5 — Navigation and world systems

Candidates:

- debug visualization;
- configurable cost/threshold profiles;
- read-only path status;
- validation and bake/setup actions.

Do not expose low-level runtime collections or allow arbitrary mutation of nav state through the Inspector.

### Batch 6 — UI, audio, and polish

Candidates:

- presentation references;
- timing and easing;
- audio profiles;
- feedback intensity;
- debug states;
- reusable presets.

### Batch 7 — Project health tooling

Only after patterns are proven, consider a small project dashboard for:

- missing required references;
- invalid materials;
- missing `_NormalMap` secondary textures;
- incorrect Sorting Layers;
- duplicate required children;
- invalid shared settings references;
- known prefab inconsistencies.

The dashboard should report first and modify only through explicit, reviewable actions.

---

## 10. Rules for serialized fields

Claude must follow these rules:

1. Prefer private `[SerializeField]` fields over public mutable fields.
2. Preserve public APIs where other systems depend on them.
3. Use `[FormerlySerializedAs]` when renaming serialized fields.
4. Do not serialize caches, derived values, or ephemeral runtime state without justification.
5. Use properties/methods to enforce invariants when runtime mutation is allowed.
6. Clamp or validate values safely.
7. State units in tooltips or labels:

```text
seconds
degrees
Unity units
pixels
cells
tiles
percentage
sorting-order steps
```

8. Avoid ambiguous fields such as `amount`, `value`, `offset2`, or `speedMod`.
9. Keep related fields together.
10. Hide irrelevant fields based on selected modes only when a custom Inspector clearly improves comprehension.
11. Do not use `HideInInspector` to conceal problematic design rather than fixing it.
12. Preserve prefab override semantics.

---

## 11. Rules for ScriptableObjects

A ScriptableObject is justified when:

- multiple objects deliberately share one configuration;
- designers need reusable profiles/presets;
- the setting is project data rather than scene state;
- variants should be represented as assets;
- runtime systems should consume immutable/read-mostly definitions.

A ScriptableObject is not justified solely because:

- a component has many fields;
- the Inspector looks crowded;
- the system might theoretically be reusable;
- it avoids writing a custom Inspector.

Every proposed settings asset must define:

```text
Authoritative owner
Expected lifecycle
Whether runtime mutation is allowed
Whether save data can override it
Fallback/default behaviour
How missing assets are handled
How variants are created
```

Do not create “settings spaghetti” where components, managers, prefabs, and assets all override one another.

---

## 12. Rules for custom Inspectors

Create a custom Inspector only when it adds concrete value:

- mode-dependent fields;
- status/validation display;
- read-only runtime diagnostics;
- safe setup/reset buttons;
- improved grouping for complex components;
- Scene handles or preview integration.

Custom Inspector requirements:

- use `SerializedObject`/`SerializedProperty` for editable serialized data;
- support multi-object editing where practical;
- support Unity Undo;
- preserve prefab overrides;
- avoid direct target mutation except through explicit actions;
- mark assets/scenes dirty only when changed;
- avoid expensive work in every Inspector repaint;
- keep Editor code in Editor-only assemblies/folders;
- fail gracefully when references are missing.

Do not duplicate all default Inspector rendering manually without a reason.

---

## 13. Rules for auto-setup and repair actions

All setup actions must be:

- explicit;
- idempotent;
- undoable;
- non-destructive by default;
- safe for prefab assets and instances;
- clear about what they will change;
- resistant to duplicate child creation;
- unwilling to overwrite valid custom references silently.

Examples:

```text
Auto Setup
Validate
Repair Missing References
Create Required Child
Apply Default Material
Refresh Preview
Reset Visual Values
```

Mass/batch repair must first provide a dry-run report and require an explicit action.

---

## 14. Rules for Edit Mode execution

`[ExecuteAlways]` and `OnValidate` require discipline.

Allowed:

- cheap visual previews;
- local transform updates;
- local reference validation;
- gizmo/handle preparation;
- clamping serialized values.

Avoid:

- gameplay state transitions;
- save writes;
- scene-wide searches every frame;
- asset creation/deletion automatically;
- repeated material instantiation;
- prefab mutation;
- continuous scene dirtiness;
- network, input, or runtime service calls.

Every Edit Mode preview must have an off switch if it performs ongoing updates.

---

## 15. Runtime diagnostics policy

Useful diagnostics should be visible but not editable.

Potential diagnostics:

```text
Initialized
Current State
Resolved Floor Index
Resolved Sorting Order
Current Sprite
Current Target
Current Path Length
Current Light/Shadow Direction
Active Profile
Last Validation Result
```

Do not serialize these merely to show them unless necessary. Prefer a custom Inspector or debug view.

Debug logging must be disabled by default and avoid per-frame spam.

---

## 16. Testing and validation strategy

Every implementation batch must include:

### Serialization safety

- existing scenes open without missing scripts;
- prefabs retain field values;
- renamed fields preserve data;
- overrides remain understandable;
- no unexpected asset modifications.

### Runtime regressions

- gameplay behaviour remains unchanged unless explicitly intended;
- performance-sensitive loops remain allocation-free;
- initialization order remains valid;
- pooled/disabled objects behave correctly;
- builds exclude Editor-only code.

### Inspector usability

- fields are understandable without source-code inspection;
- defaults are usable;
- invalid configurations display useful warnings;
- setup actions are idempotent;
- Undo works;
- prefab overrides work;
- reset actions affect only their stated scope;
- Edit Mode previews do not dirty scenes continuously.

### Code quality

- algorithms remain in runtime code;
- fields are not exposed unnecessarily;
- no duplicate sources of truth are created;
- no reflection-heavy generic framework is introduced;
- documentation names the authoritative owner of shared settings.

---

## 17. Batch delivery requirements

For each implementation batch, Claude must provide:

1. Batch objective.
2. Files inspected.
3. Files created.
4. Files modified.
5. Prefabs/scenes/assets modified.
6. Fields newly serialized.
7. Fields deliberately kept code-only.
8. Custom Inspector/editor tools added.
9. Setup/reset/validation actions added.
10. Migration notes.
11. Tests and checks performed.
12. Known limitations.
13. Rollback instructions.
14. Suggested manual Unity validation.

Do not deliver multiple unrelated batches in one unreviewable change set.

---

## 18. Acceptance criteria for the full initiative

The modernization initiative is successful when:

- [ ] High-frequency tuning no longer requires editing source code.
- [ ] Important visual systems can be previewed in the Scene view.
- [ ] Required references are obvious and validated.
- [ ] Repetitive setup has safe, idempotent actions.
- [ ] Shared configuration has an explicit owner.
- [ ] Runtime state is visible where useful but not accidentally editable.
- [ ] Prefab overrides remain understandable.
- [ ] Sorting, elevation, movement, navigation, and occupancy invariants remain code-controlled.
- [ ] Existing scenes and prefabs retain their configuration.
- [ ] No broad public-field conversion occurred.
- [ ] No Inspector-only architecture replaced sound runtime code.
- [ ] Editor tooling supports Undo and does not continuously dirty assets.
- [ ] The project contains concise documentation for the new authoring workflows.
- [ ] Each modified system has a known-good reset/default state.
- [ ] The changes are incremental and reversible.

---

## 19. Stop conditions

Claude must stop and report before proceeding if:

- a change requires guessing configuration ownership;
- exposing a value would bypass a critical invariant;
- serialized-field migration risks losing prefab/scene values;
- a runtime builder overwrites Inspector configuration;
- a proposed ScriptableObject creates unclear precedence;
- an Editor tool would need broad automatic asset mutation;
- Edit Mode execution causes persistent asset dirtiness;
- a batch would mix unrelated architecture changes;
- a change would alter save-data compatibility;
- project-wide prefab edits cannot be reliably verified;
- the requested usability gain is too small relative to the risk.

---

# Ready-to-use Claude Code prompt

```markdown
# Task: Audit IdleCloud and create an incremental Unity Inspector usability modernization roadmap

Work inside the existing IdleCloud Unity project.

## Goal

I want to make the project substantially easier to configure, tune, preview, validate, and debug through the Unity Editor. I believe too many legitimate authoring and tuning decisions currently live only in code.

This is not a request to move all logic into the Inspector or serialize every private field.

Use this model:

```text
Code owns behaviour, algorithms, invariants, and runtime state.
Inspector/prefabs own safe per-instance configuration and tuning.
ScriptableObjects own genuinely shared reusable configuration.
Editor tooling owns setup, validation, preview, and repetitive authoring actions.
```

## Critical first instruction

Perform an inspection-only audit first. Do not modify production files, scenes, prefabs, or assets until you have produced the complete audit and ranked backlog described below.

## Existing important context

IdleCloud is a 2.5D isometric Unity project with custom rendering/sorting, tile elevation, `floorIndex`, navigation/occupancy, character animation, URP 2D normal-mapped lighting, an orbiting Spot Light 2D, and projected-shadow work.

Known working visual configuration includes:

- URP 2D Renderer;
- `_NormalMap` sprite secondary textures;
- `Sprite-Lit-Default` for lit sprites/tilemaps;
- renderer Sorting Layer `WorldLit`;
- Spot Light 2D normal-map lighting;
- `SunOrbitController`.

Do not destabilize these systems.

## Audit scope

Inventory and inspect:

- runtime and Editor assemblies;
- scenes and prefabs;
- ScriptableObjects/settings assets;
- player/NPC systems;
- rendering, sorting, elevation, and floorIndex;
- tiles, buildings, builders, placement, and occupancy;
- movement and navigation;
- animation and presentation;
- lighting, shadows, camera, and visual effects;
- input and interactions;
- UI/audio/polish;
- save/runtime state;
- tests, validators, custom Inspectors, gizmos, and Editor windows.

Search for genuine authoring friction, including:

- hard-coded tuning constants;
- repeated magic numbers;
- private configuration with no Inspector access;
- public mutable fields lacking protection;
- implicit hierarchy/reference assumptions;
- setup performed manually or through child-name lookups;
- components with poorly organized serialized fields;
- visual systems with no Edit Mode preview;
- missing validation;
- repetitive prefab setup;
- runtime state serialized unintentionally;
- shared settings with unclear ownership;
- custom Editors or OnValidate/ExecuteAlways logic with unsafe side effects.

Do not mechanically classify every literal or private field as a problem. Trace usage and understand intent.

## For each candidate, determine

- authoritative owner;
- per-instance vs shared scope;
- whether it is authoring configuration, runtime state, derived state, cache, or invariant;
- read/write call sites;
- runtime mutation requirements;
- save-data interaction;
- prefab override desirability;
- validation rules;
- serialization/migration risk;
- performance sensitivity;
- expected usability benefit.

## Required audit report

Produce a Markdown report containing:

### 1. Executive summary

- current Inspector usability maturity;
- highest-friction systems;
- important risks;
- recommended modernization sequence.

### 2. System inventory

For each major system, identify:

```text
System
Key files/components
Current configuration owner
Current Inspector usability
Pain points
Recommended intervention
Risk
Expected benefit
```

### 3. Ranked backlog

Each item must contain:

```text
ID
System
Problem and evidence
Files involved
Proposed Inspector/editor improvement
What must remain in code
Serialization/prefab impact
Acceptance criteria
Risk
Recommended batch
```

Rank high-impact/low-risk items first.

### 4. Explicit do-not-expose list

Identify fields/state/invariants that should remain code-controlled and explain why.

### 5. Shared-settings recommendations

Recommend ScriptableObjects only where configuration is genuinely shared or profile-based. Define authority, lifecycle, runtime mutation, fallbacks, save-data interaction, and precedence.

### 6. Migration risks

Identify renamed-field preservation, FormerlySerializedAs needs, prefab overrides, scene migration, runtime builders, save compatibility, domain reload, custom Editor compatibility, and Edit Mode side effects.

### 7. Proposed batches

Create small independently reviewable batches. Start with high-value, low-risk systems. Do not combine unrelated refactors.

## Inspector design standard

For production-facing components, use only the sections that are appropriate:

```text
Status / Configuration Health
Required References
Core Configuration
Optional Behaviour
Visual Tuning
Debug / Preview
Safe Actions
Read-only Runtime Diagnostics
```

Use:

- private SerializeField fields rather than broad public mutability;
- clear names and tooltips;
- units in labels/tooltips;
- Min/Range and validation;
- enums for modes;
- colour/reference fields;
- read-only diagnostics through custom Inspectors where useful;
- Scene gizmos/handles for spatial values;
- safe presets/reset actions;
- Edit Mode previews only where they materially help.

## Setup and Editor actions

Where justified, provide actions such as:

```text
Auto Assign References
Create Required Child
Validate Configuration
Repair Missing References
Refresh Preview
Reset Visual Values
Apply Known-Good Baseline
```

Every action must:

- support Unity Undo;
- be idempotent;
- avoid duplicate children;
- avoid silently overwriting valid references;
- avoid automatic mass asset mutation;
- preserve prefab overrides;
- dirty assets only when explicitly changed.

## ScriptableObject rules

Do not create settings assets merely because a component has many fields. Use them only for genuinely shared/profile-based configuration. Avoid multiple sources of truth and unclear override precedence.

## Edit Mode rules

ExecuteAlways/OnValidate must not run gameplay systems, perform repeated project searches, create assets/materials, write saves, mutate prefabs, or continuously dirty scenes. Any ongoing preview must have an off switch.

## Non-goals

Do not:

- serialize every private field;
- expose caches or derived state as editable controls;
- replace algorithms with Inspector configuration;
- rewrite working architecture for aesthetic consistency;
- create a custom Inspector for every component;
- create one giant cross-project Editor window;
- add reflection-heavy generic frameworks;
- introduce runtime scene searches as an authoring shortcut;
- change sorting, floorIndex, navigation, occupancy, movement, or save semantics during the audit.

## Implementation rule after the audit

Do not implement the whole backlog in one pass.

After producing the audit, select only the recommended first batch and provide:

- exact proposed change set;
- files/assets affected;
- serialized-field migration plan;
- Unity validation plan;
- rollback plan.

Wait at that natural review gate before implementing later batches. Do not ask for confirmation between individual files inside an approved batch.

## Delivery quality

Be explicit about uncertainty. If you cannot safely determine ownership, serialization impact, or prefab behaviour, stop and report it rather than guessing.
```

---

## 20. Recommended operating model after the audit

Use the audit as a backlog, not as one giant prompt execution.

Recommended workflow:

```text
1. Claude produces repository-wide audit only.
2. Review the ranked backlog.
3. Claude implements Batch 1.
4. Open Unity and test Inspector workflow and regressions.
5. Commit the batch.
6. Continue to the next batch.
```

A good initial batch should usually contain no more than two or three closely related systems.

Example:

```text
Batch 1A — Lighting authoring
- SunOrbitController Inspector cleanup
- projected-shadow Inspector
- lighting validation and presets

Batch 1B — Character visual authoring
- renderer/reference setup
- foot-anchor diagnostics
- animation presentation tuning
```

This keeps the pass useful, reviewer-friendly, and reversible.
