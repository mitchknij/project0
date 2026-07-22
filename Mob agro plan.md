# Mob Aggro, Tile Occupancy, Positioning and Respawn Plan

> Revision: incorporated secondary architecture review covering alert cascades, player-death behavior, starvation resistance, unreachable semantics, deterministic testing, encounter authoring, primary mob skills and stale reservation cleanup.

## Purpose

Implement a reliable, tile-based mob combat behavior system for IdleCloud in which mobs:

- acquire aggro through proximity or being attacked;
- alert nearby mobs that belong to the same encounter group;
- pursue and attack the player using the existing tile and skill systems;
- never occupy or reserve the same tile as another mob or the player;
- form compact groups around the player, making AoE skills naturally valuable;
- intelligently search for free attack positions and wait when no position is available;
- respect encounter boundaries and return home when leaving them;
- provide clear, lightweight aggro and de-aggro feedback;
- respawn safely for repeatable idle farming.

The implementation should extend the existing architecture instead of creating a second movement, targeting or attack system specifically for mobs.

---

## Confirmed Design Decisions

### Aggro acquisition

- A mob gains aggro when the player enters its aggro radius or attacks the mob.
- When attacked, a mob alerts nearby mobs belonging to the same encounter group and within the configured help radius.
- Group alerting is single-hop: only the mob that was directly attacked initiates the alert. Mobs aggroed through an alert do not propagate that alert again. This prevents chain reactions across an entire pocket.
- Proximity detection uses a short detection delay, approximately 0.3 to 0.5 seconds, rather than triggering instantly.
- The player is the only supported target in this first implementation.

### Target selection and retention

- A mob targets the nearest valid player target. With one player, this remains simple but leaves a clean selection boundary.
- A mob drops its target when:
  - the player dies;
  - the player is confirmed unreachable;
  - the player leaves the encounter or pocket boundary.
- On player death, the mob clears its target and enters `ReturningHome` unless the mob is already at home, in which case the mob returns to `Idle`.
- `Unreachable` means that no path can be found to any valid attack tile after a configurable small number of controlled replanning attempts, or that the target is outside the encounter leash. A single transient path failure must not immediately clear aggro.
- Do not add a threat or damage-ranking system yet.

### Tile occupancy

- Every mob must occupy a unique tile.
- A mob must reserve its destination tile before starting movement.
- The first successful reservation wins. Other mobs must recalculate or select an alternative tile.
- Occupied and reserved tiles block other mobs.
- Mobs may not move through each other.
- Mobs may never occupy the player's tile.
- A dead mob releases gameplay occupancy immediately, even if its corpse animation remains visible.

### Positioning around the player

- Candidate attack positions are scored using a simple deterministic evaluation:
  1. reachable;
  2. valid for the selected skill;
  3. unoccupied and unreserved;
  4. as close as practical;
  5. deterministic tie-breaker.
- If all immediate attack tiles are occupied, the mob attempts to route around the group toward another free attack position.
- If no valid attack position can be reached, the mob waits as fallback.
- Waiting mobs periodically reconsider positions with a small randomized reaction delay so all mobs do not react on the same frame.
- The retry jitter must use the project's seedable or injectable randomness boundary so automated tests remain reproducible.
- `WaitingForPosition` must not become a permanent deadlock. When an attack tile is released, eligible waiting mobs are reconsidered fairly using a stable queue, age bonus or equivalent starvation-resistant rule. Do not rely solely on whichever mob updates first every frame.
- Melee mobs should cluster compactly around the player.
- Ranged mobs should maintain a preferred distance rather than moving directly adjacent to the player.

### Attack execution

- Mobs use the same general skill validation and execution pipeline as the player.
- In the first implementation, each mob uses one configured primary combat skill. Do not introduce a full mob skillbar or autonomous multi-skill priority system. The design may preserve a clean extension point for later mob loadouts without building that feature now.
- A mob may attack only when its target and selected skill are valid under the shared rules.
- When outside attack range, the mob moves toward the closest free tile that permits the selected attack.
- Standard attacks lock movement while executing.
- Hit timing depends on skill type:
  - melee damage occurs at the configured impact moment;
  - projectile damage occurs through the existing projectile impact path;
  - instant skills may resolve immediately.

