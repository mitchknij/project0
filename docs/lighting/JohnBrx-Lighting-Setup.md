# JohnBrx-Style Lighting — Setup & Reference

Companion doc to `IdleCloud_JohnBrx_Lighting_Claude_Code_Plan_REVISED.md` (repo root). Covers the Phase A
inspection findings and the Phase B `ProjectedSpriteShadow` component: what it does, how to wire it in the
Unity Editor, and what still needs to be prepared by hand.

## 1. Files delivered

| File | Purpose |
|---|---|
| `Assets/Iso/Sorting/ProjectedSpriteShadow.cs` | New component. Mirrors an owner sprite into a dark, tilted child "shadow" renderer, driven by a rotating 2D sun light. |
| `docs/lighting/JohnBrx-Lighting-Setup.md` | This file. |

No existing files were modified. No scene, sorting formula, pipeline asset, or navigation code was
touched.

## 2. Architecture this plugs into (Phase A findings)

- **Pipeline is already ready.** Unity 6000.5.1f1, URP 17.5.0, active render-pipeline asset is
  `Assets/Settings/Universal Render Pipeline Asset 2D.asset` using a true **Renderer2DData** (2D
  Renderer). Sprites already use **Sprite-Lit-Default**, so they already respond to `Light2D`.
- **Sorting is untouched.** `IsoTerrainSortCalculator` computes
  `sortingOrder = round((-(x+y) + height*2.5) * 10) + height`, giving **10-unit gaps** between adjacent
  cells and **26-unit gaps** between floors (`IsoSortSettings.asset`: `sortScale=10`,
  `elevationSortWeight=2.5`). `ProjectedSpriteShadow` reads the owner's already-resolved
  `SpriteRenderer.sortingOrder` and offsets it by a small configurable amount (`sortingOrderOffset`,
  default `-1`) — well inside that gap, so it can never collide with a neighbouring cell's band. It never
  recomputes the sort key.
- **Player structure.** In `Scene A.unity`, the **root "Player"** GameObject carries `PlayerController`,
  `Rigidbody2D`, `SortingGroup`, and `PlayerSortController`. The **child "Visual"** GameObject carries the
  only `SpriteRenderer` and `SpriteSheetAnimator`. `PlayerSortController` drives the root's `SortingGroup`
  every `LateUpdate`; a shadow child added under the Player root sits *inside* that group, so its local
  `sortingOrder` (owner order + offset) places it relative to the body without affecting the group's
  world placement.
- **Feet are already bottom-center.** `basic idle small.png` / `basic walk small.png` are sliced with
  `alignment 7` (BottomCenter) / `pivot (0.5, 0)`, so the feet sit at the Visual's local origin — the
  default `footAnchorOffset = (0, 0)` is correct out of the box.
- **Light direction reuse.** `Assets/Scripts/View/SunOrbitController.cs` already orbits a 2D "sun" light
  around the camera and exposes `Vector2 ShadowDirection => -LightDirection`. `ProjectedSpriteShadow`
  consumes this directly — no new light-direction logic was written.
- **Assembly placement.** `ProjectedSpriteShadow` lives in `Assets/Iso/Sorting/` in namespace
  `Iso.Sorting`, compiling into `Assembly-CSharp` (same as `PlayerSortController`). It references only
  `UnityEngine` and the global-namespace `SunOrbitController` — it does **not** need to reference
  `IdleCloud.View`.

## 3. What `ProjectedSpriteShadow` does

Per instance, in `LateUpdate`:
1. If the source sprite is null (pooled/disabled/not yet set), disables the shadow renderer and returns.
2. Copies `source.sprite`, `flipX`, `flipY` onto the shadow child's `SpriteRenderer`.
3. Sets `shadowRenderer.color` to a configurable dark/translucent tint (default `(0,0,0,0.35)`).
4. Sets `shadowRenderer.sortingLayerID = source.sortingLayerID` and
   `sortingOrder = source.sortingOrder + sortingOrderOffset` (default `-1`, i.e. behind the owner).
5. Positions the shadow at the foot anchor and rotates it `tiltAngleX` degrees (default 45, per the
   transcript) plus a yaw toward the current `ShadowDirection` (from `SunOrbitController`, or a fixed
   direction if configured), then scales it for `length` / `skew` / `verticalCompression`.

All of this uses **one shared, cached, non-instanced material** (`Sprites/Default`, created once as a
`static Material`) — no material is created per instance or per frame. Tinting is done via
`SpriteRenderer.color`, which is per-renderer state, not a material property, so no
`MaterialPropertyBlock` was needed for this simple case (none existed in the project to reuse).

The shadow GameObject (`"ProjectedShadow"`) is created lazily as a child of whatever GameObject the
component is on, the first time it's needed, and reused after that — safe with enable/disable and
pooling.

