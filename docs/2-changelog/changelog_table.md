# Changelog Table

| Version | Week | Commit Message                  |
| ------- | ---- | ------------------------------- |
| `0.9.0` | 1    | feat(juice): combat & progression feedback — level-up banner, coin/crit popups, XP-bar pulse, loot-bag bounce/vacuum, node shake/crumble (v0.9.0) |
| `0.8.0` | 1    | feat(loot): ground-loot bags with click/vacuum pickup, loot feed + popups, slime_goo drop content, tertiary drop structure (v0.8.0) |
| `0.7.0` | 1    | feat(lifeskills): copper mining on FirstMap — swing progress bar, gathering nodes, craft-from-inventory (v0.7.0) |
| `0.5.0` | 1    | feat(combat): tile-first targeting — Shatterstep + Arcane Detonation tile-pattern skills (v0.5.0) |
| `0.2.0` | 1    | feat: rebuild UI layer on procedural theme with hardened boot and bake pipeline |
| `0.1.1` | 1    | chore: initialize TRIP workflow |

# Changelog Summary

- **v0.9.0 (Combat & Progression Juice - Week 1, 19-07-2026)**:
  - **Progression feedback**: queued gold level-up banners (character + skill) via new `GameManager.XpAwarded`/`LevelUp` events; XP-bar pulse in the HUD on every XP award
  - **Combat feedback**: per-kill "+N coins" popups (`KillLootRecord.Coins`, reuses existing roll), crits orange/larger with scale-punch
  - **Loot & gathering feedback**: bag spawn bounce + AutoLoot vacuum fly-to-player tween (`LootPickedUpEvent.Vacuum`; partial pickups keep the bag), node hit-shake per swing + crumble-puff per gather (capped per tick)
  - **Scope**: Managers/View/UI only — no Core/Data, save, or RNG-order changes; 7 new EditMode tests; details `docs/2-changelog/w1_v0.9.0.md`

- **v0.8.0 (Drops & Loot Pickup - Week 1, 19-07-2026)**:
  - **Loot**: physical ground bags per kill (click to pick up; AutoLoot = auto-vacuum), inventory-first destinations everywhere, tunable despawn, runtime-only (no save change)
  - **Systems**: `LootDropManager` lifecycle service + GameManager-owned commit path (`LootPickedUp` session event); tertiary drop structure with active/offline parity; slime → `slime_goo` via ScriptableObject content overrides
  - **Feedback**: HUD loot feed + world popups for pickups/gathering/crafts, red "Miss" popups (combat both directions + gathering), dedicated AutoLoot HUD toggle
  - **Behavior changes**: gathering ignores AutoLoot (always inventory), offline loot inventory-first (report says INVENTORY FULL); details `docs/2-changelog/w1_v0.8.0.md`

- **v0.7.0 (Mining Lifeskill - Week 1, 19-07-2026)**:
  - **Lifeskills**: first live gathering nodes — copper rocks (`copper_vein`) + choppable tree (`oak_tree`) in FirstMap; Core exposes per-swing progress (`ActionIntervalMs`/`ActionProgress01`), no swing accrual while walking
  - **View**: amber world-space progress bar on the mined node; clicked-instance targeting across identical rocks; assignment-rejection warning
  - **Crafting (scope change)**: materials bank-first then character inventory, coins bank-only, output to inventory, `CanCraft` dry-run guards full inventory
  - **Lineage note**: v0.6.0 (skillbar eight slots) still unreleased/planned; details `docs/2-changelog/w1_v0.7.0.md`

- **v0.5.0 (Tile-First Targeting - Week 1, 17-07-2026)**:
  - **Combat**: deterministic tile-pattern targeting (`TilePatternResolver`, Core) — skills declare Cross/SquareRadius/SingleTile/CustomOffsets patterns anchored on caster or target tile; circle/actor skills unchanged
  - **Skills**: Shatterstep (self-anchored Cross, auto-gates on 2 enemies) and Arcane Detonation (3×3 around target); slot-order auto-combat + offline approximation include tile skills
  - **Presentation**: `TileAreaResolved` event -> fading tile-outline overlays via `GridPathfinder` conversion seam; overlays mirror Core hits exactly
  - **Lineage note**: ships the unreleased v0.3.0 (active combat tick) and v0.4.0 (MMO skillbar + skill engine) WIP inside this release; details `docs/2-changelog/w1_v0.5.0.md`

- **v0.2.0 (UI Clean Rebuild - Week 1, 16-07-2026)**:
  - **UI**: procedural-only theme (all PNG art dropped from code), `UITheme.Layout` constants, HUD rebuilt with 7 nav buttons + responsive width clamp
  - **Boot**: deferred `UIBuilder.Bootstrap` fallback behind `SceneLoader.InitialLoadCompleted`; baked prefab authoritative
  - **Bake**: `UIBakeTool` gate now rejects non-generated persistent assets; details `docs/2-changelog/w1_v0.2.0.md`
  - **Known issue accepted**: GameUI instance in FirstMap instead of PersistentGame (unloads on travel) — open item

- **v0.1.1 (TRIP Initialization - Week 1, 16-07-2026)**:
  - **Setup**: Initialized TRIP workflow with docs structure (`docs/1-plans/`, `2-changelog/`, `3-code-review/`, `4-unit-tests/`, `6-memo/`)
  - **Documentation**: Generated ARCHI.md with Game (Unity 2.5D isometric idle RPG) architecture; ARCHI.md defers normative rules to `docs/guardrails/PROJECT.md`
  - **Files Added**: docs/ARCHI.md, docs/ARCHI-rules.md, docs/2-changelog/changelog_table.md, docs/4-unit-tests/TESTING.md
