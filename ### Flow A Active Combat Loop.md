### Flow A: Active Combat Loop (2.5D Isometric - Deterministic Tick)

This flow handles real-time combat execution in a 2.5D space. It strictly separates spatial navigation from core logic and ensures deterministic math decoupled from visual rendering frames.

             +---------------------+
             |      CombatView     | (Rendering, UI, Particles - Runs on visual Unity frames)
             +----------+----------+
               /        |        \
Manual Click  /   UI Toggle       \ OnEnemyKilled
(Overrides)  V      (Auto)         V OnSkillExecuted
      +-------------+   |   +----------------+
      | Manual Input|   |   | Auto UI Button |
      +------+------+   V   +----------------+
              \         |
               V        V
             +---------------------+
             |      ActiveSim      | <--- Query --- +----------------------+
             | (Tracks: isAuto)    | --- Target --> | NavSystem (2.5D Grid)| (Handles A* Pathfinding & Line of Sight)
             | (Runs on FIXED TICK)|                +----------------------+
             +----------+----------+
                        |
                        V
             +---------------------+
             |     CombatMath      | (Pure Math: Accuracy, Crit, Damage)
             +----------+----------+
                        |
                        V
             +---------------------+
             |     DropsSystem     | (Progression, Loot Tables)
             +----------+----------+
                        |
                        V
             +---------------------+
             | StateData/Inventory | (Local Cache Memory)
             +---------------------+

#### 1. Input & State Control (View)
* **Manual Input Intercept:** Player manually clicks an enemy or terrain. `CombatView.cs` catches this input, instantly sets `isAutoCombatActive = false` via `ActiveSim.cs` to halt automation, and forwards the manual action.
* **Auto-Combat UI Toggle:** Player clicks "Auto". `CombatView.cs` sets `isAutoCombatActive = true` via `ActiveSim.cs`.

#### 2. Loop Execution & Spatial Queries (Core)
* `ActiveSim.cs` runs on a **deterministic fixed tick** (e.g., 10 or 20 ticks per second), independent of Unity's visual framerate (`Update`).
* **If `isAutoCombatActive == true`:** `ActiveSim` pings `NavSystem.cs` requesting the closest valid enemy ID. `NavSystem` performs the heavy 2.5D isometric pathfinding (NavMesh or Grid) and returns a clean vector path. `ActiveSim` processes movement along this path and queues attacks based on cooldowns.
* **If `isAutoCombatActive == false`:** Automated spatial queries to `NavSystem` are bypassed. `ActiveSim` relies exclusively on discrete manual target/movement commands forwarded from `CombatView.cs`.

#### 3. Logic & Hit Registration (Core)
* Triggered via Auto or Manual Input, `ActiveSim.cs` evaluates range via `NavSystem`. If in range, it executes the attack and calls `CombatMath.cs`.
* `CombatMath.cs` rolls accuracy checks, critical strike modifiers, and calculates final hit values within the player's min/max damage bounds.
* Target monster health is decremented. If health <= 0, the core triggers the death sequence.

#### 4. Loot & XP Distribution (Core)
* Upon monster death, `DropsSystem.cs` evaluates the map's loot tables against player modifiers.
* Items are appended directly to local cache (if Auto-Loot is enabled) or instantiated as physical entities on the grid via `NavSystem`.

#### 5. Write (Data)
* Real-time state transitions, structural currency changes, item additions, and experience gains are saved directly to local memory cache structures: `StateData.cs` and `InventoryData.cs`.

#### 6. Visuals & Feedback (View)
* The core engine dispatches lightweight events such as `OnEnemyKilled`, `OnDamageDealt`, or `OnSkillExecuted`.
* `CombatView.cs` listens to these triggers inside Unity's standard `Update()` loop to render animations, interpolate smooth movement between grid points, and spawn visual particle effects without blocking the core simulation.