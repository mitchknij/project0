# IdleCloud — Prioritized Board Roadmap

Source: Trello-board screenshot supplied on 2026-07-19. This is a planning
backlog, not implementation approval. Priorities are based on building a
playable idle-RPG vertical slice before expanding content or presentation.

## Priority model

| Priority | Meaning |
| --- | --- |
| P0 | Product or technical decision that blocks dependable development. |
| P1 | Required for the first end-to-end playable combat loop. |
| P2 | Required for durable progression, rewards, and repeatable play. |
| P3 | Content depth and usability that make the loop complete. |
| P4 | Balancing, visual polish, and post-loop expansion. |

## Delivery order

1. **Foundation (P0):** establish the game's identity, platform target,
   persistence contract, and authoring-safe item identities.
2. **Playable combat (P1):** make selection, movement/range, resources,
   damage, death, and feedback operate as one reliable loop.
3. **Retention loop (P2):** connect XP, skills, equipment, inventory, drops,
   saving, and offline claims into a complete reward cycle.
4. **World and interface (P3):** add class identity, enemy behaviour, maps,
   menus, accessibility, and core life-skill interaction.
5. **Depth and polish (P4):** add boss systems, advanced drops, loadouts,
   visual language, balancing, and optional systems.

## P0 — Foundation and non-negotiable decisions

### P0-01 — Define the game name

**Definition:** Choose the public and in-game name, then define its usage in
the title screen, save namespace, package metadata, and project documentation.

**Acceptance criteria:**

- One approved name is recorded with a short naming rationale.
- Title-screen copy, application metadata, and save identifier use that name.
- A rename/migration decision exists if existing saves used another identifier.

### P0-02 — Define the robot's role

**Definition:** Decide whether the robot is the player character, companion,
enemy, narrator, or a world-system element; define its gameplay purpose.

**Acceptance criteria:**

- A one-page role brief states narrative purpose, player interaction, and art
  requirements.
- The role does not conflict with the selected core fantasy or player role.
- At least one first-map encounter or presentation moment demonstrates it.

### P0-03 — Establish the core fantasy

**Definition:** State the repeatable fantasy the player experiences, such as
becoming a powerful explorer through active combat and idle advancement.

**Acceptance criteria:**

- A concise fantasy statement names the player, action, setting, and reward.
- The first playable loop can be traced directly to that statement.
- Major systems in this roadmap are checked for support of the fantasy.

### P0-04 — Define world and lore pillars

**Definition:** Establish three to five durable setting principles that guide
biomes, factions, classes, enemies, item flavour, and visual tone.

**Acceptance criteria:**

- Pillars include a one-sentence meaning and one gameplay implication each.
- First-map, class, monster, and item concepts conform to the pillars.
- Contradictory or out-of-scope concepts are explicitly deferred or rejected.

### P0-05 — Define the player’s role in the world

**Definition:** Specify why the player fights, gathers, travels, and improves;
connect the account and character model to the fiction.

**Acceptance criteria:**

- The role explains the player's initial location, immediate objective, and
  reason to return after offline progress.
- Character classes and account-wide progression have a clear fiction.
- The opening UI and first quest/tutorial communicate the role.

### P0-06 — Align lore, map, and game design

**Definition:** Convert the narrative pillars into map rules, enemy themes,
resources, progression gates, and encounter purposes.

**Acceptance criteria:**

- A first-map design sheet links each zone, enemy, resource, and exit to a
  lore pillar and gameplay purpose.
- No first-map feature exists only as decoration without a deliberate decision.
- The sheet is the authoring reference for subsequent map work.

### P0-07 — Select production platforms

**Definition:** Decide supported launch platforms, input methods, performance
budgets, display targets, and save-location expectations.

**Acceptance criteria:**

- Supported and explicitly unsupported launch platforms are recorded.
- Input, UI scaling, performance, and persistence requirements are listed per
  supported platform.
- Build and test gates match the selected platform plan.

### P0-08 — Define stable item definitions and IDs