## 4. Editor wiring steps (you do this)

1. Open `Scene A.unity` and select the root **Player** GameObject.
2. **Add Component → Projected Sprite Shadow.**
3. Drag the **Visual** child's `SpriteRenderer` into the **Source** field.
4. Drag the scene's sun-orbit object (the one carrying `SunOrbitController`) into the **Sun** field. If
   left empty, the component auto-finds one via `FindFirstObjectByType<SunOrbitController>()` at `Awake`.
5. Leave `Sorting Order Offset` at `-1` unless you have a specific reason to change it (see §2 for why
   `-1` is safe).
6. Enter Play Mode. A "ProjectedShadow" child should appear under the Player, mirroring the current
   sprite frame, tilted and tinted.
7. Tune to taste: `Shadow Color` (opacity), `Tilt Angle X`, `Length`, `Skew`, `Vertical Compression`,
   `Foot Anchor Offset`.
8. Tick **Debug** to see live diagnostic fields (source sprite name, resolved sorting orders, direction,
   sprite-match flag) in the Inspector and a gizmo showing the foot anchor + projection direction in the
   Scene view. Debug fields/gizmos are compiled out of builds (`#if UNITY_EDITOR`).

## 5. Normal-map / light wiring (separate, manual, not part of this component)

Not implemented by this pass — listed here for reference per the governing plan §3.1/§8:
- Both scene `Light2D`s (`Global Light 2D`, `Spot Light 2D`) currently have `Use Normal Map` **off**.
  Enable it on the lights you want reacting to normal maps.
- Only `Basic block v3.png` and `trees.png` currently have a `_NormalMap` secondary texture bound
  (`Assets/Art/Tilesets/Basic block_normalmap.png`, `Assets/Art/Tilesets/trees normalmap.png`). Player,
  mob, grass, and UI sprites have no normal map yet — bind these per-sprite in the Inspector's
  **Secondary Textures** list under the name `_NormalMap` once art exists, matching plan §3.1.A/§8.5-6.
- Do the manual visual proof (plan §5, Phase 2) before judging the final look: confirm one Sprite-Lit
  tile's top/left/right faces respond differently to a moving `Light2D`.

## 6. Default parameter values

| Field | Default | Notes |
|---|---|---|
| `sortingOrderOffset` | `-1` | Safe within the 10-unit cell gap. |
| `shadowColor` | `(0,0,0,0.35)` | ~35% opaque black, per plan's 25–40% recommendation. |
| `tiltAngleX` | `45` | Matches the transcript literally. |
| `length` | `1` | |
| `skew` | `0.5` | |
| `verticalCompression` | `0.6` | |
| `footAnchorOffset` | `(0,0)` | Correct as-is for the current bottom-center-pivot player art. |
| `useFixedDirection` | `false` | IdleCloud extension — off by default, uses the live sun. |

## 7. Known limitations (v1, by design)

- Player only — not wired to enemies/props. Enemies use a different flat, non-SortingGroup structure
  (`ElevationSorter`, built at runtime by `SceneBootstrap.SpawnEnemy`), so the same wiring does not map
  1:1; extending to enemies is a follow-up task.
- No cross-floor or multi-tile shadow projection; short shadows only, as specified.
- No physically-correct shadow shape — transform-only shear/tilt/compression, matching the transcript's
  simple prefab-and-transform method, not a shader-based projection.
- Shadow shape (skew/tilt) is not foot-anchor-aware per animation frame — it uses one fixed anchor offset,
  not per-frame metadata. Acceptable per plan §2/§3.1.D (source art should be visually validated by the
  user across idle/walk/attack frames).

## 8. Test checklist

- [ ] Idle animation: shadow uses the current idle frame.
- [ ] Walking in all directions: shadow frame updates; `flipX` mirrors correctly.
- [ ] Shadow stays attached to the feet through animation (no detach/wobble).
- [ ] Shadow leans away from the sun; rotating the sun changes the shadow direction live.
- [ ] Shadow renders behind the player body in normal situations.
- [ ] Walking onto a higher `floorIndex` does not create a large sort jump or a detached shadow.
- [ ] Disabling the `ProjectedSpriteShadow` component returns the player to its previous rendering
      behaviour (no shadow child, no side effects).
- [ ] Null/changing source sprite (e.g. disabling the Visual renderer) does not throw; shadow disables
      gracefully.
- [ ] Multiple instances (if added to more than one owner later) share the same material — verify in the
      Profiler / Frame Debugger that no new material is created per frame.

## 9. Rollback

Remove the `ProjectedSpriteShadow` component from the Player (and delete the auto-created
`"ProjectedShadow"` child if it persists in the scene), then delete
`Assets/Iso/Sorting/ProjectedSpriteShadow.cs` and this doc. No other file was touched, so no other rollback
step is required.
