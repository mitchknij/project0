# Code Review: Tile-First Targeting

**Review Date**: 2026-07-17  
**Version**: 0.5.0  
**Files Reviewed**:

- `Assets/Scripts/Core/Activity.cs`
- `Assets/Scripts/Core/Combat/ActiveSim.cs`
- `Assets/Scripts/Core/Combat/AutoCombatPolicy.cs`
- `Assets/Scripts/Data/ClassesRepo.cs`
- `Assets/Scripts/Data/State/ActiveCombatContracts.cs`
- `Assets/Scripts/Data/State/GameTypes.cs`
- `Assets/Scripts/Data/State/SkillContracts.cs`
- `Assets/Scripts/Managers/Content/ContentValidator.cs`
- `Assets/Scripts/Tests/EditMode/AutoCombatPolicyTests.cs`
- `Assets/Scripts/Tests/EditMode/ContentValidationTests.cs`
- `Assets/Scripts/Tests/EditMode/SkillSnapshotApproximationTests.cs`
- `Assets/Scripts/View/CombatFeedbackView.cs`
- `Assets/Scripts/View/GridPathfinder.cs`
- `IdleCloud.slnx`
- `docs/4-unit-tests/COVERAGE-DEBT.md`
- `docs/STATE.md`

**Plan**: `docs/1-plans/F_0.5.0_tile-first-targeting.plan.md`

---

## Executive Summary

This change adds deterministic source- and target-anchored tile-pattern targeting, starter skills, auto-combat and offline integration, combat events, validation, and placeholder tile overlays. All substantive review findings were addressed, and the reported build and EditMode test gates pass.

APPROVED with observations

---

## Changes Overview

The Data and Core layers gain tile-pattern contracts, deterministic tile/actor resolution, scheduled and immediate impact handling, automatic-use eligibility, and conservative offline approximation. `ClassesRepo` adds Ground Smash and Arcane Detonation, while the View converts resolved tiles through `GridPathfinder` and renders configurable short-lived overlays. Tests cover resolver ordering, validation, auto-combat, offline estimates, event payloads, rewards, and scheduled anchor behavior.

---

## Findings

### Critical Issues

None.

### Major Issues

#### Source-anchored scheduled impacts cancelled by incidental target death

- **Location**: `Assets/Scripts/Core/Combat/ActiveSim.cs:1259`, `Assets/Scripts/Core/Combat/ActiveSim.cs:1297`
- **Description**: Pending source-anchored tile impacts originally retained the selected target ID and were cancelled if that unrelated target died before impact.
- **Disposition**: **Addressed.** Cancellation now preserves `TilePatternAroundSource` impacts while continuing to cancel target-anchored impacts. Regression coverage verifies both outcomes at `Assets/Scripts/Tests/EditMode/TileTargetingSkillTests.cs:156` and `Assets/Scripts/Tests/EditMode/TileTargetingSkillTests.cs:205`.

#### Per-tile overlay material leak

- **Location**: `Assets/Scripts/View/CombatFeedbackView.cs:37`, `Assets/Scripts/View/CombatFeedbackView.cs:79`, `Assets/Scripts/View/CombatFeedbackView.cs:145`
- **Description**: Each affected tile originally allocated a native `Material` that was not destroyed, accumulating thousands of materials during long auto-combat sessions.
- **Disposition**: **Addressed.** The View now caches one material, assigns it through `sharedMaterial`, and destroys it during teardown.

#### Accidental Unity AI and project-configuration churn

- **Location**: `Packages/manifest.json:7`, `ProjectSettings/UnityConnectSettings.asset:7`, `ProjectSettings/ProjectSettings.asset:832`
- **Description**: A later iteration accidentally added preview Unity AI packages, enabled UnityConnect, introduced package compiler defines, and rewrote package, build, and URP configuration unrelated to tile targeting.
- **Disposition**: **Addressed with user-approved revert.** AI dependencies are absent, UnityConnect is disabled, compiler defines are restored, and the package, URP, and project settings no longer appear in the change set. The remaining generated solution ordering is recorded separately below.

### Minor Issues

#### Generated solution project ordering remains changed

- **Location**: `IdleCloud.slnx:7`
- **Description**: Unity regenerated the solution with the EditMode test and View project entries in a different order.
- **Disposition**: **Open observation.** This is ordering-only, does not change project membership, and did not affect the verified build. It is non-blocking but may be omitted as generated churn.

### Suggestions

#### Pre-existing PlayMode identity test failure

- **Location**: `Assets/Scripts/View/CombatTargetView.cs:47`, `Assets/Scripts/View/CombatTargetView.cs:66`, `Assets/Scripts/Tests/PlayMode/GameplayLoopSmokeTests.cs:219`, `docs/STATE.md:678`
- **Description**: `CombatSpatialAdapter_CapturesLogicalActorsWithoutSpriteGeometry` fails because `Awake` derives an ID before `ConfigureRuntimeIdentity` can supply one.
- **Disposition**: **Accepted with override.** The failure was reproduced on the v0.4.0 base with the involved production and test files unchanged, so it is outside this plan. The remaining PlayMode tests pass.

#### Manual tile-overlay verification pending

- **Location**: `docs/1-plans/F_0.5.0_tile-first-targeting.plan.md:498`
- **Description**: The user has not yet completed the visual Play-mode acceptance pass for exact tile placement, floor handling, and overlay/effect consistency.
- **Disposition**: **Open, requester-owned observation.** Exact acceptance steps are recorded in the plan, so the change remains explicitly verifiable without blocking code approval.

---

## Checklist

- [x] 1. Functional Requirements — passed
- [ ] 2. Code Quality — passed with caveat: nonfunctional `IdleCloud.slnx` ordering churn remains
- [x] 3. Architectural Compliance — passed
- [ ] 4. Unity & Layering Compliance — passed with caveat: requester-owned Play-mode overlay verification remains pending
- [x] 5. Simulation Correctness & Determinism — passed
- [x] 6. Error Handling — passed
- [x] 7. Security — passed
- [x] 8. Performance — passed

---

## Verdict

**APPROVED with observations**

Both production-impacting code findings were fixed and regression-tested. The accidental Unity AI/configuration installation was reverted with user approval. The reported post-revert build has zero errors, all 103 EditMode tests pass, the sole PlayMode failure is documented as pre-existing, and the remaining work is limited to the requester’s visual overlay acceptance pass and optional removal of generated solution ordering churn. Changelog work remains deferred to the TRIP-3 release stage.