**Definition:** Create a data-authoring contract for every item: immutable ID,
display data, stack rule, category, rarity, and save compatibility behaviour.

**Acceptance criteria:**

- Each shipped item has a stable non-display ID that survives renaming.
- Item data validates duplicate/missing IDs before a session begins.
- A save containing an item remains loadable after display-name changes.

### P0-09 — Implement and validate save/load

**Definition:** Persist account, character, inventory, equipment, progression,
map/activity snapshot, and offline timestamps through an explicit schema.

**Acceptance criteria:**

- A save/load round trip restores all supported player state exactly.
- Corrupt, missing, and old-version saves produce a controlled recovery path.
- Save compatibility tests cover the oldest supported schema and current schema.

### P0-10 — Specify offline-progression calculation

**Definition:** Finalize the snapshot-based formula: active rates are captured,
elapsed time is bounded, and rewards are bulk-calculated deterministically.

**Acceptance criteria:**

- The formula, caps, anti-clock-abuse policy, and eligible activities are
  documented.
- The same snapshot, seed, and elapsed time yield identical rewards.
- No offline calculation simulates individual combat ticks or individual kills.

### P0-11 — Define baked offline-claim flow

**Definition:** Decide how calculated offline rewards are presented, claimed,
idempotently recorded, and recovered after interruption.

**Acceptance criteria:**

- A returning player sees an itemized reward summary before rewards are lost.
- Claiming twice cannot duplicate rewards.
- Closing during claim and reloading yields either one completed claim or one
still-pending claim, never an ambiguous state.

## P1 — First playable combat loop

### P1-01 — Polish scene loading

**Definition:** Make Bootstrap, PersistentGame, and map loading reliably show
the correct player, UI, camera, and world without duplicate singletons.

**Acceptance criteria:**

- Initial launch reaches the first playable map without missing references.
- Map transition preserves persistent systems and disposes map-only objects.
- A loading/error state is visible when a requested scene cannot load.

### P1-02 — Complete target selection

**Definition:** Provide deterministic click/tap targeting with a visible
selection state, invalid-target handling, and auto-combat interoperability.

**Acceptance criteria:**

- Selecting a valid visible enemy updates the logical combat target and view.
- Invalid clicks clear or preserve target according to a documented rule.
- Manual selection and auto-targeting never create conflicting target state.

### P1-03 — Support skill range and movement behaviour

**Definition:** Define skill range, approach distance, and kiting behaviour so
auto-combat moves deliberately rather than oscillating or casting out of range.

**Acceptance criteria:**

- Each skill declares its usable range and movement policy.
- Auto-combat approaches into range, casts, and respects a retreat/kite rule
when applicable.
- The movement loop has a bounded retry/fallback when pathing fails.

### P1-04 — Add range to skills

**Definition:** Make range an authored skill property used consistently by
target validation, movement decisions, area targeting, and UI.

**Acceptance criteria:**

- Range is inspector/content-authored rather than hard-coded per cast path.
- A cast outside range is rejected or causes the documented movement action.
- UI communicates when a selected target is outside usable range.

### P1-05 — Define line-of-sight and obstruction policy

**Definition:** Decide whether walls, terrain, elevation, and map pockets block
attacks; implement only the policy needed for the first map.

**Acceptance criteria:**

- The first map has documented obstructing and non-obstructing tile/object
types.
- Target/cast validation follows the rule consistently for manual and auto play.
- If deferred, all current maps are explicitly authored as no-LOS maps.

### P1-06 — Implement damage and critical hits

**Definition:** Resolve base damage, attack/skill scaling, critical chance,
critical multiplier, and rounding in headless Core logic.

**Acceptance criteria:**

- Given fixed stats and random seed, damage and critical results are exact and
repeatable.
- Damage output identifies normal versus critical hit for presentation.
- Tests cover minimum/maximum damage, zero damage, and critical boundaries.

### P1-07 — Add defence and mitigation

**Definition:** Define armour/resistance/other defences and their ordering in
incoming-damage resolution.

**Acceptance criteria:**

