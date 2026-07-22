# Three-scene world structure

## Inspection report

- **Bootstrap:** one `Bootstrap` object with `SceneLoader`; it has no camera, player, UI, map, lighting, or gameplay content.
- **PersistentGame:** `_GameManager` (including `SaveManager` and `GameManager`), `Player`, `Main Camera`, and `CinemachineCamera` from Scene A. UI remains persistent because `GameManager` creates it at runtime.
- **FirstMap:** the copied Grid/tilemaps, terrain visuals, trees, enemies, `GameplayLoop`, `SceneBootstrap`, global/spot lighting, post-processing volume, colliders, and all other Scene A content not listed above.
- **Uncertain ownership:** no authored EventSystem or audio root exists in Scene A. `UIInputBootstrap` creates the EventSystem at runtime, and no audio system was found to move.
- **Risk controls:** SceneBootstrap, CombatView, and GatheringView resolve the persistent player at runtime after their serialized Scene A reference is cleared. The player, main camera, and Cinemachine camera move together so their direct relationships remain scene-local.
- **Implementation:** Bootstrap loads PersistentGame then FirstMap additively, sets FirstMap active, and positions the persistent player at an authored `MapSpawnPoint` with ID `default`.

## One-time migration

1. In the open Unity Editor, wait for scripts to compile.
2. Choose **Tools > IdleCloud > Create Three-Scene Structure**.
3. Confirm the Console reports `[SceneStructureMigration] Created Bootstrap, PersistentGame, and Maps/FirstMap`.

The command refuses to overwrite any of its three target scenes. It first saves `Scene A` as a copy at `Assets/Scenes/Maps/FirstMap.unity`; the original `Assets/Scenes/Scene A.unity` remains the rollback scene.

## Resulting workflow

- Build Settings order is Bootstrap, PersistentGame, then FirstMap.
- Enter Play Mode through Bootstrap. The loader keeps PersistentGame loaded and makes FirstMap the active scene.
- Edit map art and map-local gameplay in `FirstMap.unity` normally. The default arrival marker is `Spawns/default`; move or rotate it to change the player arrival transform.
- Keep player, cameras, `_GameManager`, runtime UI, and future global services in PersistentGame. Keep tilemaps, terrain, environment, colliders, lights/volumes, enemies, triggers, and map-specific gameplay in map scenes.
- To revert during development, restore Build Settings to `Scene A.unity` and open Scene A; the migration never modifies it.

## Manual verification still required

After running the migration, test Bootstrap startup, player movement/camera/UI/input, collision and sorting, lighting, and a FirstMap unload/reload while PersistentGame remains loaded. No Unity Play Mode verification was possible from the active editor process in this session.
