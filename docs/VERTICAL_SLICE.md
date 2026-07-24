# Vertical Slice Specification

## Goal

Prove that the complete expedition loop is enjoyable, understandable, and technically viable before expanding content or assigning parallel agent work.

## Implementation status

The documented vertical slice is feature-complete in the active PlayCanvas build:

- Route-map selection and confirmation lead into the rendered field.
- Hollow Vein unlocks resource-focused Ironroot Grove and hard lore-focused Forgotten Ossuary.
- Brann and Nyra are available in the refuge; Elowen unlocks through expedition completion.
- Gravebound, Gloam Stalker, and Veinbound enemies have distinct combat profiles.
- Equipment uses five slots, randomized modifiers, fixed identities, and passive discoveries.
- Frost Lance unlocks through the first Overseer kill or optional lore discovery.
- Boss rewards, extraction, defeat, and the 24-hour recovery cache are persistent.
- A nine-step onboarding card, procedural audio, reduced motion, touch layout, local saves, and PWA shell are included.
- Automated system coverage and browser interaction checks are part of release verification.

## Slice content

- One hero.
- Two available companions, with a third introduced during the slice.
- One town hub.
- One abandoned mining settlement biome.
- One linear quest chain with optional branches.
- Three enemy types.
- One boss: the corrupted mine overseer.
- Two gathering resources: ore and wood.
- One blacksmith town upgrade.
- A small set of named equipment with randomized modifiers.
- At least one discovered ability and one equipment-granted ability.

## Required player journey

1. The tutorial introduces the town, hero, equipment, and first companion.
2. The player equips the hero and configures a simple skill priority.
3. The player selects a route from the world map.
4. The party visibly travels through the isometric region.
5. The player gathers ore and wood at resource nodes.
6. The party resolves an automated encounter.
7. The player receives a normal reward choice and can inspect item modifiers.
8. The player reaches an optional branch containing a stronger encounter or additional resources.
9. The player defeats the corrupted mine overseer.
10. The player unlocks a new ability and the blacksmith upgrade.
11. A failed test expedition demonstrates resource loss and 24-hour recovery.
12. The browser can close and reopen while preserving local save data.

## Acceptance criteria

- A new player can understand the first expedition without external instructions.
- Touch controls work for every required action.
- Mouse controls work for every required action.
- The hero and companions are visually distinguishable at normal gameplay scale.
- Combat resolves without direct character steering.
- Skill priority visibly changes combat behavior.
- Loot comparison takes no more than two obvious interactions.
- The player can tell what was gathered, lost, and recovered.
- The first expedition can be completed in 5–10 minutes.
- A complete local save can be exported and imported.
- Core systems have automated tests for combat resolution, loot generation, resource loss, recovery expiry, and save/load.

## Deliberate exclusions from the slice

- Full six-companion roster.
- Procedural campaign generation.
- Cloud accounts and synchronization.
- Player trading, PvP, guilds, and housing.
- Complex crafting trees.
- Voice acting or cinematic cutscenes.
- Portrait orientation.

## Suggested build order

1. Data schemas and local save format.
2. World map, route nodes, and expedition state machine.
3. Hero, companion, equipment, and ability data.
4. Autobattle simulation with priorities and targeting.
5. Resource nodes, loot, failure, and recovery.
6. Town, blacksmith upgrade, and quest flow.
7. Isometric rendering and modular asset integration.
8. Touch-first UI and responsive layout.
9. Audio, accessibility, and onboarding polish.
10. Automated tests and playtest iteration.