- Every damage type either has a documented mitigation path or is intentionally
unmitigated.
- Mitigation cannot produce negative received damage.
- Combat feedback can explain the final received-damage value.

### P1-08 — Add mana and resource rules

**Definition:** Introduce mana or the selected resource, costs, regeneration,
and the rules for failed casts.

**Acceptance criteria:**

- Skills declare resource cost in authored content.
- A player cannot spend below zero or cast a skill without its cost.
- Regeneration, costs, and failure feedback are visible during play.

### P1-09 — Polish cooldown and resource interactions

**Definition:** Establish cooldown start timing, global/individual cooldown
behaviour, resource checks, and UI state precedence.

**Acceptance criteria:**

- A skill’s availability is computed from one authoritative runtime state.
- Cooldown, insufficient-resource, and range states have distinct feedback.
- Auto-combat skips unavailable skills without changing slot priority semantics.

### P1-10 — Add death and respawn

**Definition:** Define player defeat, enemy death cleanup, respawn location,
penalty/recovery rules, and combat reset.

**Acceptance criteria:**

- Player and enemy death remove invalid combat targets and active actions.
- Respawn restores a valid controllable state at a defined safe location.
- Any death penalty is applied once and is visible to the player.

### P1-11 — Add combat feedback

**Definition:** Present attacks, damage, criticals, misses/blocks where used,
casts, kills, and invalid actions without putting gameplay logic in View/UI.

**Acceptance criteria:**

- Each combat result emits a presentation-safe event or diagnostics payload.
- Feedback appears for manual and auto actions without changing simulation state.
- Repeated combat does not leak feedback objects or duplicate visual events.

### P1-12 — Add status effects and indicators

**Definition:** Define a minimal status-effect model (duration, stacking,
source, modifier) and show active effects on the relevant combatants.

**Acceptance criteria:**

- Effects have deterministic apply, refresh, expire, and removal semantics.
- Stack caps and mutually exclusive effects are content-defined.
- UI/View shows effect identity and remaining duration where appropriate.

## P2 — Retention, rewards, and progression

### P2-01 — Polish character XP and levels

**Definition:** Make character XP gain, level-up processing, rewards, and
presentation robust across combat, gathering, and offline claims.

**Acceptance criteria:**

- XP is awarded once per eligible reward event.
- Multi-level gains process every intermediate reward in order.
- UI and saved state agree on current level and XP progress.

### P2-02 — Define the XP curve

**Definition:** Author the XP requirements and pacing targets per level and
validate they support the intended session and idle cadence.

**Acceptance criteria:**

- The curve is data-authored and has no missing or non-increasing thresholds.
- A pacing table states expected time-to-level across the first progression arc.
- Curve changes preserve or deliberately migrate existing XP totals.

### P2-03 — Implement skill levels

**Definition:** Give skills individual level/proficiency state, earned XP or
use criteria, and level-dependent benefits.

**Acceptance criteria:**

- Skill progress is tracked independently from character XP.
- Level changes use authored thresholds and effects.
- Save/load and offline reward paths preserve skill-level progress.

### P2-04 — Implement unlocks

**Definition:** Build a single unlock evaluator for skills, maps, pockets,
equipment, and systems using explicit requirements and reasons.

**Acceptance criteria:**

- Locked content exposes the unmet requirement to UI.
- Unlock evaluation is deterministic and independent of presentation code.
- A newly met requirement unlocks content once and persists after reload.

### P2-05 — Polish stat growth

**Definition:** Define growth from levels, class, equipment, and temporary
effects, then expose one authoritative computed-stat view to systems.

**Acceptance criteria:**

- Base, additive, multiplicative, and temporary modifiers have a documented
order.
- Combat, UI, and offline calculation use the same resulting stats.
- Removing an item/effect recalculates affected stats without stale values.

### P2-06 — Implement class progression

**Definition:** Add class-specific advancement, milestones, and skill access
while preserving the multi-character account model.

**Acceptance criteria:**

- Each class has authored progression milestones and valid starting content.
- Class-only unlocks cannot be claimed by an incompatible character.
- Switching characters does not leak class runtime state.

