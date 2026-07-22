# Code Review: Combat & Progression Juice

**Review Date**: 2026-07-19  
**Version**: 0.9.0  
**Files Reviewed**:

- `Assets/Art/UI/Generated/Fonts/PressStart2P SDF.asset`
- `Assets/Art/UI/Generated/Fonts/VT323 SDF.asset`
- `Assets/Prefabs/Enemies/DropTable_Slime.asset.meta`
- `Assets/Prefabs/Enemies/Monster_Slime.asset`
- `Assets/Prefabs/UI/GameUI.prefab`
- `Assets/Scenes/Bootstrap.unity`
- `Assets/Scenes/Maps/FirstMap.unity`
- `Assets/Scenes/Maps/FirstMap.unity.meta`
- `Assets/Scenes/PersistentGame.unity`
- `Assets/Scripts/Managers/GameManager.cs`
- `Assets/Scripts/Managers/Gameplay/ActiveCombatCoordinator.cs`
- `Assets/Scripts/Managers/Gameplay/ActiveGatheringCoordinator.cs`
- `Assets/Scripts/Managers/Gameplay/LootDropManager.cs`
- `Assets/Scripts/Tests/EditMode/ActiveCombatCoordinatorTests.cs`
- `Assets/Scripts/Tests/EditMode/LifeSkillsTests.cs`
- `Assets/Scripts/Tests/EditMode/LootDropManagerTests.cs`
- `Assets/Scripts/UI/MainHudPanel.cs`
- `Assets/Scripts/UI/TravelPanel.cs`
- `Assets/Scripts/UI/UIBuilder.cs`
- `Assets/Scripts/UI/UITheme.cs`
- `Assets/Scripts/View/CombatFeedbackView.cs`
- `Assets/Scripts/View/CombatView.cs`
- `Assets/Scripts/View/GatheringNodeView.cs`
- `Assets/Scripts/View/GatheringView.cs`
- `Assets/Scripts/View/LootBagView.cs`
- `Assets/Scripts/View/PlayerController.cs`
- `Assets/Scripts/View/SceneLoader.cs`
- `IdleCloud.slnx`
- `ProjectSettings/EditorBuildSettings.asset`
- `docs/STATE.md`

**Plan**: `docs/1-plans/F_0.9.0_combat-progression-juice.plan.md`

---

## Executive Summary

This change adds combat, gathering, loot, and progression feedback: per-kill coin popups, stronger critical-hit presentation, XP pulses, queued level-up banners, loot-bag bounce/vacuum animation, and gathering-node shake/crumble effects. Manager-layer event plumbing exposes existing progression results without changing Core simulation, save data, offline processing, or deterministic RNG order. All feature findings were addressed and the user completed the build, Unity EditMode, UI bake, and Play-mode gates.

APPROVED with observations

---

## Changes Overview

Managers now retain per-kill coin and level-transition data, distinguish vacuum pickups, and publish normalized XP and level-up events. View code consumes those events for combat, loot-bag, and gathering effects, while UI code adds the level-up banner and XP-bar pulse; the completed bake serializes these changes into `GameUI.prefab`.

The working tree also contains parallel map-rework changes. Those were inspected where they intersected prior findings but are outside this feature's release scope. The new untracked `LevelUpBannerPanel.cs` and its metadata were also reviewed through `git status`, although untracked files do not appear in `git diff --name-only HEAD`.

---

## Findings

### Critical Issues

None.

### Major Issues

1. **Starting-map path mismatch** — `Assets/Scenes/Bootstrap.unity:153`, `Assets/Scripts/View/SceneLoader.cs:18`, `ProjectSettings/EditorBuildSettings.asset:15`  
   The initial review found Bootstrap loading the deleted `FirstMap` path while Build Settings registered `Grasslands_1`. **Disposition: addressed.** All current startup paths consistently reference `Grasslands_1`; the rename remains part of the user's parallel map work.

2. **Slime drop-table GUID disconnected** — `Assets/Prefabs/Enemies/DropTable_Slime.asset.meta:2`, `Assets/Prefabs/Enemies/Monster_Slime.asset:30`  
   The initial review found the drop-table meta GUID changed without updating `Monster_Slime`, which would remove slime item drops after reimport. **Disposition: addressed outside feature scope.** Both files now reference `9b6fe00aaf93d8e4facdf4ccad93d36f`. User ownership of this parallel content wiring is recorded at `docs/STATE.md:782`.

3. **Level-up banner absent from the authoritative baked UI** — `Assets/Prefabs/UI/GameUI.prefab:23876`, `Assets/Prefabs/UI/GameUI.prefab:23948`, `Assets/Prefabs/UI/GameUI.prefab:23961`  
   The initial review found only source-side builder code; the existing baked UI prevented the runtime fallback from constructing the banner. **Disposition: addressed.** The rebaked prefab contains an active banner root, an idle CanvasGroup with alpha zero and raycasts disabled, and populated label/CanvasGroup references at `Assets/Prefabs/UI/GameUI.prefab:23964-23965`. Completion is recorded at `docs/1-plans/F_0.9.0_combat-progression-juice.plan.md:223`.

4. **Unity EditMode verification gate not executed** — `docs/1-plans/F_0.9.0_combat-progression-juice.plan.md:224`  
   Earlier rounds could not approve while the seven new tests remained authored but unexecuted. **Disposition: addressed.** The user ran the full Unity Test Runner EditMode suite and reported all tests green, including the seven new tests.

### Minor Issues

1. **Loot-bag spawn bounce scaled the clickable root collider** — `Assets/Scripts/View/LootBagView.cs:72`, `Assets/Scripts/View/LootBagView.cs:202`  
   The first implementation root-scaled the authored loot bag, briefly shrinking its click area despite the plan requiring a live collider from frame one. **Disposition: addressed.** The fallback now captures its circle colliders and counter-scales radius and offset at `Assets/Scripts/View/LootBagView.cs:210-216`; the user also verified bag bounce and vacuum behavior in Play mode at `docs/1-plans/F_0.9.0_combat-progression-juice.plan.md:225`.

### Suggestions

None.

---

## Checklist

- [x] 1. Functional Requirements — passed; all planned source behavior is implemented and user-verified in Play mode.
- [x] 2. Code Quality — passed; changes remain typed, focused, and consistent with existing event/coroutine patterns.
- [x] 3. Architectural Compliance — passed; Managers, View, and UI responsibilities remain separated with downward-only dependencies.
- [x] 4. Unity & Layering Compliance — passed; tunables are serialized, baked references are populated, and Editor follow-ups were completed.
- [x] 5. Simulation Correctness & Determinism — passed; no Core/offline/save changes or additional deterministic RNG draws were introduced.
- [x] 6. Error Handling — passed; partial pickups, missing targets, and interrupted animations retain graceful behavior.
- [ ] 7. Security — not applicable; no authentication, sensitive-data, or external-input surface changed.
- [x] 8. Performance — passed; feedback batching is capped and temporary animation resources are cleaned up.

---

## Verdict

**APPROVED with observations**

The build reports zero errors, the full Unity EditMode suite is green, the authoritative UI was rebaked and saved, and the user completed Play-mode verification. Parallel map/content changes were not treated as feature defects; their ownership is explicitly recorded, and the formerly broken slime GUID reference currently matches. Changelog work remains deferred to the TRIP-3 release phase. The normal Git refresh was obstructed by the known LFS temporary-file permission limitation, so the non-LFS HEAD comparison was inspected using a read-only filter bypass; generated PNG hashes matched their HEAD LFS objects.