### Pocket and leash behavior

- Mobs can pursue the player between areas or pockets that exist inside the same loaded map scene.
- Mobs do not travel across Unity scene boundaries in this iteration.
- Each encounter has its own leash area.
- A mob outside its leash loses aggro and walks back toward its home or spawn position.
- During the return state, health regenerates gradually.
- The return behavior should prevent immediate aggro oscillation. Apply a short return lock or require re-entry into the valid encounter area before aggro can reactivate.

### Feedback

- On aggro acquisition, show a short red exclamation indicator above each mob that becomes aggroed.
- The indicator appears for approximately 0.5 to 1 second.
- When aggro is lost, show a short question-mark indicator.
- Every visually active mob may show its own indicator.
- Feedback is presentation-only and must not control gameplay state.

### Respawn

- Normal mobs respawn individually after a configurable fixed delay at their original spawn point.
- Respawn uses a short spawn animation or invulnerable spawn phase.
- Respawn does not require the player to look away.
- Occupancy is checked before respawn. A mob may not appear on an occupied or reserved tile.
- If the original tile is unavailable, wait and retry instead of forcing an overlap.

### Initial scope

- Support melee and ranged mobs.
- Design for approximately 25 simultaneously active mobs.
- Prioritize technically correct movement and occupancy, followed closely by clear aggro feedback.
- Use global defaults with per-mob-type overrides through existing definitions or ScriptableObjects.


---

## Terminology and Scene Authoring

Use these terms consistently:

- **Spawn tile:** the tile on which a mob is initially created and may later respawn.
- **Home tile:** the tile to which a living mob returns after losing aggro. It defaults to the spawn tile but may be overridden per placed spawn.
- **Encounter:** a configured group of mobs that shares an encounter ID, help behavior and leash boundary.
- **Leash boundary:** the allowed pursuit area for an encounter.
- **Attack tile:** a reachable tile from which the mob's configured primary skill is valid against the player.

### Authoring convention

- Each combat area contains one lightweight `EncounterController` or the existing project equivalent.
- The encounter owns a stable encounter ID, leash boundary, global defaults and a collection of mob spawn points.
- A mob is assigned to an encounter explicitly through its spawn point or Inspector reference. Do not infer encounter membership solely from physical proximity at runtime.
- Spawn points define at least the mob definition, spawn tile, optional home-tile override and per-spawn overrides where already supported by the project.
- The encounter may auto-register child spawn points in the Editor if that matches existing project conventions, but runtime behavior must still have explicit, validated ownership.
- Missing encounter assignments, duplicate IDs, invalid home tiles and spawn tiles outside the leash should produce clear validation warnings.

---

## Architecture Direction

The coding agent should inspect and integrate with the existing systems before introducing new abstractions. Reuse the current equivalents of:

- tile/grid coordinates;
- pathfinding or movement planning;
- skill definitions;
- target validation;
- skill execution;
- entity health and death;
- spawn definitions;
- Inspector-facing mob definitions.

Do not assume the exact class names in this plan exist. Adapt naming to the current codebase.

### Recommended responsibilities

#### Mob aggro component or state

Owns:

- current aggro state;
- configured aggro radius and detection delay;
- current target reference;
- encounter-group alerting;
- target invalidation;
- transition into pursuit or return.

It should not calculate paths, reserve tiles or apply damage directly.

#### Encounter controller

Owns:

- encounter identity;
- membership of mobs;
- leash boundary;
- home or spawn positions;
- single-hop group-alert propagation;
- notification or controlled reconsideration when attack tiles become available;
- optional encounter-wide state later.

Keep the first version lightweight. Do not turn this into a general quest or wave framework.

#### Tile occupancy and reservation service

Owns the authoritative mapping of:

