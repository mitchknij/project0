# Inspector-first configuration

The persistent `_GameManager` object is the runtime entry point. Its required references are:

- `ContentRegistry` for stable-ID content and content/balance versions;
- `OfflineProgressionConfig` for offline timing and rate policy;
- `CombatBalanceConfig` for combat and gathering formula inputs;
- `ProgressionBalanceConfig` for the XP curve;
- `AutoCombatPolicyConfig` for automatic-combat policy.

Core receives detached pure definitions/configuration snapshots. Save files contain stable IDs and
version strings, never Unity asset references. Changing any registry or balance version invalidates
efficiency snapshots; it does not reset character, inventory, or bank state.

## Creating content

Use `Create > IdleCloud > Content` to create an item, monster, resource node, recipe, map, talent,
class, or drop-table asset. Keep the stable ID unchanged when an asset is renamed or moved. Drag
the new asset into the matching list on `ContentRegistry`. Run `Validate Content Registry` before
entering Play Mode.

Drop tables support always-drops and weighted main-table rolls, including explicit `Nothing` slots.
The drop-table Inspector reports total weight, nothing probability, average quantity, and expected
quantity per 1,000 rolls. Item IDs and quantity ranges are validated by the registry.

Scene-local references remain on scene components. Prefer explicit references on `SceneBootstrap`
and `WorldMapContext`; legacy discovery remains available only for older scenes until they are
curated. Add `RuntimeDebugView` to the persistent manager object to inspect authoritative account,
character, activity, effective-stat, snapshot, revision, reward, and capacity state during Play Mode.

The legacy repositories remain the deliberate migration fallback for un-authored domains. New
content should be asset-backed; migrate an existing domain by creating its assets, registering them,
validating the registry, and then removing the corresponding legacy definitions in a dedicated
content migration change.
