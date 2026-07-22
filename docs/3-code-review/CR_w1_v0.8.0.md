# Code Review: Drops & Loot Pickup (Ground Loot Flow)

**Review Date**: 2026-07-19  
**Version**: `0.8.0`  
**Files Reviewed**:

- `Assets/Art/UI/Generated/Fonts/PressStart2P SDF.asset`
- `Assets/Art/UI/Generated/Fonts/VT323 SDF.asset`
- `Assets/Prefabs/UI/GameUI.prefab`
- `Assets/Scenes/Maps/FirstMap.unity`
- `Assets/Scenes/PersistentGame.unity`
- `Assets/Scripts/Core/DropSystem.cs`
- `Assets/Scripts/Core/Offline.cs`
- `Assets/Scripts/Data/State/GameTypes.cs`
- `Assets/Scripts/Managers/Content/ContentRegistryProvider.cs`
- `Assets/Scripts/Managers/Content/ContentValidator.cs`
- `Assets/Scripts/Managers/Content/DropTableDefinitionAsset.cs`
- `Assets/Scripts/Managers/GameManager.cs`
- `Assets/Scripts/Managers/Gameplay/ActiveCombatCoordinator.cs`
- `Assets/Scripts/Managers/Gameplay/ActiveGatheringCoordinator.cs`
- `Assets/Scripts/Managers/Gameplay/GameSession.cs`
- `Assets/Scripts/Managers/RuntimeDebugView.cs`
- `Assets/Scripts/Tests/EditMode/ActiveCombatCoordinatorTests.cs`
- `Assets/Scripts/Tests/EditMode/ContentValidationTests.cs`
- `Assets/Scripts/Tests/EditMode/DropAndActivityTests.cs`
- `Assets/Scripts/Tests/EditMode/LifeSkillsTests.cs`
- `Assets/Scripts/Tests/EditMode/ProgressionAndOfflineTests.cs`
- `Assets/Scripts/UI/MainHudPanel.cs`
- `Assets/Scripts/UI/OfflineReportPanel.cs`
- `Assets/Scripts/UI/UIBuilder.cs`
- `Assets/Scripts/UI/UITheme.cs`
- `Assets/Scripts/View/CombatFeedbackView.cs`
- `Assets/Scripts/View/CombatView.cs`
- `Assets/Scripts/View/PlayerController.cs`
- `docs/STATE.md`
- `docs/guardrails/PROJECT.md`
- `plan-the-autocombat-loop-fizzy-muffin.md`

**Plan**: `docs/1-plans/F_0.8.0_drops-loot-pickup.plan.md`

---

## Executive Summary

The change implements physical ground loot, manual and automatic pickup, inventory-first reward routing, tertiary drop tables, pickup feedback, and runtime loot lifecycle management. Four major issues were found during review: a destroyed-target intent leak, silent loss when spawn coordinates could not be resolved, missing AutoLoot overflow warnings, and missing destination-regression tests.

All four findings were addressed. The reported build completes with zero errors and warnings, all affected EditMode classes pass, and the PlayMode smoke suite is green. Requester-owned prefab, content-asset, UI-rebake, and feature verification remain intentionally deferred to Phase 4.

APPROVED

---

## Changes Overview

Core and Data add tertiary drop authoring, stochastic rolls, expected-value parity, validation, and offline inventory-first deposits. Active combat now returns per-defeated-actor loot without depositing items directly, while gathering consistently deposits into character inventory.

`LootDropManager` owns runtime ground-loot records, partial pickup, despawn, auto-vacuum, and map clearing. `GameManager` owns account commits and lifecycle-event relays. View code renders and sorts loot bags, paths the player to clicked bags, and provides pickup popups. UI code adds a loot feed and a distinct AutoLoot toggle.

---

## Findings

### Critical Issues

None.

### Major Issues

#### Destroyed loot targets could leave combat paused

- **Original locations**: `Assets/Scripts/View/PlayerController.cs:149`, `Assets/Scripts/View/CombatView.cs:102`
- **Description**: Auto-vacuum or expiry could destroy a targeted bag before `PlayerController` emitted a reached or cancelled event. Unity’s destroyed-object null semantics then left `CombatView._lootIntentActive` set, suspending combat until another explicit action.
- **Disposition**: **Addressed.** `ReferenceEquals` now detects destroyed managed references during invalidation, cancellation, and arrival at `Assets/Scripts/View/PlayerController.cs:151`, `Assets/Scripts/View/PlayerController.cs:244`, and `Assets/Scripts/View/PlayerController.cs:323`. `CombatView` releases the gate before attempting the safe no-op pickup at `Assets/Scripts/View/CombatView.cs:424-426`.

