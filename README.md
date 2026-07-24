# Ashen Road

Ashen Road is the locally deployable PlayCanvas vertical slice for Project 0: a mouse/touch-only, browser-based isometric gothic action-idle RPG.

## Play locally

Requirements: Node.js 24 or newer.

```powershell
npm install
npm start
```

Open the local address shown in the terminal and navigate to `/game.html`.

For live development:

```powershell
npm run dev
```

## Current PlayCanvas slice

- A large, camera-followed 3D hunting zone instead of a node-by-node text screen.
- A visual Ashen Road route map with confirmation, a main campaign route, and two optional branches.
- One directly commanded hero plus one swappable active companion with independent progression.
- Tap/click terrain to move and tap/click an enemy to pursue and repeatedly attack it.
- Persistent auto-combat that acquires nearby enemies and chains between targets.
- Three mechanically and visually distinct enemy archetypes across four roaming packs.
- Collectible blight essence, visible combat impacts, hero/enemy health, and defeat recovery.
- Real-time directional lighting and shadows shared by characters, foliage, ruins, and terrain.
- Responsive pointer and touch HUD with no keyboard controls required.
- Four gatherable resource families with depletion, timed respawning, and equipment-modified yields.
- A functional Warden Refuge where carried resources are secured.
- A four-stage forge quest, one blacksmith town upgrade, and three rare crafting recipes.
- Five equipment slots, named randomized item modifiers, equipment passives, and modular visuals.
- A named Corrupted Mine Overseer that unlocks after the refuge quest and gates extraction.
- A three-choice rare Overseer reward that must be claimed before extraction.
- Repeat Overseer hunts increase in tier, maximum health, and attack damage after each success.
- Successful extraction banks carried materials; defeat ends the run immediately and creates a 24-hour recovery cache.
- A nine-step in-world tutorial, discoverable fourth ability, procedural audio cues, reduced-motion support, and installable offline PWA shell.
- Browser-local PlayCanvas progression with refuge-based save export and import.

The earlier Phaser implementation remains under `src/` as a reference for progression
systems. The active playable renderer and simulation are under `src3d/`.

The first production art-direction benchmark is saved at
`docs/art/gloamwood-art-benchmark-v1.png`. Character rigs expose head, shoulder,
back, belt, and weapon attachment slots so generated equipment can be introduced
without rebuilding animation or combat code.

The active benchmark now streams rigged glTF characters, animation clips, converted GLB
ruins, generated terrain materials, and fallback procedural assets. See
`docs/ART_PIPELINE.md` and `THIRD_PARTY_ASSETS.md` for the production rules and licensing.

## Quality checks

```powershell
npm test
npm run build
```

The automated suite covers routes, optional branches, companions, discoveries, audio,
enemy archetypes, boss/extraction/recovery, town progression, gathering, equipment,
3D targeting, movement, range, and picking helpers.

## Project documents

- [Project Overview](docs/PROJECT_OVERVIEW.md)
- [Vertical Slice Specification](docs/VERTICAL_SLICE.md)
- [Local Release Notes](docs/LOCAL_RELEASE.md)

The boundary charter is locked at v1.6. Changes should be deliberate and documented.
