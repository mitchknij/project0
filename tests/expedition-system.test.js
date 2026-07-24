import test from 'node:test';
import assert from 'node:assert/strict';
import {
  RECOVERY_WINDOW_MS,
  claimBossReward,
  completeExtraction,
  createExpeditionProgress,
  defeatBoss,
  defeatExpedition,
  overseerDifficulty,
  pruneExpiredRecovery,
  recoverExpeditionCache,
  returnAfterDefeat,
  updateBossAvailability
} from '../src3d/expedition-system.js';

const EMPTY_RESOURCES = { ore: 0, wood: 0, herb: 0, essence: 0 };

test('creates the required initial expedition state and 24-hour recovery window', () => {
  assert.equal(RECOVERY_WINDOW_MS, 86_400_000);
  assert.deepEqual(createExpeditionProgress(), {
    state: 'hunting',
    bossState: 'locked',
    bossKills: 0,
    rewardPending: false,
    extractionReady: false,
    recovery: null,
    successfulRuns: 0
  });
});

test('boss availability unlocks only for a completed quest during hunting', () => {
  const initial = createExpeditionProgress();
  assert.deepEqual(updateBossAvailability(initial, false), initial);

  const available = updateBossAvailability(initial, true);
  assert.equal(available.bossState, 'available');
  assert.equal(initial.bossState, 'locked');

  const defeatedRun = defeatExpedition(initial, {}, 100);
  assert.equal(updateBossAvailability(defeatedRun, true).bossState, 'locked');

  const alreadyDefeated = {
    ...initial,
    bossState: 'defeated',
    rewardPending: false,
    extractionReady: true
  };
  assert.deepEqual(updateBossAvailability(alreadyDefeated, true), alreadyDefeated);
});

test('Overseer difficulty increases after each successful extraction', () => {
  assert.deepEqual(overseerDifficulty(createExpeditionProgress()), {
    tier: 1,
    maxHp: 430,
    attackDamage: 13
  });
  assert.deepEqual(overseerDifficulty({ successfulRuns: 2 }), {
    tier: 3,
    maxHp: 559,
    attackDamage: 15
  });
});

test('defeating an available boss unlocks one reward choice', () => {
  const available = updateBossAvailability(createExpeditionProgress(), true);
  const first = defeatBoss(available);

  assert.equal(first.success, true);
  assert.equal(first.progress.bossState, 'defeated');
  assert.equal(first.progress.bossKills, 1);
  assert.equal(first.progress.rewardPending, true);
  assert.equal(first.progress.extractionReady, false);
  assert.equal(available.bossKills, 0);

  const duplicate = defeatBoss(first.progress);
  assert.equal(duplicate.success, false);
  assert.equal(duplicate.reason, 'boss-unavailable');
  assert.equal(duplicate.progress.bossKills, 1);
  assert.equal(duplicate.progress.rewardPending, true);
  assert.equal(duplicate.progress.extractionReady, false);
});

test('claiming a boss reward unlocks extraction exactly once', () => {
  const defeated = defeatBoss(updateBossAvailability(createExpeditionProgress(), true));
  const claim = claimBossReward(defeated.progress);

  assert.equal(claim.success, true);
  assert.equal(claim.progress.rewardPending, false);
  assert.equal(claim.progress.extractionReady, true);

  const duplicate = claimBossReward(claim.progress);
  assert.equal(duplicate.success, false);
  assert.equal(duplicate.reason, 'reward-unavailable');
  assert.equal(duplicate.progress.extractionReady, true);
});

test('boss defeat is gated while locked or outside a hunting expedition', () => {
  const locked = defeatBoss(createExpeditionProgress());
  assert.equal(locked.success, false);
  assert.equal(locked.reason, 'boss-unavailable');

  const unavailable = {
    ...createExpeditionProgress(),
    state: 'defeated',
    bossState: 'available'
  };
  assert.equal(defeatBoss(unavailable).success, false);
});

test('extraction is gated until the boss is defeated and normalizes banked resources', () => {
  const initial = createExpeditionProgress();
  const blocked = completeExtraction(initial, { ore: 9 });
  assert.equal(blocked.success, false);
  assert.equal(blocked.reason, 'extraction-not-ready');
  assert.deepEqual(blocked.banked, EMPTY_RESOURCES);
  assert.deepEqual(blocked.progress, initial);

  const bossResult = defeatBoss(updateBossAvailability(initial, true));
  const reward = claimBossReward(bossResult.progress);
  const extracted = completeExtraction(reward.progress, {
    ore: 4,
    wood: -3,
    herb: 2.5,
    essence: Number.NaN,
    gold: 99
  });

  assert.equal(extracted.success, true);
  assert.deepEqual(extracted.banked, { ore: 4, wood: 0, herb: 2.5, essence: 0 });
  assert.deepEqual(extracted.progress, {
    state: 'hunting',
    bossState: 'locked',
    bossKills: 1,
    rewardPending: false,
    extractionReady: false,
    recovery: null,
    successfulRuns: 1
  });
  assert.equal(bossResult.progress.successfulRuns, 0);
});

