import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createDefaultSave, parseImportedSave, serializeSave } from '../src/core/save.js';
import { RECOVERY_WINDOW_MS, defeatExpedition, gatherAndAdvance, pruneExpiredRecovery, recoverCache, startExpedition } from '../src/core/expedition.js';
import { simulateEncounter } from '../src/core/combat.js';
import { claimLoot, lootChoicesFor } from '../src/core/loot.js';

const items = JSON.parse(await readFile(new URL('../src/data/items.json', import.meta.url)));
const enemies = JSON.parse(await readFile(new URL('../src/data/enemies.json', import.meta.url)));

test('expedition starts with a healthy hero and empty carried resources', () => {
  const base = createDefaultSave();
  base.hero.health = 12;
  const state = startExpedition(base, 'mine_approach', 1000);
  assert.equal(state.expedition.state, 'traveling');
  assert.equal(state.hero.health, 100);
  assert.deepEqual(state.expedition.carried, { ore: 0, wood: 0 });
});

test('gathering advances the route and stores resources as carried', () => {
  const state = gatherAndAdvance(startExpedition(createDefaultSave(), 'mine_approach'), 'ore', 4);
  assert.equal(state.expedition.nodeIndex, 1);
  assert.equal(state.expedition.carried.ore, 4);
  assert.equal(state.town.resources.ore, 0);
});

test('single deployed hero defeats the normal encounter through multiple rounds', () => {
  const state = startExpedition(createDefaultSave(), 'mine_approach');
  const enemy = enemies.find(entry => entry.id === 'hollow_worker');
  const result = simulateEncounter(state, enemy, items);
  assert.equal(result.result, 'victory');
  assert.ok(result.state.hero.health > 0);
  assert.ok(result.log.some(line => line.includes('Ember Bolt')));
  assert.equal(result.events.filter(event => event.type === 'hero-attack').length, 3);
  assert.equal(result.events.filter(event => event.type === 'enemy-attack').length, 2);
});

test('single deployed hero can survive the first boss when fully rested', () => {
  const state = startExpedition(createDefaultSave(), 'mine_approach');
  const boss = enemies.find(entry => entry.id === 'corrupted_overseer');
  const result = simulateEncounter(state, boss, items);
  assert.equal(result.result, 'victory');
  assert.ok(result.events.length > 10);
  assert.ok(result.state.hero.health > 0);
});

test('defeat stores carried resources in a cache for exactly 24 hours', () => {
  const now = 50_000;
  let state = startExpedition(createDefaultSave(), 'mine_approach', now);
  state = gatherAndAdvance(state, 'ore', 4);
  state = defeatExpedition(state, now);
  assert.deepEqual(state.recovery.resources, { ore: 4, wood: 0 });
  assert.equal(state.recovery.expiresAt, now + RECOVERY_WINDOW_MS);
  assert.equal(pruneExpiredRecovery(state, now + RECOVERY_WINDOW_MS - 1).recovery.resources.ore, 4);
  assert.equal(pruneExpiredRecovery(state, now + RECOVERY_WINDOW_MS).recovery, null);
});

test('valid cache recovery transfers resources to town', () => {
  const now = 90_000;
  let state = startExpedition(createDefaultSave(), 'mine_approach', now);
  state = gatherAndAdvance(state, 'wood', 3);
  state = defeatExpedition(state, now);
  state = recoverCache(state, now + 1);
  assert.equal(state.town.resources.wood, 3);
  assert.equal(state.recovery, null);
});

test('boss offers three rewards and selected loot enters inventory', () => {
  const boss = enemies.find(entry => entry.id === 'corrupted_overseer');
  const choices = lootChoicesFor(boss, items);
  assert.equal(choices.length, 3);
  const base = { ...createDefaultSave(), pendingLoot: choices.map(item => item.id) };
  const state = claimLoot(base, choices[0].id);
  assert.ok(state.inventory.includes(choices[0].id));
  assert.deepEqual(state.pendingLoot, []);
});

test('save export and import preserve the versioned game state', () => {
  const base = createDefaultSave();
  base.town.resources.ore = 12;
  const restored = parseImportedSave(serializeSave(base));
  assert.equal(restored.version, 2);
  assert.equal(restored.town.resources.ore, 12);
  assert.equal(restored.hero.equipped.weapon, 'rusted_blade');
});