### P2-07 — Implement equipment progression

**Definition:** Make equipment acquisition and upgrades materially alter the
computed stats and combat performance in predictable ways.

**Acceptance criteria:**

- Equipping/unequipping updates calculated stats and UI immediately.
- Equipment requirements and upgrade limits are validated.
- Save/load restores equipped items only when their IDs remain valid.

### P2-08 — Define account versus character progression

**Definition:** Classify every progression value as account-wide or
character-specific and define sharing/migration rules.

**Acceptance criteria:**

- A data ownership table covers currency, bank, unlocks, XP, equipment, and
map access.
- New characters inherit only declared account-wide benefits.
- Save validation rejects accidental duplication of account-only resources.

### P2-09 — Implement inventory capacity

**Definition:** Define slots, weight, or another capacity model and resolve how
loot behaves when capacity is reached.

**Acceptance criteria:**

- Capacity is visible and derived from authoritative inventory state.
- Pickup, crafting, rewards, and offline claims use the same full-inventory
rule.
- Full inventory never silently destroys a reward.

### P2-10 — Implement item stacking

**Definition:** Merge compatible item stacks using authored maximums and stable
item IDs.

**Acceptance criteria:**

- Stackable items merge up to their authored maximum.
- Overflow creates valid additional stacks or follows the capacity rule.
- Non-stackable items never merge solely because presentation data matches.

### P2-11 — Implement currencies

**Definition:** Define currency types, ownership, transaction sources, and
auditable add/spend rules.

**Acceptance criteria:**

- Every currency transaction names a reason and cannot take a balance negative.
- Account-shared versus character-local currency follows P2-08.
- Currency changes survive save/load and are represented in UI.

### P2-12 — Polish equipment slots

**Definition:** Define valid slots, equip restrictions, swap behaviour, and
the UI contract for inspecting and changing equipment.

**Acceptance criteria:**

- Invalid item/slot combinations are rejected with clear feedback.
- Swapping moves displaced equipment according to inventory-capacity rules.
- All equipped-slot changes update stats, save state, and UI consistently.

### P2-13 — Define item stats, colour, and rarity

**Definition:** Establish an item-stat schema and rarity language inspired by
the desired ARPG feel without copying another game's content rules.

**Acceptance criteria:**

- Rarity and stat rolls are data-authored and identifiable from stable item data.
- Colour is presentation derived from rarity, not the source of rarity truth.
- Tooltips show all gameplay-relevant rolled and fixed stats.

### P2-14 — Implement upgrades

**Definition:** Add the chosen item/character upgrade path, costs, limits,
success/failure policy, and stat effects.

**Acceptance criteria:**

- An upgrade validates ownership, cost, eligibility, and maximum level.
- The transaction is atomic: it cannot charge without applying the result.
- Upgrade outcomes persist and appear in computed stats/tooltips.

### P2-15 — Implement consumables

**Definition:** Add usable items with target rules, cooldowns/effects, stack
consumption, and failure feedback.

**Acceptance criteria:**

- Use consumes exactly one item only after validation succeeds.
- Invalid use leaves the inventory unchanged and explains why.
- Effects integrate with the status/resource systems without View dependencies.

### P2-16 — Implement loot pickup/claim flow

**Definition:** Define how world loot, combat rewards, and offline rewards
become inventory transactions and how the player confirms them.

**Acceptance criteria:**

- Each reward can be claimed at most once.
- Claim feedback lists received, deferred, and blocked items distinctly.
- Capacity failures have a recoverable destination or explicit player choice.

### P2-17 — Implement basic drop tables

**Definition:** Give standard enemies and activities content-authored drop
tables with independently testable probabilities and quantities.

**Acceptance criteria:**

- Every eligible source resolves a valid drop table.
- Bulk/offline and active reward resolution use equivalent probability semantics.
- Missing/invalid table references fail validation before play begins.

### P2-18 — Implement currency drops

**Definition:** Add currency as a drop-table outcome with visible source and
safe account/character ownership routing.

