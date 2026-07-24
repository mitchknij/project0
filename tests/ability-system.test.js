import test from 'node:test';
import assert from 'node:assert/strict';
import {
  choosePriorityAbility,
  createAbilityState,
  moveAbilityUp,
  tickAbilityCooldowns
} from '../src3d/ability-system.js';

test('defensive priority selects Glacial Ward at critical health', () => {
  const state = createAbilityState();
  assert.equal(choosePriorityAbility({
    order: state.order,
    cooldowns: state.cooldowns,
    health: 30,
    maxHealth: 100,
    nearbyEnemyCount: 3,
    hasTarget: true
  }), 'glacialWard');
});

test('Frost Nova wins when Ward condition is false and enemies surround the hero', () => {
  const state = createAbilityState();
  assert.equal(choosePriorityAbility({
    order: state.order,
    cooldowns: state.cooldowns,
    health: 100,
    maxHealth: 100,
    nearbyEnemyCount: 2,
    hasTarget: true
  }), 'frostNova');
});

test('Ice Shard is the ready default against a selected target', () => {
  const state = createAbilityState();
  assert.equal(choosePriorityAbility({
    order: state.order,
    cooldowns: state.cooldowns,
    health: 100,
    maxHealth: 100,
    nearbyEnemyCount: 1,
    hasTarget: true
  }), 'iceShard');
});

test('discovered Frost Lance can take priority over the default attack', () => {
  const selected = choosePriorityAbility({
    order: ['frostLance', 'iceShard'],
    cooldowns: { frostLance: 0, iceShard: 0 },
    health: 100,
    maxHealth: 100,
    nearbyEnemyCount: 1,
    hasTarget: true
  });
  assert.equal(selected, 'frostLance');
});

test('cooldowns suppress abilities until they recover', () => {
  const state = createAbilityState();
  state.cooldowns.glacialWard = 4;
  state.cooldowns.frostNova = 2;
  tickAbilityCooldowns(state.cooldowns, 1.25);
  assert.equal(state.cooldowns.glacialWard, 2.75);
  assert.equal(state.cooldowns.frostNova, 0.75);
  assert.equal(state.cooldowns.iceShard, 0);
});

test('priority controls can move an ability one slot upward', () => {
  assert.deepEqual(
    moveAbilityUp(['glacialWard', 'frostNova', 'iceShard'], 'iceShard'),
    ['glacialWard', 'iceShard', 'frostNova']
  );
});

test('priority evaluation skips abilities the hero cannot afford', () => {
  const state = createAbilityState();
  assert.equal(choosePriorityAbility({
    order: state.order,
    cooldowns: state.cooldowns,
    health: 20,
    maxHealth: 100,
    resource: 10,
    nearbyEnemyCount: 3,
    hasTarget: true
  }), 'iceShard');
});