test('duplicate extraction cannot bank resources or increment successful runs twice', () => {
  const defeatedBoss = defeatBoss(updateBossAvailability(createExpeditionProgress(), true));
  const reward = claimBossReward(defeatedBoss.progress);
  const first = completeExtraction(reward.progress, { essence: 3 });
  const duplicate = completeExtraction(first.progress, { essence: 3 });

  assert.equal(first.success, true);
  assert.equal(duplicate.success, false);
  assert.deepEqual(duplicate.banked, EMPTY_RESOURCES);
  assert.equal(duplicate.progress.successfulRuns, 1);
});

test('defeat creates a normalized recovery cache with an absolute 24-hour expiry', () => {
  const progress = {
    ...claimBossReward(
      defeatBoss(updateBossAvailability(createExpeditionProgress(), true)).progress
    ).progress,
    successfulRuns: 2
  };
  const carried = { ore: 7, wood: 3, herb: -1, essence: 1, coins: 50 };
  const defeated = defeatExpedition(progress, carried, 12_345);

  assert.equal(defeated.state, 'defeated');
  assert.equal(defeated.extractionReady, false);
  assert.equal(defeated.successfulRuns, 2);
  assert.deepEqual(defeated.recovery, {
    resources: { ore: 7, wood: 3, herb: 0, essence: 1 },
    createdAt: 12_345,
    expiresAt: 12_345 + RECOVERY_WINDOW_MS
  });
  assert.equal(progress.state, 'hunting');
  assert.equal(progress.extractionReady, true);
});

test('defeat with empty or invalid carried resources creates no recovery cache', () => {
  const initial = createExpeditionProgress();
  const defeated = defeatExpedition(initial, {
    ore: 0,
    wood: -5,
    herb: Number.NaN,
    essence: 0,
    gold: 100
  }, 500);

  assert.equal(defeated.state, 'defeated');
  assert.equal(defeated.recovery, null);
});

test('returning after defeat starts a locked run while preserving recovery', () => {
  const defeated = defeatExpedition(
    { ...createExpeditionProgress(), bossState: 'available' },
    { wood: 4 },
    1_000
  );
  const returned = returnAfterDefeat(defeated);

  assert.equal(returned.state, 'hunting');
  assert.equal(returned.bossState, 'locked');
  assert.equal(returned.extractionReady, false);
  assert.deepEqual(returned.recovery, defeated.recovery);
  assert.equal(defeated.state, 'defeated');
});

test('recovery succeeds strictly before expiry and can only be claimed once', () => {
  const createdAt = 2_000;
  const returned = returnAfterDefeat(
    defeatExpedition(createExpeditionProgress(), { ore: 2, herb: 5 }, createdAt)
  );
  const claim = recoverExpeditionCache(
    returned,
    createdAt + RECOVERY_WINDOW_MS - 1
  );

  assert.equal(claim.success, true);
  assert.deepEqual(claim.recovered, { ore: 2, wood: 0, herb: 5, essence: 0 });
  assert.equal(claim.progress.recovery, null);
  assert.notEqual(claim.progress, returned);

  const duplicate = recoverExpeditionCache(claim.progress, createdAt + 10);
  assert.equal(duplicate.success, false);
  assert.equal(duplicate.reason, 'no-recovery');
  assert.deepEqual(duplicate.recovered, EMPTY_RESOURCES);
});

test('recovery fails at the exact expiry boundary and clears the expired cache', () => {
  const createdAt = 10_000;
  const progress = defeatExpedition(
    createExpeditionProgress(),
    { essence: 2 },
    createdAt
  );
  const result = recoverExpeditionCache(
    progress,
    createdAt + RECOVERY_WINDOW_MS
  );

  assert.equal(result.success, false);
  assert.equal(result.reason, 'recovery-expired');
  assert.deepEqual(result.recovered, EMPTY_RESOURCES);
  assert.equal(result.progress.recovery, null);
  assert.notEqual(result.progress, progress);
});

test('pruning preserves a live cache and clears it at or after expiry immutably', () => {
  const createdAt = 50;
  const progress = defeatExpedition(
    createExpeditionProgress(),
    { wood: 1 },
    createdAt
  );
  const live = pruneExpiredRecovery(progress, createdAt + RECOVERY_WINDOW_MS - 1);
  assert.deepEqual(live.recovery, progress.recovery);

  const expired = pruneExpiredRecovery(progress, createdAt + RECOVERY_WINDOW_MS);
  assert.equal(expired.recovery, null);
  assert.ok(progress.recovery);

  const later = pruneExpiredRecovery(progress, createdAt + RECOVERY_WINDOW_MS + 500);
  assert.equal(later.recovery, null);
});

test('public operations normalize malformed progress without mutating inputs', () => {
  const malformed = {
    state: 'unknown',
    bossState: 'broken',
    bossKills: -5,
    rewardPending: 'yes',
    extractionReady: 'yes',
    recovery: {
      resources: { ore: 3, gold: 10 },
      createdAt: 100,
      expiresAt: 200
    },
    successfulRuns: 1.9
  };
  const snapshot = structuredClone(malformed);
  const normalized = updateBossAvailability(malformed, false);

  assert.deepEqual(normalized, {
    state: 'hunting',
    bossState: 'locked',
    bossKills: 0,
    rewardPending: false,
    extractionReady: false,
    recovery: {
      resources: { ore: 3, wood: 0, herb: 0, essence: 0 },
      createdAt: 100,
      expiresAt: 200
    },
    successfulRuns: 1
  });
  assert.deepEqual(malformed, snapshot);
});
