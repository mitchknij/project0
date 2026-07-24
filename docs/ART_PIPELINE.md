# Ashen Road art pipeline

## Quality target

`docs/art/gloamwood-art-benchmark-v1.png` is the visual benchmark. It defines:

- a grim but accessible dark-gothic tone;
- oxidized teal cloth, worn brown leather, bone, chipped slate, moss, and cyan blight;
- readable silhouettes at an isometric gameplay scale;
- cold forest ambience with warm directional rim light;
- grounded contact and directional shadows;
- environmental density built from reusable authored kits.

## Runtime asset standard

Production models are loaded as glTF/GLB containers. Character assets must include:

- a skinned skeleton;
- named idle, locomotion, attack, hit, and death clips;
- in-place locomotion;
- consistent meter-scale proportions;
- shadow-casting render meshes;
- attachment bones or named nodes for weapons and equipment.

Environment assets should be binary GLB, use shared materials where practical, and keep
their origins on the ground plane. Textures should use power-of-two dimensions and repeat
cleanly when intended for terrain.

## Character integration

Gameplay actors retain a stable, invisible simulation root. The visible production model
is a child of that root. This keeps click targeting, movement, combat range, mob respawning,
and camera following independent from a specific mesh or skeleton.

Animation names are remapped to gameplay states at runtime:

- idle → `Idle_Loop`, `Idle_Combat`, `Idle_Weapon`, or `Idle`;
- locomotion → `Jog_Fwd_Loop`, `Running_A`, `Walking_D_Skeletons`, `Run`, or `Walk`;
- attack → `Spell_Simple_Shoot`, `1H_Melee_Attack_Slice_Diagonal`, `Weapon`, or `Punch`;
- death → `Death01`, `Death_C_Skeletons`, or `Death`.

Mara now uses the Quaternius modular Female Ranger outfit. Compatible Universal Animation
Library tracks are baked into the outfit's own skeleton during asset preparation. Gameplay
state mapping adds `Idle_Loop`, `Walk_Loop`, `Jog_Fwd_Loop`, `Spell_Simple_Shoot`, and
`Death01` to the legacy clip names.

This appearance/animation split is the foundation for swapping outfit parts without
changing the simulation actor. The Warden refuge is assembled from the Medieval Village
MegaKit. Forest silhouettes and ground detail combine the Stylized Nature MegaKit with
Ultimate Stylized Nature, then apply a shared dark-gothic grade so both generations read
as one authored world.

Fallback procedural rigs remain available when an asset cannot load.

## Generated material assets

- `public/assets/textures/gloamwood-ground-v1.png`
- Quaternius `RockPath_*` geometry for the raised, shadow-casting causeway

Both were generated as seamless, neutral-lit albedo textures and resized to 1024×1024 for
WebGL delivery. They are combined with real-time lighting, fog, geometry, and shadows.
