import test from 'node:test';
import assert from 'node:assert/strict';
import {
  circlePenetration,
  distanceXZ,
  moveTowardXZ,
  nearestAlive,
  pointSegmentDistanceXZ,
  raySphereDistance
} from '../src3d/systems.js';

test('distanceXZ ignores elevation', () => {
  assert.equal(distanceXZ({ x: 0, y: 99, z: 0 }, { x: 3, y: -2, z: 4 }), 5);
});

test('moveTowardXZ stops exactly at the destination', () => {
  assert.deepEqual(moveTowardXZ({ x: 0, z: 0 }, { x: 3, z: 4 }, 10), { x: 3, z: 4, arrived: true });
});

test('nearestAlive skips dead actors and respects range', () => {
  const actors = [
    { alive: false, position: { x: 1, z: 0 } },
    { alive: true, position: { x: 3, z: 0 } },
    { alive: true, position: { x: 7, z: 0 } }
  ];
  assert.equal(nearestAlive({ x: 0, z: 0 }, actors, 5), actors[1]);
  assert.equal(nearestAlive({ x: 0, z: 0 }, actors, 2), null);
});

test('raySphereDistance returns the front intersection', () => {
  assert.equal(raySphereDistance({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 5 }, 1), 4);
});

test('circlePenetration identifies overlapping actor bodies', () => {
  assert.ok(Math.abs(circlePenetration({ x: 0, z: 0 }, 0.5, { x: 0.8, z: 0 }, 0.5) - 0.2) < 0.0001);
  assert.ok(circlePenetration({ x: 0, z: 0 }, 0.5, { x: 2, z: 0 }, 0.5) < 0);
});

test('pointSegmentDistanceXZ detects an occluder between camera and target', () => {
  const result = pointSegmentDistanceXZ(
    { x: 1, z: 0.2 },
    { x: 0, z: 0 },
    { x: 2, z: 0 }
  );
  assert.ok(Math.abs(result.distance - 0.2) < 0.0001);
  assert.equal(result.t, 0.5);
});
