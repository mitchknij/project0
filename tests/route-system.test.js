import test from 'node:test';
import assert from 'node:assert/strict';
import {
  ROUTES,
  completeActiveRoute,
  confirmRouteSelection,
  createRouteProgress,
  getAvailableRoutes,
  normalizeRouteProgress,
  selectRoute
} from '../src3d/route-system.js';

const EMPTY_REWARD = {
  resources: { ore: 0, wood: 0, herb: 0, essence: 0 },
  rareReward: null,
  lore: []
};

function enterAndComplete(progress, routeId) {
  const selected = selectRoute(progress, routeId);
  assert.equal(selected.success, true);
  const confirmed = confirmRouteSelection(selected.progress);
  assert.equal(confirmed.success, true);
  return completeActiveRoute(confirmed.progress);
}

test('campaign begins at the refuge with only Hollow Vein available', () => {
  const progress = createRouteProgress();

  assert.deepEqual(progress, {
    location: 'refuge',
    selectedRouteId: null,
    activeRouteId: null,
    completedRouteIds: [],
    claimedRewardIds: []
  });
  assert.deepEqual(
    getAvailableRoutes(progress).map((route) => route.id),
    ['hollow-vein']
  );
});

test('route definitions include the main, resource, and hard rare/lore routes', () => {
  assert.deepEqual(
    ROUTES.map(({ id, kind, difficulty }) => ({ id, kind, difficulty })),
    [
      { id: 'hollow-vein', kind: 'main', difficulty: 'standard' },
      { id: 'ironroot-grove', kind: 'optional-resource', difficulty: 'standard' },
      { id: 'forgotten-ossuary', kind: 'optional-rare-lore', difficulty: 'hard' }
    ]
  );
});

test('optional branches remain gated until Hollow Vein is completed', () => {
  const initial = createRouteProgress();

  for (const routeId of ['ironroot-grove', 'forgotten-ossuary']) {
    const blocked = selectRoute(initial, routeId);
    assert.equal(blocked.success, false);
    assert.equal(blocked.reason, 'route-locked');
    assert.deepEqual(blocked.progress, initial);
  }

  const main = enterAndComplete(initial, 'hollow-vein');
  assert.deepEqual(
    getAvailableRoutes(main.progress).map((route) => route.id),
    ['ironroot-grove', 'forgotten-ossuary']
  );
});

test('selection is immutable and requires confirmation before route completion', () => {
  const initial = createRouteProgress();
  const selected = selectRoute(initial, 'hollow-vein');

  assert.equal(selected.success, true);
  assert.equal(selected.progress.selectedRouteId, 'hollow-vein');
  assert.equal(selected.progress.location, 'refuge');
  assert.deepEqual(initial, createRouteProgress());

  const premature = completeActiveRoute(selected.progress);
  assert.equal(premature.success, false);
  assert.equal(premature.reason, 'no-active-route');
  assert.deepEqual(premature.reward, EMPTY_REWARD);

  const confirmed = confirmRouteSelection(selected.progress);
  assert.equal(confirmed.success, true);
  assert.equal(confirmed.progress.location, 'route');
  assert.equal(confirmed.progress.activeRouteId, 'hollow-vein');
  assert.equal(confirmed.progress.selectedRouteId, null);
  assert.equal(selected.progress.location, 'refuge');
});

test('unknown selection, absent confirmation, and selection while deployed are gated', () => {
  const initial = createRouteProgress();
  const unknown = selectRoute(initial, 'invented-route');
  assert.equal(unknown.success, false);
  assert.equal(unknown.reason, 'unknown-route');

  const absent = confirmRouteSelection(initial);
  assert.equal(absent.success, false);
  assert.equal(absent.reason, 'no-route-selected');

  const confirmed = confirmRouteSelection(selectRoute(initial, 'hollow-vein').progress);
  const deployed = selectRoute(confirmed.progress, 'hollow-vein');
  assert.equal(deployed.success, false);
  assert.equal(deployed.reason, 'not-at-refuge');
  assert.deepEqual(getAvailableRoutes(confirmed.progress), []);
});