- current entity occupancy;
- destination reservations;
- reservation acquisition and release;
- cleanup on cancellation, death, despawn and scene unload.

Reservation operations must be deterministic and safe when multiple mobs request the same tile during the same update cycle. Reservations should be owner-bound and automatically invalidated when the owner is destroyed, disabled, despawned or unloaded. Add a conservative reservation lease or stale-owner cleanup as a safety net, but do not use a short blind timeout that can expire during valid long movement.

#### Attack-position resolver

Given a mob, target and candidate skill, returns the best reachable tile from which the skill can be executed.

The resolver should:

- use the shared skill range and validity rules;
- exclude occupied and reserved tiles;
- support melee and ranged preferred-distance behavior;
- return a failure reason when no position exists;
- avoid frame-by-frame oscillation between equally valid tiles.

#### Mob combat state machine

Use explicit states or an equivalent clearly inspectable model:

```text
Idle
Detecting
Pursuing
MovingToAttackPosition
Attacking
WaitingForPosition
ReturningHome
Dead
Respawning
```

The existing project architecture may already have a state model. Extend that model rather than adding a parallel framework.

---

## Implementation Phases

### Phase 1: inspect and document integration points

Before changing behavior:

1. Locate the current mob movement, pathfinding, skill execution, health/death and spawn flows.
2. Identify the authoritative grid coordinate and tile walkability source.
3. Determine whether occupancy or destination reservations already exist.
4. Identify which mob parameters already live in Inspector-facing definitions.
5. Record the smallest compatible extension plan in code comments or existing project documentation.

Do not refactor unrelated systems during this phase.

### Phase 2: authoritative tile occupancy

Implement or complete:

- unique occupied tile per living entity;
- destination reservation before movement;
- first-reservation-wins behavior;
- reservation release after arrival, cancellation, path failure, death, disable, despawn or scene unload;
- owner-bound stale-reservation cleanup or a conservative reservation lease as a safety net;
- player tile exclusion;
- immediate gameplay release on mob death;
- debug visualization for occupied and reserved tiles if the project has a debug overlay pattern.

This phase must be stable before group positioning is added.

### Phase 3: aggro and encounter groups

Implement:

- proximity detection with configurable short delay;
- aggro from being attacked;
- single-hop alerting of nearby members of the same encounter group and within the help radius;
- player-only targeting;
- player-death transition to `ReturningHome` or `Idle` when already home;
- target invalidation on confirmed unreachable state or boundary exit;
- aggro and de-aggro presentation events.

Group alerting should use both encounter membership and a configurable help radius. Do not alert unrelated mobs merely because they are nearby.

### Phase 4: attack-position selection

Implement candidate generation and scoring for:

- melee adjacency or skill-valid tiles;
- ranged minimum, preferred and maximum range;
- reachable, free and reservable tiles;
- deterministic tie-breaking;
- route-around behavior when the nearest edge is blocked;
- waiting fallback when no valid position exists;
- staggered reconsideration intervals using seedable randomness;
- starvation-resistant allocation when an attack tile becomes available.

Avoid expensive full-map searches on every frame. Recalculate on meaningful events or controlled intervals, such as:

- target movement to another tile;
- destination becoming invalid;
- skill choice changing;
- path failure;
- an attack position being released;
- waiting retry timer expiring.

### Phase 5: shared skill execution

Connect mobs to the existing skill pipeline using one configured primary combat skill per mob type:

- validate target and range through shared rules;
- move to the closest suitable execution tile when needed;
- lock movement during standard attacks;
- trigger melee, projectile or instant impact through existing mechanisms;
- resume position evaluation after attack completion or target movement.

Do not create a mob-only damage calculator or duplicate cooldown implementation.

### Phase 6: leash and return behavior

Implement encounter-boundary handling:

- detect when pursuit exceeds encounter leash limits;
- clear target and reservations;
- enter ReturningHome;
- path back to the configured home tile;
- regenerate health gradually while returning;
- temporarily suppress reacquisition to prevent boundary oscillation;
- return to Idle after reaching home and restoring the expected state.

