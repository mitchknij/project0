# Roadmap implementation progress

Updated at completed integration-wave boundaries. The prioritized board remains the planning backlog.

## Foundation

| Card | Status | Implementation evidence | Remaining work / blocker | Wave |
| --- | --- | --- | --- | --- |
| P0-01 | blocked | `IdleCloud` appears in metadata, title copy, and the save filename, but no approval rationale exists. | Recommended default: approve `IdleCloud` provisionally and retain the existing save filename; user approval required. | 2 decision audit |
| P0-02 | blocked | No robot asset, code, or role brief was found outside the backlog. | Recommended default: non-combat companion/guide; user approval required. | 2 decision audit |
| P0-03 | blocked | The architecture defines active/idle progression and multi-character play, but not a complete fantasy statement. | Recommended default: guide an adventuring family through frontier pockets, building lasting active/offline power; user approval required. | 2 decision audit |
| P0-04 | blocked | Biome/map labels exist without durable lore pillars. | Recommended pillars: arcane-tech wilderness, linked pocket worlds, adventuring family, active mastery with persistent work; user approval required. | 2 decision audit |
| P0-05 | blocked | Family account, shared bank, characters, and starting map exist without approved fiction. | Recommended default: player leads a Thornhaven adventuring family; offline gains represent assigned work; user approval required. | 2 decision audit |
| P0-06 | blocked | Grasslands, Slime, resources, and exits exist mechanically without a lore-to-purpose sheet. | After P0-03 through P0-05 approval, map Thornhaven outskirts/Grasslands I to tutorial combat, copper/tree gathering, slime pressure, and the exit gate. | 2 decision audit |
| P0-07 | blocked | Desktop/mobile settings and generic bindings are implementation defaults, not a platform plan. | Recommended default: Windows, mouse/keyboard, 1920x1080 reference, 1280x720 minimum, 60 fps; defer gamepad and exclude mobile/web/consoles initially. | 2 decision audit |
| P0-08 | partial | `ItemDef.Id` is separate from display name; 89 shipped definitions have matching unique keys/IDs; asset registries reject blank/duplicate IDs before conversion; saves persist `ItemId`, not display data. | Rarity contract/taxonomy and direct duplicate/missing-ID plus display-rename compatibility tests. | 1 audit |
| P0-09 | partial | Schema-v4 production Save/Load now share isolated path helpers; compiled tests cover current-envelope filesystem round trip, missing/corrupt recovery, v1 migration, and full-envelope idempotence; migration docs match v4. | Run `SaveCompatibilityTests` in Unity EditMode Test Runner. | 2 implementation |
| P0-10 | partial | `docs/offline-progression-policy.md` documents the code-backed rate formula, one-minute minimum, 24-hour cap, clock rollback rejection, eligible snapshots, deterministic seed, and bulk/no-tick rule. | Add minimum/cap/rollback/seed/full-result repeatability tests and controlled backward-clock recovery. | 2 documentation |
| P0-11 | blocked | Itemized report UI exists, but rewards are eagerly applied and the report/claim acknowledgement is memory-only. | Establish a persisted pending-claim/acknowledgement contract and schema migration before implementation. | 1 audit |

## Cleanup and validation notes

- Ground Smash authoring is statically coherent: `GroundSmash.asset` uses stable ID `ground_smash`, the correct `SkillDefinitionAsset` script GUID, and the registry references its asset GUID/main object. Live Play-mode content startup remains to be confirmed.
- Initial audit baseline: `dotnet build IdleCloud.slnx` restored and compiled with 0 errors and 51 warnings.
- The in-progress ground-drops/pickup slice and all of its source, tests, docs, scenes, prefabs, and generated assets are owned by Claude and excluded from this route's implementation and commits.
- Local Codex configuration, AAA input documents, and the unrelated note deletion remain user-owned and uncommitted.