**Acceptance criteria:**

- Currency drops use the same transactional claim path as items.
- Drop summaries show currency independently from item stacks.
- Currency ownership matches the account/character table.

### P2-19 — Define item quantity ranges

**Definition:** Author minimum/maximum quantity ranges and deterministic random
resolution for each applicable drop outcome.

**Acceptance criteria:**

- All ranges validate `min <= max` and use integer-safe boundaries.
- Fixed seed tests prove both endpoints are reachable when intended.
- Quantity is resolved before stacking/claim logic.

## P3 — World, UI, and core content

### P3-01 — Polish sorting, layering, and large objects

**Definition:** Finalize isometric sort rules for characters, enemies, terrain,
and large scenery so depth reads correctly across the first map.

**Acceptance criteria:**

- Player, enemies, and large scenery sort correctly while moving around each
other.
- Sorting is stable at tile/elevation boundaries without visible flicker.
- Authoring guidance covers required sprite pivots and sorting settings.

### P3-02 — Implement map/pocket unlocks

**Definition:** Connect unlock rules to exits, map selection, and optional
pocket areas with a clear reason for each gate.

**Acceptance criteria:**

- Locked exits show their requirement without allowing invalid travel.
- Unlocking persists and enables travel after reload.
- Map authoring can declare the required unlock without code changes.

### P3-03 — Add class specialities

**Definition:** Give each class a concise gameplay identity through passive
rules, skill patterns, resource interactions, or equipment preferences.

**Acceptance criteria:**

- Each class has at least one measurable strength and trade-off.
- Specialities affect headless gameplay calculations, not only UI text.
- Content validation ensures class-referenced skills and equipment exist.

### P3-04 — Add class visuals

**Definition:** Supply class-select, world, and combat presentation that makes
classes distinguishable while retaining shared animation/sorting conventions.

**Acceptance criteria:**

- Each playable class has visible world and UI identity assets/prefabs.
- Class visuals are assigned through Inspector/prefab references.
- Changing classes updates visuals without affecting gameplay state.

### P3-05 — Implement enemy specialities

**Definition:** Define enemy archetypes such as ranged, tank, swarm, or caster,
each with a focused combat behaviour and reward profile.

**Acceptance criteria:**

- Each speciality changes at least one combat decision or stat profile.
- Enemies remain configurable through content assets, not scene-specific code.
- The first map contains a small, readable set of distinct archetypes.

### P3-06 — Implement enemy aggro

**Definition:** Add a bounded aggro system: enemies select a player based on
distance/attack/other defined signals, pursue, respect leash limits, and avoid
unbounded stacking.

**Acceptance criteria:**

- Aggro target selection has documented tie-breaking and range rules.
- Each mob respects a per-tile/area crowd limit or chosen anti-stacking rule.
- Mobs disengage and return safely after leash/path failure.

### P3-07 — Add periodic boss mechanics

**Definition:** Introduce scheduled or recurring boss encounters with spawn,
participation, reward, and reset rules.

**Acceptance criteria:**

- Spawn timing is visible and survives scene reload according to design.
- Boss reset, defeat, and reward transactions cannot duplicate rewards.
- At least one boss mechanic uses the established combat rules.

### P3-08 — Add the star/elite system

**Definition:** Create the Valheim-inspired elite-star modifier system that
raises enemy strength and reward potential with clear visual signalling.

**Acceptance criteria:**

- Star tier modifies authored stats/rewards through the shared stat pipeline.
- A player can identify tier before engagement.
- Spawn rules bound tier frequency and are deterministic for a fixed seed.

### P3-09 — Build inventory UI polish

**Definition:** Improve the inventory panel for readable stacks, capacity,
rarity, selection, transfer, and failure feedback.

**Acceptance criteria:**

- Inventory state is readable at supported resolutions and input modes.
- UI actions dispatch validated intents; inventory logic remains outside UI.
- Capacity, stack counts, rarity, and claim failures refresh immediately.

### P3-10 — Build a start menu and character selection

**Definition:** Provide title/start flow, account loading, character creation,
selection, and safe entry into the game session.

