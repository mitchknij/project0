# Project Overview

## Product identity

Project 0 is a touch-first, browser-based 2.5D isometric expedition RPG. The player leads one classless hero with a small roster of companions, prepares builds and skill priorities, selects routes, gathers resources, completes quests, and watches automated combat execute their strategy.

The emotional center is building a powerful hero. Companions provide combat roles and unique influences on the hero's build rather than competing as equal protagonists.

## Core loop

```text
Return to town
  → equip hero and companions
  → configure skill and targeting priorities
  → choose a campaign route and optional branch
  → travel, gather, and resolve encounters
  → choose rewards or recover dropped resources
  → improve the party and town
  → begin the next expedition
```

## Locked boundaries

### Gameplay

- One active classless hero with swappable companions.
- Six deeply designed companions; the first slice introduces three.
- Pausable real-time autobattle.
- Hero uses four active ability slots.
- Companions use simplified configurable priorities.
- Equipment slots: weapon, armor, helmet, accessory, gadget.
- Defeat ends the expedition immediately.
- Failed expeditions lose some gathered resources; those resources remain recoverable for 24 hours.
- Companions can only be swapped in town.
- The campaign is linear with optional branches.
- Branches provide extra resources, loot, lore, quests, harder encounters, and rare rewards.

### Progression and economy

- Common, uncommon, rare, epic, and legendary equipment rarity.
- Items have fixed base identities plus randomized modifiers.
- Rare items may change build and ability behavior, not only increase stats.
- Abilities come from equipment and discoveries.
- Hero and companion levels, equipment, abilities, and town development all contribute to long-term progression.
- Resources support crafting, town upgrades, and quests.
- Gold and specialized resources are used; no premium currency is planned for the core design.
- Crafting unlocks gradually through town upgrades.
- Rewards show visible odds and use bad-luck protection.

### World and presentation

- Dark gothic fantasy: grim and dangerous, but accessible and without graphic gore.
- Fixed isometric camera with drag-to-pan and pinch-to-zoom.
- Landscape-first responsive browser layout; portrait support is deferred.
- Visible travel combined with node-based route decisions.
- Combat zooms into encounters while remaining in the world.
- World uses reusable tiles, props, landmarks, and controlled procedural variation.
- Characters use rigged modular 3D animation with directional locomotion and standardized equipment anchors.
- Equipment is assembled from standardized layers.
- Abilities combine procedural effects with generated textures.

### Platform and production

- WebGL through PlayCanvas with a fixed isometric 3D camera. This supersedes the early Phaser preference after the 3D art benchmark review.
- Client-side single-player architecture; cloud saves are deferred.
- Browser-local saves with manual export/import.
- JSON/data files define content; code defines systems.
- Desktop and mobile browser support from the beginning.
- Hand-authored campaign content plus procedural repeatable expeditions.
- Automated tests plus regular playtesting.
- Agents share design documents and have strict ownership boundaries.

## Explicitly out of scope for the first version

- PvP.
- Player trading.
- Guild multiplayer.
- A fully open world.
- Player housing.
- Dozens of classes.
- Premium currency or power-selling monetization.

## Design guardrails

1. Autobattle must remain the central interaction; manual control cannot turn the game into an action RPG.
2. Every major system must support the expedition loop.
3. Complexity must be readable on a touch screen.
4. Generated art must conform to templates before entering the game.
5. Offline progress provides convenience, not automatic success.
6. Each progression layer needs a distinct job and must not become redundant stat inflation.