Mobs may follow the player between sections of the same loaded scene when those sections remain inside the encounter's permitted area.

### Phase 7: respawn

Implement individual farming respawn:

- record an authoritative spawn or home tile;
- schedule respawn after a configurable delay;
- verify the spawn tile is free;
- retry later when blocked;
- restore runtime state cleanly;
- play a short spawn phase or animation;
- prevent attacks and damage during the configured spawn-protection window.

### Phase 8: visual feedback and Inspector pass

Expose only practical designer-facing values, with global defaults and mob-type overrides:

- aggro radius;
- aggro detection delay;
- encounter help radius;
- configured primary combat skill;
- movement speed;
- leash configuration or encounter assignment;
- return health regeneration rate;
- melee or ranged positioning profile;
- ranged minimum, preferred and maximum distance;
- position retry interval, jitter and maximum replanning attempts before `Unreachable`;
- respawn delay;
- spawn protection duration;
- aggro and de-aggro indicator references.

Add helpful grouping, tooltips and validation. Avoid exposing internal runtime state as editable values.

---

## Suggested Default Values

These are starting values, not hardcoded rules:

```text
Aggro detection delay: 0.4 seconds
Aggro indicator duration: 0.75 seconds
Waiting position retry: 0.35 seconds
Retry jitter: 0.0 to 0.2 seconds, seedable in tests
Maximum failed replanning attempts before Unreachable: 3
Respawn delay: 8 to 15 seconds, configurable per mob type
Spawn protection: 0.5 to 1.0 seconds
Maximum expected active mobs: 25
```

Use existing game units and timing conventions where available.

---

## Key Edge Cases

The implementation must explicitly handle:

- two or more mobs selecting the same destination in one update;
- target death while a mob is moving or attacking;
- mob death while holding a destination reservation;
- path becoming blocked after reservation;
- player moving before a melee mob reaches its chosen tile;
- all attack positions around the player being occupied;
- ranged mob being closer than its preferred distance;
- mob leaving the leash while chasing;
- player re-entering the leash during mob return;
- player dying while mobs are detecting, pursuing, moving or attacking;
- one attacked mob alerting a group without creating a multi-hop alert cascade;
- a waiting mob repeatedly losing tile claims and requiring starvation protection;
- a stale reservation whose owner was disabled or destroyed unexpectedly;
- respawn tile remaining occupied;
- scene unload while mobs own occupancy or reservations;
- twenty-five mobs reconsidering positions simultaneously;
- corpse visuals remaining after occupancy has been released.

---

## Acceptance Criteria

### Aggro

- A mob enters detection when the player remains within aggro range for the configured delay.
- Attacking a mob causes immediate aggro.
- Eligible nearby mobs in the same encounter group and within the help radius are alerted.
- Alerting is single-hop and cannot cascade across the pocket.
- Unrelated mobs are not alerted.
- A single transient path failure does not clear aggro.
- Confirmed unreachable state follows the configured replanning rule.
- On player death, mobs clear the target and return home, or become idle immediately when already home.
- Aggro is dropped when the player is confirmed unreachable or leaves the encounter boundary.

### Occupancy

- No two living mobs can occupy the same tile.
- No two moving mobs can hold the same destination reservation.
- Mobs cannot move through occupied or reserved mob tiles.
- Mobs never occupy the player's tile.
- Reservations are always released after arrival, cancellation, path failure, death, disable, despawn or scene unload.
- Stale reservations cannot remain indefinitely after their owner disappears.

### Positioning

- Melee mobs form a compact ring or cluster on valid attack tiles.
- Ranged mobs attempt to maintain their preferred distance.
- Blocked mobs route toward another valid attack position where possible.
- Mobs wait instead of stacking when no valid position exists.
- Waiting mobs reconsider without all updating on the same frame.
- Reconsideration randomness is reproducible in automated tests.
- A repeatedly waiting mob eventually receives a newly available suitable position and cannot starve indefinitely.

### Combat

