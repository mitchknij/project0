# IdleCloud - Project Compass & Guardrails

## 1. North Star & Context
- **Genre:** 2.5D top-down isometric idle RPG (billboard sprites).
- **Simulation Philosophy:** Active gameplay defines efficiency. When the player is active, the game calculates current "gains per hour" (kills/resources). When offline, the system uses these pre-calculated rates to apply bulk rewards based on total elapsed time when the player logs back in, rather than simulating real-time events.
- **Logic Execution:** Headless & decoupled. All combat, math, and efficiency formulas are pure C#, independent of Unity MonoBehaviours or frames.
- **Active Interactivity:** Real-time and responsive. While auto-combat handles baseline pathing/targeting, the Core engine instantly intercepts and weaves manual player inputs (movement, skill triggers) into the current active tick.
- **Account:** Multi-character, shared bank/currency pool. Snapshot-based and deterministic.

## 2. Architectural Borders (Strict Downward Flow)
- **View & UI:** Presentation only. Reads all layers. No layer may reference View/UI.
- **Managers:** Orchestration. Bridges Core and Data.
- **Core Logic:** Combat math, life skills, progression. Reads Data. Never references Managers/View/UI.
- **Data & State:** Pure models, static configs, runtime state. 100% independent.

## 3. Development Sandbox & Style
- **Location:** Code lives in `Assets/Scripts/` within its `.asmdef`. Art/Prefabs live in `Assets/Art/`, `Assets/Scenes/`, etc.
- **Data-Driven:** Keep content (stats, loops) out of logic. Use ScriptableObjects or clean data structures.
- **Patterns:** Prefer Composition over deep inheritance. Use C# events/delegates for upward communication.

## 4. Core Gameplay Flows

### Flow A: Active Combat Loop (Manual & Auto)
1. **Input (View):** Player clicks an enemy or triggers a skill -> `CombatView.cs` catches input and forwards the action command to `ActiveSim.cs` (Core).
2. **Logic (Core):** `ActiveSim.cs` intercepts the command (overriding or weaving into auto-combat choices) and uses `CombatMath.cs` to instantly calculate hits/damage.
3. **Loot & XP (Core):** On target death (`health <= 0`), `DropsSystem.cs` rolls loot (`DropData.cs`) and XP is applied by `Progression.cs`; item loot spawns as a ground bag and reaches the inventory through player pickup or AutoLoot vacuum.
4. **Write (Data):** Updates written directly to `StateData.cs` and `InventoryData.cs`.
5. **Visuals (View):** Core fires `OnEnemyKilled` or `OnSkillExecuted` events -> `CombatView.cs` and UI immediately render sprites, animations, and damage popups.

### Flow B: Resource Gathering Loop
1. **Input (View):** Click node -> `LifeSkillsView.cs` passes intent to `LifeSkills.cs` (Core).
2. **Simulation (Core):** `LifeSkills.cs` runs a headless, timestamp-based progress calculation.
3. **Reward (Core):** On interval success, `Inventory.cs` and `Progression.cs` award items/XP.
4. **Write (Data):** Saved directly to `LifeSkillsData.cs` and `InventoryData.cs`.
5. **UI (View):** `OnResourceGathered` event fires -> UI renders updated counts.

### Flow C: Snapshot-Based Offline Simulation
1. **Auth (Manager):** Character select -> `AccountManager.cs` triggers login.
2. **Delta (Manager):** `SaveLoadManager.cs` loads the character snapshot from `AccountData.cs` and calculates the total offline time delta against the system clock.
3. **Blueprint Formula (Core):** Instead of simulating individual fights, `Offline.cs` evaluates the character's static snapshot stats (Damage, Speed, Map Density) to determine a fixed gain rate (e.g., Kills per Hour).
4. **Bulk Loot/gains Transaction (Core & Data):** The engine multiplies the rate by hours to get total kills and experience or resources gained ($N$). Instead of looping every individual kill, `DropsSystem.cs` executes a high-performance statistical bulk-roll for each item on the loot table based on its flat OSRS-style drop rate ($P$) without any pity systems. The accumulated items and XP are written to the `Data` models in a single transaction.
5. **Display (View):** `SaveLoadManager.cs` generates a delta summary log -> UI displays the offline rewards screen to the player.
