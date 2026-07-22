# Code Review: UI Clean Rebuild

**Review Date**: 2026-07-16
**Version**: `0.2.0`
**Files Reviewed**:

- `### Flow A Active Combat Loop.md`
- `Assets/Editor/UIBakeTool.cs`
- `Assets/Scripts/UI/MainHudPanel.cs`
- `Assets/Scripts/UI/PanelManager.cs`
- `Assets/Scripts/UI/UIBuilder.cs`
- `Assets/Scripts/UI/UIHelpers.cs`
- `Assets/Scripts/UI/UITheme.cs`
- `Assets/Scripts/View/SceneLoader.cs`
- `Plan Basic play loop.md`
- `docs/1-plans/F_0.2.0_ui-clean-rebuild.plan.md`
- `docs/STATE.md`
- `tree prefabs.md`

**Plan**: `docs/1-plans/F_0.2.0_ui-clean-rebuild.plan.md`

---

## Executive Summary

The UI layer was rebuilt around a procedural theme, centralized layout metrics, hardened direct-reference wiring, deferred runtime fallback, and stricter bake-time asset validation. One major responsive-layout issue was found during review and subsequently fixed.

APPROVED

---

## Changes Overview

The change removes runtime PNG-art loading, converts navigation to text buttons, centralizes UI dimensions, and retains the established builder-to-prefab bake pipeline. It also coordinates UI fallback with additive scene loading, validates baked sprite/font references, removes verified dead APIs, and makes the HUD responsive to canvas-width changes. No Core, Data, Managers, save, or simulation behavior changed.

---

## Findings

### Critical Issues

None.

### Major Issues

#### Responsive HUD width and fixed-control sizing

- **Original locations**: `Assets/Scripts/UI/UIBuilder.cs:347`, `Assets/Scripts/UI/UIBuilder.cs:365`, `Assets/Scripts/UI/UIBuilder.cs:445`
- **Description**: The initial implementation calculated the HUD width only during construction, so the baked prefab would not react to later window or aspect-ratio changes. Navigation buttons and skill slots also had preferred widths without matching minimum widths, allowing the layout group to shrink them instead of shrinking only the center block.
- **Disposition**: **Addressed.** `MainHudPanel` now clamps its width on enable and whenever the parent canvas width changes (`Assets/Scripts/UI/MainHudPanel.cs:48`, `Assets/Scripts/UI/MainHudPanel.cs:93`, `Assets/Scripts/UI/MainHudPanel.cs:108`). `UIHelpers.AddLayout` now supports `minW` (`Assets/Scripts/UI/UIHelpers.cs:96`), and fixed minimum widths are applied to navigation buttons and skill slots (`Assets/Scripts/UI/UIBuilder.cs:365`, `Assets/Scripts/UI/UIBuilder.cs:446`). The center block retains its configured minimum-width floor at `Assets/Scripts/UI/UIBuilder.cs:387`.

### Minor Issues

None.

### Suggestions

None.

---

## Checklist

- [x] 1. Functional Requirements — passed; the responsive HUD correction brought the implementation into plan conformance.
- [x] 2. Code Quality — passed.
- [x] 3. Architectural Compliance — passed.
- [x] 4. Unity & Layering Compliance — passed; required rebake and Editor/Play follow-ups are explicitly recorded.
- [x] 5. Simulation Correctness & Determinism — not applicable; no simulation or persistence logic changed.
- [x] 6. Error Handling — passed.
- [x] 7. Security — not applicable.
- [x] 8. Performance — passed; width monitoring adds constant-time cached work to the existing HUD update loop.

---

## Verdict

**APPROVED**

The reported build completed with zero errors and only baseline CS0618-family warnings. EditMode testing is not applicable; the PlayMode suite, UI rebake, scene placement, and seven-point Editor checklist remain explicitly `EDITED-UNVERIFIED` with exact confirmation steps in the plan. The release changelog is deferred to TRIP-3-release, and the unrelated root-note edits and deletions were documented as intentional user changes.

**Requester note (post-review override, 2026-07-16)**: after this review converged, the bake placed the `GameUI` instance into `Maps/FirstMap.unity` instead of the plan-designated `PersistentGame.unity` (FirstMap was the active scene during the bake). The requester chose to release as-is with this known issue open: the UI will unload with FirstMap on map travel until the instance is moved to PersistentGame. Tracked in `docs/STATE.md`.
