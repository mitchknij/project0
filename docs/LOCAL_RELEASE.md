# Local Release

## Release status

The current build is a locally deployable gameplay vertical slice. It proves the primary expedition loop with production-shaped data and state boundaries. The presentation separates the selectable Ashen Road map from a dedicated rendered expedition battlefield. Mara remains the commanded hero while one refuge-selected companion follows, contributes combat support, levels independently, and influences the build.

## Entry points

- `/game.html` — playable Ashen Road vertical slice.
- `/index.html` — original project decision questionnaire.

## Player flow

1. Select Hollow Vein on the Ashen Road map and confirm deployment.
2. Follow the training objectives for movement, refuge preparation, equipment, priorities, gathering, and combat.
3. Gather ore and wood, restore the Cold Forge, and craft a rare item.
4. Fight Gravebound, Gloam Stalker, and Veinbound packs with a selected companion.
5. Defeat the Corrupted Mine Overseer and choose one of three rare rewards.
6. Extract at Warden Refuge to secure carried materials and unlock optional routes.
7. Choose resource-rich Ironroot Grove or hard, lore-focused Forgotten Ossuary.
8. Discover Frost Lance, unlock Elowen, and continue higher-tier Overseer hunts.

If the party is defeated after gathering, the carried resources become a recoverable cache for 24 hours. Returning to town exposes the recovery action.

## PlayCanvas refuge progression

The current PlayCanvas field includes the first complete refuge progression loop:

1. Gather at least 3 ore and 3 wood in the rendered world.
2. Use the Warden Refuge command or select the glowing forge marker.
3. Mara paths back to the refuge and deposits all carried materials.
4. Restore the Cold Forge for 6 ore and 4 wood.
5. Craft and automatically equip one of three rare modular items.
6. Enter the northern hollow and defeat the Corrupted Mine Overseer.
7. Choose one of three distinct rare, build-changing Overseer rewards.
8. Return to Warden Refuge to extract and bank all carried materials.
9. Depart the refuge to begin a higher-tier Overseer hunt.

The refuge is a safe radius. Enemies drop aggro at its boundary and are steered
back into the hunting grounds. After the forge quest is complete, visiting the
refuge does not bank expedition materials unless the Overseer has been defeated.

Defeat ends the current expedition immediately. Carried resources are removed
from the field and stored as a recovery cache in Warden Refuge for exactly 24
hours. Returning after defeat starts a fresh, locked hunt while preserving that
cache until it is claimed or expires.

The Overseer reward choice is saved immediately, so closing the browser cannot
reroll the offered items. Selecting a reward equips it, unlocks extraction, and
clears the two unchosen items. Each successful extraction raises the next
Overseer tier by increasing maximum health and attack damage.

## Save behavior

- Progress autosaves to browser storage after every meaningful action.
- Export creates `gloamwood-refuge-save.json`.
- Import validates the save version before loading it.
- Save schema version: 2.

The PlayCanvas field uses a separate versioned `project0-town-v1` save because
its inventory contains item instances rather than legacy content IDs. It
autosaves town resources, carried resources, inventory, equipment, item serials,
blight shards, ability priority, blacksmith progress, and quest progress.
It also stores boss state, successful extraction count, and absolute recovery
cache expiry timestamps, route progress, companion roster and XP, tutorial and
ability discoveries, equipment passives, pending reward choices, and audio preference.
Export and import controls are available inside the Warden Refuge panel.

## In-world interaction

- Current objectives pulse directly on the rendered battlefield.
- The objective marker and side-panel action invoke the same touch-safe interaction.
- Mara walks to gates, resources, camps, and encounters before resolving them.
- Gathering, resting, combat, and loot use distinct in-world animations.

## Generated visual assets

- `public/assets/generated/ashen-road-overworld.png`
- `public/assets/generated/mine-arena.png`
- `public/assets/generated/character-lineup.png`

The assets were generated specifically for this project using the built-in image generation workflow. The character lineup was produced on a chroma-key background and converted to an alpha PNG for runtime cropping.

## Known production follow-ups

- Add more authored campaign chapters and companion-specific production models.
- Expand biome-specific enemy models and boss mechanics beyond stat scaling.
- Add a full side-by-side equipment comparison screen and advanced priority-rule editor.
- Add offline expedition time resolution.
- Split the PlayCanvas production bundle for faster initial loading.
- Portrait orientation remains deliberately deferred by the locked slice boundaries.
