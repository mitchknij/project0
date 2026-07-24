import test from 'node:test';
import assert from 'node:assert/strict';
import { ENEMY_ARCHETYPES, createEnemyStats } from '../src3d/enemy-system.js';

test('vertical slice exposes three mechanically distinct enemy archetypes', () => {
  assert.equal(Object.keys(ENEMY_ARCHETYPES).length, 3);
  const types = Object.keys(ENEMY_ARCHETYPES).map((type) => createEnemyStats(type));
  assert.equal(new Set(types.map((enemy) => enemy.name)).size, 3);
  assert.equal(new Set(types.map((enemy) => enemy.speed)).size, 3);
  assert.equal(new Set(types.map((enemy) => enemy.maxHp)).size, 3);
});

test('champions gain health and damage without mutating archetype data', () => {
  const base = createEnemyStats('stalker');
  const champion = createEnemyStats('stalker', true);
  assert.ok(champion.maxHp > base.maxHp);
  assert.ok(champion.attackDamage > base.attackDamage);
  assert.equal(champion.name, 'Gloam Packleader');
  assert.equal(ENEMY_ARCHETYPES.stalker.maxHp, 48);
});

test('unknown enemy types safely normalize to gravebound', () => {
  assert.equal(createEnemyStats('unknown').type, 'gravebound');
});
