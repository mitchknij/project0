# Code Review: Mining Lifeskill on FirstMap Copper Rocks

**Review Date**: 2026-07-19  
**Version**: 0.7.0  
**Files Reviewed**:

- `Assets/Art/UI/Generated/Fonts/PressStart2P SDF.asset`
- `Assets/Art/UI/Generated/Fonts/VT323 SDF.asset`
- `Assets/Prefabs/World/Trees/Tree_0.prefab`
- `Assets/Scenes/Maps/FirstMap.unity`
- `Assets/Scripts/Core/Craft.cs`
- `Assets/Scripts/Core/LifeSkills/LifeSkills.cs`
- `Assets/Scripts/Data/State/ActiveGatheringContracts.cs`
- `Assets/Scripts/Managers/GameManager.cs`
- `Assets/Scripts/Tests/EditMode/LifeSkillsTests.cs`
- `Assets/Scripts/Tests/PlayMode/GameplayLoopSmokeTests.cs`
- `Assets/Scripts/UI/CraftingPanel.cs`
- `Assets/Scripts/View/GatheringNodeView.cs`
- `Assets/Scripts/View/GatheringView.cs`
- `docs/STATE.md`

**Plan**: `docs/1-plans/F_0.7.0_mining-copper-lifeskill.plan.md`

---

## Executive Summary

The change makes copper mining and oak gathering playable, adds deterministic per-swing progress feedback, and supports crafting with bank-first materials plus character-inventory fallback. Two user-approved amendments place crafted output in the character inventory and provide honest full-inventory validation. All review findings were addressed and every reported verification gate is green.

APPROVED

---

## Changes Overview

Core gathering now reports swing timing and progress while preventing out-of-range time from accruing. The View tracks the selected gathering-node instance and displays a world-space progress bar; authored copper-rock and oak-tree nodes complete the playable loop. Crafting consumes materials from the bank first and inventory second, keeps coins bank-only, deposits output into character inventory, and validates output capacity before enabling the craft action.

---

## Findings

### Critical Issues

None.

### Major Issues

1. **Tree shadows removed from authored scene objects** — `Assets/Scenes/Maps/FirstMap.unity:17717`  
   The initial scene edit removed `ProjectedSpriteShadow` from all 84 tree roots. **Disposition: addressed.** All tree roots have their shadows restored with the tuned color, length, and skew values represented at `Assets/Scenes/Maps/FirstMap.unity:17728-17737`; only the pre-existing `Floor addons 1` non-tree shadow remains.

2. **Rocks parent incorrectly gained rendering and shadow components** — `Assets/Scenes/Maps/FirstMap.unity:31333`  
   The `Rocks` container initially contained `Tilemap`, `SortingGroup`, and `ProjectedSpriteShadow`, creating unintended outer sorting and shadow behavior. **Disposition: addressed.** It is now Transform-only, with rock prefab instances directly parented beneath it at `Assets/Scenes/Maps/FirstMap.unity:31333-31360`.

3. **Rejected gathering assignment failed silently** — `Assets/Scripts/View/GatheringView.cs:128`  
   `GameManager.Assign` could swallow validation failures, leaving a selected node without an active gathering activity. **Disposition: addressed.** The View now verifies the resulting activity, logs actionable node-map and character-map context, and clears rejected selection state at `Assets/Scripts/View/GatheringView.cs:128-142`.

4. **PlayMode crafting test retained the obsolete bank-output expectation** — `Assets/Scripts/Tests/PlayMode/GameplayLoopSmokeTests.cs:286`  
   After crafted output moved into character inventory, the existing PlayMode test still expected a pickaxe in the bank and would fail. **Disposition: addressed.** It now records the pre-craft inventory count, verifies a one-item increase, and confirms the bank receives no output at `Assets/Scripts/Tests/PlayMode/GameplayLoopSmokeTests.cs:286-295`.

5. **Full inventory caused an enabled craft action followed by a silent no-op** — `Assets/Scripts/Core/Craft.cs:53`  
   `Craft` detected inventory overflow, but `CanCraft` did not, so the panel enabled an action whose exception was subsequently swallowed. **Disposition: addressed.** `CanCraft` now previews post-consumption inventory state and returns `Inventory is full` at `Assets/Scripts/Core/Craft.cs:53-67`; the panel displays the reason and disables the action through `Assets/Scripts/UI/CraftingPanel.cs:26-49`. Regression coverage is provided at `Assets/Scripts/Tests/EditMode/CraftingTests.cs:65-82`.

### Minor Issues

None.

### Suggestions

None.

---

## Checklist

- [x] 1. Functional Requirements — Passed.
- [x] 2. Code Quality — Passed.
- [x] 3. Architectural Compliance — Passed.
- [x] 4. Unity & Layering Compliance — Passed; Phase 2 authoring and Phase 3 Play-mode verification are complete.
- [x] 5. Simulation Correctness & Determinism — Passed.
- [x] 6. Error Handling — Passed.
- [x] 7. Security — Not materially applicable; no concerns found.
- [x] 8. Performance — Passed.

---

## Verdict

**APPROVED**

The deliberate crafting divergence from `store.ts`—bank-first material consumption, inventory fallback, bank-only coins, character-inventory output, and `CharacterChanged` publication—is authorized by the plan’s scope-change section. The final reported gates are zero build errors with only pre-existing CS0618 warnings, six passing `LifeSkillsTests`, five passing `CraftingTests`, all 12 passing `GameplayLoopSmokeTests`, and user-confirmed completion of the mining, node-switching, progress-bar, and craft-to-inventory Play-mode loop. No findings remain open or overridden.

