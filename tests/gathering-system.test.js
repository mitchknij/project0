import test from 'node:test';
import assert from 'node:assert/strict';
import {
  createResourceStock,
  depleteResourceNode,
  gatheringDuration,
  gatheringYield,
  respawnResourceNode
} from '../src3d/gathering-system.js';

test('resource stock starts empty for every resource family', () => {
  assert.deepEqual(createResourceStock(), { ore: 0, wood: 0, herb: 0, essence: 0 });
});

test('gathering speed shortens the interaction without changing base data', () => {
  assert.equal(gatheringDuration('ore', 0.25), 1.92);
});

test('specialized equipment adds to a resource yield', () => {
  assert.equal(gatheringYield('ore', () => 0, { oreYield: 2 }), 4);
});

test('depleted nodes only respawn after their deadline', () => {
  const node = { type: 'herb', available: true, respawnAt: 0 };
  depleteResourceNode(node, 10);
  assert.equal(respawnResourceNode(node, 23.9), false);
  assert.equal(respawnResourceNode(node, 24), true);
});