#### Valid kills could silently discard rolled loot

- **Original location**: `Assets/Scripts/View/CombatView.cs:322`
- **Description**: Loot spawning skipped a reward when the defeated actor was absent from `_targets` or the pathfinder was unavailable. Because active combat no longer deposits item rewards directly, the rolled loot had no recovery path.
- **Disposition**: **Addressed.** Targets accepted through `BeginMobEngagement` now fall back to the current target, and missing actors use the player position at `Assets/Scripts/View/CombatView.cs:323-330`. The pathfinder is re-resolved at `Assets/Scripts/View/CombatView.cs:326`; residual physically unresolvable cases produce a one-time warning at `Assets/Scripts/View/CombatView.cs:330-337`.

#### Auto-vacuum suppressed inventory-full warnings

- **Original locations**: `Assets/Scripts/Managers/GameManager.cs:622`, `Assets/Scripts/Managers/GameManager.cs:633`
- **Description**: Automatic pickup attempts suppressed `LootPickupAttempted`, so partial or failed vacuum attempts left loot on the ground without the required HUD warning.
- **Disposition**: **Addressed.** Failed or partial vacuum attempts publish once per drop at `Assets/Scripts/Managers/GameManager.cs:637-641`. Tracking is removed after full pickup at `Assets/Scripts/Managers/GameManager.cs:626-627`, map clearing at `Assets/Scripts/Managers/GameManager.cs:652-657`, and expiry at `Assets/Scripts/Managers/GameManager.cs:670-673`.

#### Changed reward destinations lacked regression coverage

- **Original locations**: `Assets/Scripts/Core/Offline.cs:68`, `Assets/Scripts/Managers/Gameplay/ActiveGatheringCoordinator.cs:55`
- **Description**: The first testing pass covered tertiary drops and ground-loot transactions but did not verify offline inventory-first deposits or gathering behavior with `AutoLoot=true`.
- **Disposition**: **Addressed.** Offline inventory deposit, bank exclusion, and overflow are covered at `Assets/Scripts/Tests/EditMode/ProgressionAndOfflineTests.cs:71-121`. Gathering with `AutoLoot=true` is covered at `Assets/Scripts/Tests/EditMode/LifeSkillsTests.cs:12-34`. Both affected test classes were reported green.

### Minor Issues

None.

### Suggestions

#### Requester-owned Phase 4 verification remains pending

- **Location**: `docs/1-plans/F_0.8.0_drops-loot-pickup.plan.md:225`
- **Description**: LootBag prefab authoring, slime content overrides, UI rebaking, and the manual feature checks remain `EDITED-UNVERIFIED`.
- **Disposition**: **Open, requester-owned, and non-blocking by design.** These steps require the live Unity Editor and were explicitly excluded from the implementation review gate.

---

## Checklist

- [x] 1. Functional Requirements — Passed; all four review findings were addressed.
- [x] 2. Code Quality — Passed; fixes are localized and explicit.
- [x] 3. Architectural Compliance — Passed; dependency direction remains Data ← Core ← Managers ← View/UI.
- [x] 4. Unity & Layering Compliance — Passed for source implementation; requester-owned Phase 4 authoring and Play-mode verification remain pending.
- [x] 5. Simulation Correctness & Determinism — Passed; tertiary active-roll and offline expected-value paths share the same effective-chance calculation.
- [x] 6. Error Handling — Passed; destroyed references, vacuum overflow, and residual spawn failures now resolve or report safely.
- [x] 7. Security — No material security concerns.
- [x] 8. Performance — Passed; ground-loot processing is record-based, vacuum retries are throttled, and one-shot warning state is cleaned up.

---

## Verdict

**APPROVED**

All four major findings were fixed without introducing new review findings. The reported verification gate is clean: `dotnet build IdleCloud.slnx` completes with zero errors and warnings, all affected EditMode test classes pass—including both destination-regression tests—and the PlayMode smoke suite is green. Phase 4 remains a documented requester-owned Editor handoff and does not block source-code approval.


---

## Addendum (post-approval, same session)

The following user-requested additions landed after the Codex loop converged and are part of this release but outside the reviewed diff above: gathering gains and craft outputs in the loot feed and as world popups (`LootFeedPanel`, `CombatFeedbackView`, `GameManager.CraftCompleted` event), subtler loot-popup styling, and red "Miss" popups for failed gathering swings and missed combat attacks (both directions). Build re-verified green after each addition; runtime-only changes, no rebake required.

The Phase 4 suggestion above is resolved: the user completed prefab/content/rebake authoring and verified the feature in Play mode (one authoring gap found and fixed during verification: the LootBag prefab initially had no sprite assigned).