**Acceptance criteria:**

- A player can create, select, and enter a character without scene corruption.
- Empty, missing, and invalid character data have explicit UI states.
- Selected character identity is reflected in the launched session and save path.

### P3-11 — Polish the HUD

**Definition:** Make core combat, resource, target, XP, and navigation
information scannable without obscuring the isometric play space.

**Acceptance criteria:**

- HUD presents health, resources, target, core progression, and action state.
- It remains usable at the supported resolution range.
- HUD reads state through existing manager/view models rather than owning logic.

### P3-12 — Add a mouse-position/tile indicator

**Definition:** Show which tile/world position the pointer refers to and
whether it is walkable, interactable, or targeted.

**Acceptance criteria:**

- Indicator maps screen input to the same world/tile coordinate used by input.
- Valid, blocked, and interactive locations have distinguishable feedback.
- It does not intercept pointer input or desynchronise from pathfinding.

### P3-13 — Polish the skillbar UI

**Definition:** Finalize slot readability, cooldown/resource/range state,
auto-selection feedback, keyboard bindings, drag/drop, and mobile policy.

**Acceptance criteria:**

- All configured slots are visible, selectable, and accessible by their binding.
- Auto-combat priority remains left-to-right first eligible slot.
- A bake and Play-mode check confirm hierarchy, pulse feedback, and drag/drop.

### P3-14 — Build the character UI

**Definition:** Provide a clear character summary for class, calculated stats,
level/XP, equipment, and progression choices.

**Acceptance criteria:**

- Values shown match authoritative calculated state.
- Stat modifiers can be inspected at a useful level of detail.
- The panel updates after level, equipment, and class changes.

### P3-15 — Add a settings menu

**Definition:** Offer player-configurable audio, display, controls, accessibility,
and gameplay presentation settings appropriate to chosen platforms.

**Acceptance criteria:**

- Changes apply immediately or state when a restart is required.
- Settings persist independently from character progression.
- Reset-to-default affects only settings and has a confirmation path.

### P3-16 — Handle UI scaling and resolution

**Definition:** Establish reference resolution, safe areas, scaling policy,
minimum usable size, and platform-specific layout checks.

**Acceptance criteria:**

- Core menus and HUD remain usable at every supported display target.
- No critical control is obscured by safe areas or overlaps another control.
- Scaling is verified in Unity Game view/device simulation for supported targets.

### P3-17 — Add tooltips

**Definition:** Provide contextual descriptions for items, stats, skills,
effects, currencies, locks, and controls.

**Acceptance criteria:**

- Tooltip data originates from content/state, with no duplicate gameplay rules.
- Tooltips handle pointer, keyboard/controller focus, and unavailable content.
- Long content remains readable without covering the triggering control.

### P3-18 — Add core life skills

**Definition:** Implement mining, woodcutting, and fishing as headless,
timestamp-aware activities with authored nodes, tools, rewards, XP, and
interruption rules.

**Acceptance criteria:**

- Each activity can be started from a valid world node and produces a valid
reward/XP transaction.
- Node/tool/skill requirements are data-authored and explain locked actions.
- Offline calculation uses the same activity-rate model without per-tick loops.

## P4 — Expansion, balance, and polish

### P4-01 — Implement tertiary independent drop rolls

**Definition:** Add independent per-drop tertiary rolls so a rare result does
not suppress other eligible rolls, following the intended ARPG-style rule.

**Acceptance criteria:**

- Each tertiary entry rolls independently after the declared primary logic.
- Simulation tests demonstrate co-occurring eligible drops.
- Drop summary shows all awarded results without overwriting stacks.

### P4-02 — Decide and implement pity policy

**Definition:** Choose whether rare drops use no pity, soft pity, hard pity, or
an alternative; align it with the stated loot philosophy.

**Acceptance criteria:**

- The decision and player-facing disclosure policy are documented.
- If enabled, pity state is scoped, persisted, and cannot be reset by reload.
- If disabled, no hidden pity counter is maintained.

### P4-03 — Implement boss drops

