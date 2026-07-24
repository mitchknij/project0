import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const workspace = resolve(import.meta.dirname, '..');

async function readGlb(relativePath) {
  const bytes = await readFile(resolve(workspace, relativePath));
  assert.equal(bytes.subarray(0, 4).toString('ascii'), 'glTF');
  const jsonLength = bytes.readUInt32LE(12);
  return JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim());
}

test('Mara production model has a skeleton and gameplay animation coverage', async () => {
  const hero = await readGlb('public/assets/models/quaternius/characters/female-ranger-animated.glb');
  const clips = new Set(hero.animations.map(({ name }) => name));
  assert.ok(hero.skins.length > 0);
  for (const required of ['Idle_Loop', 'Walk_Loop', 'Jog_Fwd_Loop', 'Spell_Simple_Shoot', 'Death01']) {
    assert.ok(clips.has(required), `missing hero clip: ${required}`);
  }
});

test('Gravebound Warrior production model has combat and respawn animation coverage', async () => {
  const gltf = await readGlb('public/assets/models/kaykit/skeletons/gravebound-warrior.glb');
  const clips = new Set(gltf.animations.map(({ name }) => name));
  assert.ok(gltf.skins.length > 0);
  for (const required of [
    'Idle_Combat',
    'Walking_D_Skeletons',
    'Running_A',
    '1H_Melee_Attack_Slice_Diagonal',
    'Hit_A',
    'Death_C_Skeletons',
    'Spawn_Ground_Skeletons'
  ]) {
    assert.ok(clips.has(required), `missing enemy clip: ${required}`);
  }
});

test('environment benchmark assets are binary glTF containers', async () => {
  const assets = [
    'public/assets/models/environment/gothic-arch.glb',
    'public/assets/models/environment/overgrown-wall.glb',
    'public/assets/models/quaternius/village/Wall_Plaster_Door_Round.glb',
    'public/assets/models/quaternius/nature/Grass_Common_Short.glb',
    'public/assets/models/quaternius/nature/Grass_Common_Tall.glb',
    'public/assets/models/quaternius/nature/Grass_Wispy_Short.glb',
    'public/assets/models/quaternius/nature/Rock_Medium_2.glb',
    'public/assets/models/quaternius/nature/RockPath_Square_Wide.glb',
    'public/assets/models/quaternius/nature/RockPath_Square_Thin.glb',
    'public/assets/models/quaternius/nature/RockPath_Square_Small_1.glb',
    'public/assets/models/quaternius/nature/RockPath_Square_Small_2.glb',
    'public/assets/models/quaternius/nature/RockPath_Square_Small_3.glb',
    'public/assets/models/quaternius/nature/RockPath_Round_Wide.glb',
    'public/assets/models/quaternius/nature/TwistedTree_2.glb',
    'public/assets/models/quaternius/nature/Ultimate_BirchTree_1.glb'
  ];
  for (const relativePath of assets) {
    const bytes = await readFile(resolve(workspace, relativePath));
    assert.equal(bytes.subarray(0, 4).toString('ascii'), 'glTF', `${relativePath} is not a GLB`);
  }
});
