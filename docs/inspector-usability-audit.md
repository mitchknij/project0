# IdleCloud — Unity Inspector Usability Audit

Inspection-only audit performed per `IdleCloud_Unity_Inspector_Usability_Modernization_Plan.md`.
No production files were modified to produce this report. All file:line references below reflect
the state of the repository at the time of the audit (2026-07-11).

## 1. Executive summary

**Current maturity:** Mixed. The isometric sorting subsystem (`Assets/Iso/Sorting/`) and
`SunOrbitController.cs` already demonstrate the target quality bar — private `[SerializeField]`
fields, tooltips, `[Range]`/`[Min]` constraints, `OnValidate`, gizmos, and read-only runtime
diagnostics. Everything else (player/character movement, navigation, camera, scene bootstrap, and
the entire procedurally-built UI layer) exposes little beyond bare public fields with `[Header]`
grouping, no tooltips, no range constraints, and no Edit Mode preview.

**Highest-friction areas:**
1. Player/character/navigation scripts (`PlayerController`, `EnemyController`,
   `SpriteSheetAnimator`, `CameraFollow`, `GridPathfinder`, `ElevationSorter`) — every tunable is a
   bare public field with no tooltip, no unit, no range, and inconsistent styling relative to the
   one `[SerializeField]` field that does exist (`PlayerController.startHeight`).
2. `SceneBootstrap.cs` — relies on magic scene-object-name string lookups
   (`GameObject.Find("Grass 1")`, `transform.Find("Spawn")`/`"Enemy A"`) with no Inspector fallback
   references, and hardcodes fallback-enemy spawn count/jitter in code.
3. The UI layer (`UIBuilder.cs`, `UIHelpers.cs`, `UITheme.cs`) is a static, code-generated canvas
   with dozens of layout/color magic numbers. This is a legitimate architectural choice (baked UI
   via `UIBakeTool`, per existing project memory), not a bug — but it means there is currently no
   Inspector or Edit Mode surface at all for iterating on UI layout without a Play-mode/bake cycle.

**Major architectural risks:** None found that block incremental Inspector work. The one structural
oddity is that `Assets/Iso/Sorting/*` has no dedicated asmdef and compiles into the implicit
`Assembly-CSharp`, which is out of scope for this initiative but worth flagging for a future pass.