**Definition:** Create boss-specific reward tables, lockouts/eligibility, and
claim rules compatible with periodic boss encounters.

**Acceptance criteria:**

- Boss drops are distinct from standard tables and validate correctly.
- Participation/eligibility prevents duplicate or absent intended rewards.
- Rewards use the common inventory/currency claim transaction.

### P4-04 — Balance drop rates

**Definition:** Tune all drop probabilities, quantities, and expected value
against target time-to-item, progression gates, and currency sinks.

**Acceptance criteria:**

- A balancing sheet states expected rewards per hour for key activities.
- Changes are content-data changes with versioned rationale.
- Monte Carlo or deterministic bulk checks identify impossible/extreme tables.

### P4-05 — Implement the eight-button skillbar milestone

**Definition:** Complete the eight-slot skillbar authoring, save normalization,
hotkeys, auto-priority scan, and transient auto-cast highlight.

**Acceptance criteria:**

- Existing four-slot saves normalize to eight slots without losing assignments.
- Hotkeys 1–8, drag/drop, and auto-combat work across all slots.
- Unity EditMode and Play-mode checks validate repeat-cast pulse behaviour.

### P4-06 — Add small omnidirectional tornado skill

**Definition:** Implement a small tornado-like skill that executes its area
pattern in all intended directions, damaging any enemy caught in the pattern.

**Acceptance criteria:**

- Casting always performs the visual/action even when no target is hit.
- Pattern resolution is deterministic and can hit enemies in every configured
direction.
- Damage uses the shared combat resolver and respects all normal validation.

### P4-07 — Create skill visuals

**Definition:** Add prefab/Inspector-authored effects, animation, sound hooks,
and feedback for skills without placing gameplay decisions in presentation.

**Acceptance criteria:**

- Each first-slice skill has a distinct cast and impact/readability cue.
- Visuals subscribe to established action/result events and do not alter damage.
- Effects clean up safely after interruption, target death, or scene unload.

### P4-08 — Couple skills to classes

**Definition:** Define which skills are class-exclusive, shared, granted,
unlocked, or loadout-selectable.

**Acceptance criteria:**

- Skill eligibility uses class and unlock data, not UI-only filtering.
- Invalid class/skill combinations cannot be equipped or cast.
- Character creation and migration seed only approved starter skills.

### P4-09 — Polish cooldown presentation

**Definition:** Improve the cooldown display, readiness feedback, queuing rule,
and accessibility readability for every skill slot.

**Acceptance criteria:**

- Cooldown fill/timer matches authoritative remaining time.
- Ready, cooling down, out-of-resource, and out-of-range states are distinct.
- The display is readable without relying only on colour.

### P4-10 — Implement skill loadouts

**Definition:** Allow a character to save and switch validated skillbar
arrangements under defined combat and unlock restrictions.

**Acceptance criteria:**

- A loadout stores only currently legal unlocked skills and slot positions.
- Switching obeys the defined combat, cooldown, and resource restrictions.
- Loadouts persist per character and cannot overwrite another character’s data.

### P4-11 — Add skill-progression hooks

**Definition:** Connect skill levels to authored bonuses such as damage,
cooldown, cost, area, or utility, with clear discoverability.

**Acceptance criteria:**

- Each supported level benefit is data-authored and applied once.
- Calculated skill values are visible in the skill tooltip/details UI.
- Level changes update active and offline rate calculations consistently.

## Suggested next implementation slice

Start with P0-03 through P0-11, then execute P1-01 through P1-11 as one
vertical slice. Do not expand bosses, pity, advanced rarity, loadouts, or broad
visual polish until a new character can enter a map, fight, die/respawn, earn a
drop and XP, save, return, and receive one idempotent offline claim.

## Existing-project follow-up before expansion

The current project state also records two prerequisites that should be cleared
before adding new systems: verify the eight-slot skillbar in the Unity Editor,
and diagnose the `skill_asset_missing_id` Ground Smash content-authoring error.
These are tracked project follow-ups rather than cards read from the screenshot.