test('resource branch grants its resource package once and returns to refuge', () => {
  const afterMain = enterAndComplete(createRouteProgress(), 'hollow-vein').progress;
  const result = enterAndComplete(afterMain, 'ironroot-grove');

  assert.equal(result.success, true);
  assert.equal(result.firstCompletion, true);
  assert.deepEqual(result.reward, {
    resources: { ore: 12, wood: 18, herb: 8, essence: 2 },
    rareReward: null,
    lore: ['Ironroot trees drink the mineral-rich water beneath Hollow Vein.']
  });
  assert.equal(result.progress.location, 'refuge');
  assert.deepEqual(result.progress.completedRouteIds, ['hollow-vein', 'ironroot-grove']);
  assert.deepEqual(result.progress.claimedRewardIds, ['hollow-vein', 'ironroot-grove']);
});

test('hard optional branch grants a rare reward and lore once', () => {
  const afterMain = enterAndComplete(createRouteProgress(), 'hollow-vein').progress;
  const result = enterAndComplete(afterMain, 'forgotten-ossuary');

  assert.equal(result.success, true);
  assert.equal(result.reward.rareReward, 'ossuary-reliquary');
  assert.equal(result.reward.resources.essence, 10);
  assert.equal(result.reward.lore.length, 2);
  assert.deepEqual(result.progress.completedRouteIds, [
    'hollow-vein',
    'forgotten-ossuary'
  ]);
});

test('completed routes cannot be selected or rewarded twice', () => {
  const first = enterAndComplete(createRouteProgress(), 'hollow-vein');
  const duplicate = selectRoute(first.progress, 'hollow-vein');

  assert.equal(duplicate.success, false);
  assert.equal(duplicate.reason, 'route-completed');
  assert.deepEqual(duplicate.progress, first.progress);

  const malformedReplay = {
    ...first.progress,
    location: 'route',
    activeRouteId: 'hollow-vein'
  };
  const normalized = normalizeRouteProgress(malformedReplay);
  assert.equal(normalized.location, 'refuge');
  assert.equal(normalized.activeRouteId, null);
  const completion = completeActiveRoute(malformedReplay);
  assert.equal(completion.success, false);
  assert.deepEqual(completion.reward, EMPTY_REWARD);
});

test('normalization produces canonical JSON-safe state from malformed input', () => {
  const malformed = {
    location: 'route',
    selectedRouteId: 'forgotten-ossuary',
    activeRouteId: 'invented-route',
    completedRouteIds: [
      'forgotten-ossuary',
      'hollow-vein',
      'hollow-vein',
      12,
      'unknown'
    ],
    claimedRewardIds: [
      'ironroot-grove',
      'forgotten-ossuary',
      'hollow-vein',
      'hollow-vein'
    ],
    extra: { unsafe: true }
  };
  const snapshot = structuredClone(malformed);
  const normalized = normalizeRouteProgress(malformed);

  assert.deepEqual(normalized, {
    location: 'refuge',
    selectedRouteId: null,
    activeRouteId: null,
    completedRouteIds: ['hollow-vein', 'forgotten-ossuary'],
    claimedRewardIds: ['hollow-vein', 'forgotten-ossuary']
  });
  assert.deepEqual(malformed, snapshot);
  assert.deepEqual(JSON.parse(JSON.stringify(normalized)), normalized);
  assert.deepEqual(normalizeRouteProgress(null), createRouteProgress());
  assert.deepEqual(normalizeRouteProgress([]), createRouteProgress());
});

test('normalization preserves a valid pending selection and valid active route', () => {
  const pending = normalizeRouteProgress({
    location: 'refuge',
    selectedRouteId: 'ironroot-grove',
    completedRouteIds: ['hollow-vein'],
    claimedRewardIds: ['hollow-vein']
  });
  assert.equal(pending.selectedRouteId, 'ironroot-grove');

  const active = normalizeRouteProgress({
    location: 'route',
    selectedRouteId: 'ironroot-grove',
    activeRouteId: 'forgotten-ossuary',
    completedRouteIds: ['hollow-vein'],
    claimedRewardIds: ['hollow-vein']
  });
  assert.deepEqual(active, {
    location: 'route',
    selectedRouteId: null,
    activeRouteId: 'forgotten-ossuary',
    completedRouteIds: ['hollow-vein'],
    claimedRewardIds: ['hollow-vein']
  });
});

test('returned route and reward data are detached from frozen definitions', () => {
  const available = getAvailableRoutes(createRouteProgress());
  available[0].requires.push('tampered');
  available[0].reward.resources.ore = 999;

  const fresh = getAvailableRoutes(createRouteProgress())[0];
  assert.deepEqual(fresh.requires, []);
  assert.equal(fresh.reward.resources.ore, 6);
});