**Recommended modernization sequence:** Follow the source plan's batch order, adjusted for what
this audit actually found in the repo:
1. Batch 1 — Player/character movement & presentation (this audit's first implementation batch).
2. Batch 2 — Isometric sorting/elevation diagnostics polish (small gaps only; most of this system
   is already good — see below).
3. Batch 3 — `SceneBootstrap.cs` scene-lookup hardening (higher risk, deferred; touches scene wiring).
4. Batch 4 (optional, larger) — `UILayoutSettings` ScriptableObject for the procedural UI builder,
   only if UI layout iteration friction becomes a real pain point; not justified as urgent by this
   audit alone.

## 2. System inventory

| System | Key files | Config owner today | Inspector usability | Pain points | Recommended intervention | Risk | Benefit |
|---|---|---|---|---|---|---|---|
| Isometric terrain sorting | `Assets/Iso/Sorting/*.cs` | `IsoSortSettings` SO + `[SerializeField]` on components | High (reference-quality) | `TerrainVisualBuilder` fields lack tooltips; rebuild only works in Play mode (no `[ExecuteAlways]`) | Add tooltips; consider Edit Mode rebuild trigger | Low | Small |
| Sun/lighting | `Assets/Scripts/View/SunOrbitController.cs` | `[SerializeField]` on component | High (reference-quality) | None significant | None needed | — | — |
| Player movement | `Assets/Scripts/View/PlayerController.cs` | bare `public` fields | Low | No tooltips/Min/Range; `ArrivalThreshold` const not exposed | Batch 1 | Low | High (frequently tuned) |
| Character animation | `Assets/Scripts/View/SpriteSheetAnimator.cs` | bare `public` fields | Low | No tooltip; `fps` unclamped (could be 0/negative) | Batch 1 | Low | Medium |
| Enemy/NPC wander | `Assets/Scripts/View/EnemyController.cs` | bare `public` fields | Low | No tooltips/ranges; no gizmo for `wanderRadius` | Batch 1 | Low | Medium |
| Camera follow | `Assets/Scripts/View/CameraFollow.cs` | bare `public` fields | Low | No tooltips/Range on `smoothTime` | Batch 1 | Low | Medium |
| Elevation-based sort fallback | `Assets/Scripts/View/ElevationSorter.cs` | `public Transform anchor` (no `[SerializeField]` marker) | Low | Silent no-op when grid lookup fails (no warning, unlike `GridPathfinder`) | Batch 1 | Low | Low-Medium |
| Pathfinding | `Assets/Scripts/View/GridPathfinder.cs` | mostly-tooltipped public fields | Medium | `maxIterations` missing tooltip; algorithm constants (1.414f, 0.586f) correctly left in code | Batch 1 (tooltip only) | Low | Low |
| Scene bootstrap | `Assets/Scripts/View/SceneBootstrap.cs` | bare `public` fields + magic string scene lookups | Low | `GameObject.Find`/`transform.Find` string dependencies; hardcoded fallback-spawn magic numbers | Deferred batch (higher risk — scene wiring) | Medium | Medium |
| UI panels | `Assets/Scripts/UI/*.cs` | `[HideInInspector] public` fields wired by `UIBuilder` | N/A by design | Fields intentionally hidden since `UIBuilder` wires them at runtime; this is correct, not a gap | None | — | — |
| Procedural UI layout | `UIBuilder.cs`, `UIHelpers.cs`, `UITheme.cs` | compile-time literals in static classes | None (no MonoBehaviour instance to attach an Inspector to) | Dozens of magic-number layout/color literals; no Edit Mode preview of layout changes | Optional future `UILayoutSettings` ScriptableObject batch | Medium (broad, no clear component owner) | Medium |
| Managers (`GameManager`, `SaveManager`) | `Assets/Scripts/Managers/*.cs` | private consts, no `[SerializeField]` | Appropriate — pure logic/state, not authoring config | None | None | — | — |

## 3. Ranked backlog

### B1 — PlayerController tuning exposure
- **Problem/evidence:** `speed`, `idleFrames`/`walkFrames`, `idleFps`/`walkFps` are bare `public`
  with no tooltip/Range/Min (`Assets/Scripts/View/PlayerController.cs`); `ArrivalThreshold = 0.08f`
  is a private const that is a genuine tuning value never exposed (line 42).
- **Proposed improvement:** Add `[Header]/[Tooltip]/[Min]`; promote `ArrivalThreshold` to a
  tooltipped `[SerializeField, Min(0)] private float arrivalThreshold = 0.08f`.
- **What remains in code:** Movement algorithm, animation-trigger epsilon (`sqrMagnitude > 0.01f`),
  pathfinder-driven waypoint traversal.
- **Serialization/prefab impact:** None — no field renamed or reparented; `arrivalThreshold` is a
  new field with the same default as the old const, so existing prefabs get the default value.
- **Acceptance criteria:** Fields show tooltips/ranges in Inspector; default runtime behavior
  unchanged; compiles clean.
- **Risk:** Low. **Batch:** 1.

### B2 — SpriteSheetAnimator validation
- **Problem/evidence:** `frames`, `fps` public with no tooltip; `fps` can be set to 0 or negative
  with no guard (`Assets/Scripts/View/SpriteSheetAnimator.cs`).
- **Proposed improvement:** Add `[Tooltip]`, `[Min(1)]` on `fps`.
- **What remains in code:** Frame-advance timing logic.
- **Serialization/prefab impact:** None.
- **Acceptance criteria:** Cannot set `fps` below 1 via Inspector; existing values unaffected.
- **Risk:** Low. **Batch:** 1.

### B3 — EnemyController tuning + wander gizmo
- **Problem/evidence:** `wanderRadius`, `moveSpeed`, `idleDuration`, `walkDuration` public with
  `[Header]` only, no tooltips/ranges; no Scene-view visualization of the wander area
  (`Assets/Scripts/View/EnemyController.cs`).
- **Proposed improvement:** Add tooltips/`[Min]`/`[Range]`; add `OnDrawGizmosSelected` drawing a
  wire circle of radius `wanderRadius` around the enemy's spawn/current position.
- **What remains in code:** Wander state machine, jitter epsilon (`sqrMagnitude < 0.01f`).
- **Serialization/prefab impact:** None (gizmo is editor-only, additive).
- **Acceptance criteria:** Selecting an enemy in Scene view shows its wander radius; no behavior
  change in Play mode.
- **Risk:** Low. **Batch:** 1.

### B4 — CameraFollow tuning exposure
- **Problem/evidence:** `target`, `smoothTime`, `offset` bare public, no tooltip/Range
  (`Assets/Scripts/View/CameraFollow.cs`).
- **Proposed improvement:** Add tooltips; `[Range(0.01f, 2f)]` on `smoothTime` (0 would divide by
  zero / freeze the follow damp in `Vector3.SmoothDamp`).
- **What remains in code:** Smoothing/follow algorithm.
- **Serialization/prefab impact:** None.
- **Risk:** Low. **Batch:** 1.

### B5 — ElevationSorter validation + field marking
- **Problem/evidence:** `anchor` is `public Transform` without `[SerializeField]`; `Awake()`
  silently no-ops in `LateUpdate` if the grid lookup fails, unlike `GridPathfinder` which logs a
  warning (`Assets/Scripts/View/ElevationSorter.cs` lines 14, 26-30).
- **Proposed improvement:** Mark `anchor` `[SerializeField]` with a refined tooltip; add
  `Debug.LogWarning` on failed grid lookup, matching `GridPathfinder`'s existing pattern.
- **What remains in code:** Sort-order resolution logic.
- **Serialization/prefab impact:** None (field name/type unchanged).
- **Risk:** Low. **Batch:** 1.

### B6 — GridPathfinder tooltip gap
- **Problem/evidence:** `maxIterations` is the one public tunable missing a `[Tooltip]`
  (`Assets/Scripts/View/GridPathfinder.cs` line 20); other fields already documented.
- **Proposed improvement:** Add the missing tooltip only.
- **What remains in code:** All pathfinding algorithm internals, including `1.414f`/`0.586f`
  constants (see do-not-expose list).
- **Risk:** Low. **Batch:** 1.

### B7 — SceneBootstrap field tooltips
- **Problem/evidence:** `mapRoot`, `player`, `enemyIdleFrames`/`enemyWalkFrames`, `cameraFollow`
  public with `[Header]` only, no tooltips (`Assets/Scripts/View/SceneBootstrap.cs`).
- **Proposed improvement:** Add tooltips only in this batch (the `GameObject.Find`/`transform.Find`
  string-lookup issue is tracked separately as B8, deferred).
- **Risk:** Low. **Batch:** 1.

### B8 — SceneBootstrap scene-lookup hardening (deferred)
- **Problem/evidence:** `GameObject.Find("Grass 1")` (line 27), `mapRoot.Find("Spawn")` /
  `Find("Enemy A")` (lines 77, 85) are magic-string scene-object lookups with no Inspector
  fallback fields; hardcoded fallback-enemy spawn count (`3`) and jitter
  (`Random.Range(-2f,2f)/(-1f,1f)`) at lines 93-95.
- **Proposed improvement:** Add `[SerializeField]` fallback reference fields (e.g. `grassRoot`,
  `spawnPoint`, `enemySpawnPoints[]`) that are used when assigned, falling back to the current
  `Find`-based lookup only if unassigned — preserves current behavior for existing scenes while
  giving new scenes an explicit, discoverable wiring path.
- **What remains in code:** Bootstrap sequencing/order logic.
- **Serialization/prefab impact:** New optional fields only — additive, no risk to existing scene
  data. The `Find`-based fallback path is unchanged, so behavior is identical until someone
  explicitly wires the new fields.
- **Risk:** Medium (touches scene bootstrap sequencing, needs manual Play-mode verification in the
  Unity Editor since this session cannot run the Editor). **Batch:** 3 (deferred, not in Batch 1).

### B9 — TerrainVisualBuilder tooltips + Edit Mode rebuild (optional)
- **Problem/evidence:** `[SerializeField]` fields (`grid`, `blockVisualPrefab`, `sortSettings`,
  `visualRoot`, `disableSourceTilemapRenderers`, `debugTerrainSorting`) lack tooltips
  (`Assets/Iso/Sorting/TerrainVisualBuilder.cs` lines 11-16); `[ContextMenu("Rebuild Terrain
  Visuals")]` (line 35) only works in Play mode since the class has no `[ExecuteAlways]`.
- **Proposed improvement:** Add tooltips (low risk). Adding true Edit Mode rebuild support is a
  larger change (needs to guard against continuous scene dirtying per the source plan's Edit Mode
  rules) — recommend tooltips only for Batch 2, defer `[ExecuteAlways]` rebuild to a future batch
  if a real workflow need emerges.
- **Risk:** Low (tooltips) / Medium (Edit Mode rebuild, deferred). **Batch:** 2.

### B10 — UILayoutSettings ScriptableObject (optional, not currently justified)
- **Problem/evidence:** `UIBuilder.cs`/`UIHelpers.cs`/`UITheme.cs` contain dozens of magic-number
  layout/color literals with no Inspector or Edit Mode surface.
- **Proposed improvement:** A `UILayoutSettings`/`UITheme` ScriptableObject *could* centralize these,
  but per source-plan section 11 rules, a ScriptableObject is only justified when multiple objects
  share config or designers need reusable profiles/presets — here there is exactly one static
  builder consumed once at boot, so this is not currently justified. Listed for completeness; not
  scheduled into any batch unless UI-layout iteration becomes an active pain point.
- **Risk:** Medium-High (broad, touches the entire UI bake pipeline; would need to coordinate with
  `UIBakeTool` per existing project memory). **Batch:** none scheduled.

## 4. Explicit "do not expose" list

| Item | File:line | Why it stays code-controlled |
|---|---|---|
| Diagonal-cost constant `1.414f` | `GridPathfinder.cs:180,203` | √2 — a pathfinding-algorithm invariant, not a tuning input. |
| Octile heuristic tie-break `0.586f` | `GridPathfinder.cs:249` | Algorithm correction term; exposing it could produce inadmissible heuristics and break pathfinding correctness. |
| Movement-detection epsilon `sqrMagnitude > 0.01f` | `PlayerController.cs`, `EnemyController.cs` | Jitter-avoidance threshold for animation state, not a designer-facing value; too small a magnitude to be meaningfully "tunable." |
| Sort-order tie-breaker formula | `IsoTerrainSortCalculator.cs:43` | Sorting invariant — the source plan explicitly forbids exposing values that could bypass sort invariants (Batch 3 rules, section 9). |
| UI layout literals in `UIBuilder`/`UIHelpers`/`UITheme` | throughout | Static, code-generated UI baked once via `UIBakeTool`; no per-instance/shared-profile need identified (see B10). |
| `GameManager`/`SaveManager` consts (`HeartbeatIntervalSec`, `SaveFileName`) | `GameManager.cs:74`, `SaveManager.cs:49` | Pure system-level constants, not per-instance/per-scene authoring config; changing them has save-compatibility and multiplayer-heartbeat implications outside "safe tuning." |

## 5. Shared-settings recommendations

Only one ScriptableObject is currently justified and it already exists: `IsoSortSettings`
(`Assets/Iso/Sorting/IsoSortSettings.cs`) — shared sort-scale/elevation-weight/offset config
consumed by both `PlayerSortController` and `TerrainVisualBuilder`. Its ownership is already clear
(one asset referenced by both consumers) and needs no change.

No new ScriptableObject is recommended by this audit. `UILayoutSettings` (B10) was considered and
explicitly not recommended — see B10 above.

## 6. Migration risks

- **Renamed fields:** None in Batch 1 — no field is renamed, so no `[FormerlySerializedAs]` is
  needed anywhere in this batch.
- **New fields:** `PlayerController.arrivalThreshold` is a new serialized field replacing a private
  const of the same default value — existing prefabs/instances will pick up the default (0.08)
  identically to today's compiled-in behavior; no data loss possible since there was no prior
  serialized value to preserve.
- **Prefab overrides:** Unaffected — no field access modifiers change in Batch 1 (see constraint
  section below), so no field's presence in the serialized prefab data changes.
- **Scene migration:** None in Batch 1. B8 (deferred) will need scene-by-scene manual verification
  when implemented.
- **Runtime builder overwrites:** `SceneBootstrap.cs` assigns several of the fields touched in
  Batch 1 (`player.pathfinder`, `cameraFollow.target`, `anim.fps`, `ctrl.idleFrames`,
  `ctrl.walkFrames`) directly from code at runtime — this is why those fields must stay `public`
  rather than become private `[SerializeField]` in this batch (see constraint below). Adding
  attributes does not change this runtime-assignment behavior.
- **Save-data interaction:** None of the Batch 1 fields are part of `SaveManager`'s persisted state.
- **Domain reload / Edit Mode side effects:** None — no `[ExecuteAlways]` is added in Batch 1; the
  one new gizmo (`EnemyController.OnDrawGizmosSelected`) only draws, never mutates state or dirties
  scenes.

**Constraint governing Batch 1's approach:** A grep across `Assets/**/*.cs` for external references
to the candidate fields found exactly one file — `Assets/Scripts/View/SceneBootstrap.cs` — reading
or writing `PlayerController.pathfinder`, `CameraFollow.target`, `SpriteSheetAnimator.fps`,
`EnemyController.idleFrames`/`walkFrames` from outside their declaring class. Because Unity's
serializer keys data by field name regardless of access modifier, and because these fields need
external write access from `SceneBootstrap`, Batch 1 adds attributes to the existing `public`
fields rather than converting them to private `[SerializeField]` (which would require introducing
public properties or `[System.NonSerialized]` internal setters — a larger, unnecessary change for
this batch's goal of tooltips/ranges/validation).

## 7. Proposed batches

1. **Batch 1 — Player & character movement/presentation Inspector cleanup** (B1-B7): tooltips,
   `[Min]`/`[Range]` constraints, one promoted const, one new gizmo, one new warning log. Zero
   behavior change, zero serialization risk. Implemented immediately following this audit.
2. **Batch 2 — Isometric sorting/elevation diagnostics polish** (B9): tooltips on
   `TerrainVisualBuilder` fields. Small, low-risk follow-on since most of this system is already
   reference-quality.
3. **Batch 3 — SceneBootstrap scene-lookup hardening** (B8): optional `[SerializeField]` fallback
   references alongside existing `Find`-based lookups. Medium risk — needs manual Unity Editor
   verification before merging, since this session cannot open the Editor.
4. **Batch 4 (not scheduled)** — `UILayoutSettings` ScriptableObject (B10), only if UI-layout
   iteration friction becomes an active problem. Not recommended as part of this initiative's
   current scope.

Per the user's instruction, Batches 1-3 will be executed sequentially without pausing for a
mid-stream review gate; each is still delivered as its own commit with full batch-delivery notes
(source plan section 17) so the history stays reviewable and revertible batch-by-batch.