- Each mob uses its configured primary combat skill through the shared skill validation, cooldown and damage/execution systems.
- Out-of-range mobs move to the closest free valid attack tile.
- Standard attacks prevent movement during execution.
- Melee, projectile and instant attack impacts follow their existing skill-type rules.

### Leash and return

- Encounter boundaries are authoritative.
- Mobs outside the leash stop pursuing and walk home.
- Returning mobs regenerate health gradually.
- Returning mobs do not rapidly toggle between aggro and return at the boundary.

### Feedback

- Every newly aggroed visible mob briefly shows a red exclamation indicator.
- A mob losing aggro briefly shows a question-mark indicator.
- Indicators do not affect gameplay logic.

### Respawn

- Normal mobs respawn individually after their configured delay.
- A mob does not respawn onto an occupied or reserved tile.
- Respawn retries safely when blocked.
- A spawned mob receives its configured short spawn-protection phase.

### Quality

- Behavior remains stable with approximately 25 active mobs.
- No new console errors or leaked reservations occur during repeated combat, death, return and respawn cycles.
- Relevant parameters are configurable through the Inspector using global defaults plus mob-type overrides.
- Existing player skill, movement and targeting behavior remains functional.

---

## Test Scenarios

Create or extend a focused combat test scene with at least:

1. One melee mob entering proximity aggro.
2. One mob aggroed by an attack from outside its detection radius.
3. An encounter group where one attacked mob alerts nearby group members.
4. Eight melee mobs surrounding the player without overlapping.
5. More melee mobs than available adjacent attack tiles, proving route-around and waiting behavior.
6. Several ranged mobs maintaining preferred distance.
7. Two mobs attempting to reserve the same tile.
8. A player leaving the encounter boundary and mobs returning home.
9. A returning mob gradually healing and not immediately reacquiring the player.
10. Mob death during movement and attack, proving reservation cleanup.
11. A blocked spawn tile, proving delayed respawn retry.
12. A single-hop group alert scenario proving that alerted mobs do not propagate the alert further.
13. Player death while mobs are pursuing and attacking, proving clean return transitions.
14. Repeated contention for one released attack tile, proving starvation resistance.
15. Seeded retry jitter producing reproducible test outcomes.
16. Forced removal or disable of a reservation owner, proving stale-reservation cleanup.
17. A stress case with approximately 25 mixed melee and ranged mobs.

Where practical, add deterministic edit-mode tests for occupancy, reservation and attack-position scoring, plus play-mode tests for integrated movement and state transitions.

---

## Explicitly Out of Scope

Do not include the following in this implementation:

- mobs travelling between separately loaded Unity scenes;
- multiplayer or multiple player targets;
- summons, friendly NPC targeting or faction systems;
- threat tables or damage-based target ranking;
- a full mob skillbar, autonomous multi-skill priority system or dynamic skill loadout selection;
- line-of-sight, projectile obstruction or cover rules unless an already-existing shared validator requires them;
- advanced formations;
- crowd pushing or collision-based displacement;
- large mobs occupying multiple tiles;
- flying movement rules;
- boss-specific AI phases;
- a general wave, quest or encounter scripting framework;
- predictive surround tactics beyond the simple attack-position resolver;
- saving and restoring exact mid-combat AI, health, paths, reservations or aggro state;
- premature optimization for more than approximately 25 active mobs.

---

## Delivery Expectations

The coding agent should deliver:

1. The integrated mob aggro and combat-positioning implementation.
2. Required changes to occupancy/reservation behavior.
3. Inspector-facing configuration and explicit encounter/spawn authoring using existing project conventions.
4. Validation for missing encounter ownership, invalid home/spawn tiles and duplicate encounter IDs.
5. Red exclamation and question-mark presentation hooks, using placeholders if final art is unavailable.
6. A focused test scene or repeatable test setup.
7. Automated tests for the deterministic core where compatible with the existing test architecture.
8. A concise summary of changed files, architectural decisions and any remaining limitations.

The implementation should stay modular and practical, but must not introduce a broad new framework unless the existing codebase demonstrably requires it.
