export const RECOVERY_WINDOW_MS = 24 * 60 * 60 * 1000;

const RESOURCE_TYPES = Object.freeze(['ore', 'wood', 'herb', 'essence']);
const EMPTY_RESOURCES = Object.freeze({
  ore: 0,
  wood: 0,
  herb: 0,
  essence: 0
});

export function createExpeditionProgress() {
  return {
    state: 'hunting',
    bossState: 'locked',
    bossKills: 0,
    rewardPending: false,
    extractionReady: false,
    recovery: null,
    successfulRuns: 0
  };
}

export function overseerDifficulty(progress) {
  const completedRuns = normalizeProgress(progress).successfulRuns;
  return {
    tier: completedRuns + 1,
    maxHp: Math.round(430 * (1 + completedRuns * 0.15)),
    attackDamage: Math.round(13 * (1 + completedRuns * 0.08))
  };
}

export function updateBossAvailability(progress, questCompleted) {
  const current = normalizeProgress(progress);
  if (!questCompleted || current.state !== 'hunting' || current.bossState !== 'locked') {
    return current;
  }

  return {
    ...current,
    bossState: 'available'
  };
}

export function defeatBoss(progress) {
  const current = normalizeProgress(progress);
  if (current.state !== 'hunting' || current.bossState !== 'available') {
    return {
      progress: current,
      success: false,
      reason: 'boss-unavailable'
    };
  }

  return {
    progress: {
      ...current,
      bossState: 'defeated',
      bossKills: current.bossKills + 1,
      rewardPending: true,
      extractionReady: false
    },
    success: true
  };
}

export function claimBossReward(progress) {
  const current = normalizeProgress(progress);
  if (
    current.state !== 'hunting'
    || current.bossState !== 'defeated'
    || !current.rewardPending
  ) {
    return {
      progress: current,
      success: false,
      reason: 'reward-unavailable'
    };
  }

  return {
    progress: {
      ...current,
      rewardPending: false,
      extractionReady: true
    },
    success: true
  };
}

export function completeExtraction(progress, carried) {
  const current = normalizeProgress(progress);
  const empty = normalizeResources();
  if (!current.extractionReady) {
    return {
      progress: current,
      success: false,
      banked: empty,
      reason: 'extraction-not-ready'
    };
  }

  return {
    progress: {
      ...current,
      state: 'hunting',
      bossState: 'locked',
      rewardPending: false,
      extractionReady: false,
      recovery: null,
      successfulRuns: current.successfulRuns + 1
    },
    success: true,
    banked: normalizeResources(carried)
  };
}

export function defeatExpedition(progress, carried, now) {
  const current = normalizeProgress(progress);
  const resources = normalizeResources(carried);
  const createdAt = normalizeTimestamp(now);
  const hasResources = RESOURCE_TYPES.some((type) => resources[type] > 0);

  return {
    ...current,
    state: 'defeated',
    rewardPending: false,
    extractionReady: false,
    recovery: hasResources
      ? {
          resources,
          createdAt,
          expiresAt: createdAt + RECOVERY_WINDOW_MS
        }
      : null
  };
}

export function returnAfterDefeat(progress) {
  const current = normalizeProgress(progress);
  return {
    ...current,
    state: 'hunting',
    bossState: 'locked',
    rewardPending: false,
    extractionReady: false
  };
}

export function recoverExpeditionCache(progress, now) {
  const current = normalizeProgress(progress);
  const empty = normalizeResources();
  if (!current.recovery) {
    return {
      progress: current,
      success: false,
      recovered: empty,
      reason: 'no-recovery'
    };
  }

  if (normalizeTimestamp(now) >= current.recovery.expiresAt) {
    return {
      progress: { ...current, recovery: null },
      success: false,
      recovered: empty,
      reason: 'recovery-expired'
    };
  }

  return {
    progress: { ...current, recovery: null },
    success: true,
    recovered: normalizeResources(current.recovery.resources)
  };
}

export function pruneExpiredRecovery(progress, now) {
  const current = normalizeProgress(progress);
  if (!current.recovery || normalizeTimestamp(now) < current.recovery.expiresAt) {
    return current;
  }
  return {
    ...current,
    recovery: null
  };
}

function normalizeProgress(progress) {
  const defaults = createExpeditionProgress();
  if (!isPlainObject(progress)) return defaults;

  return {
    state: progress.state === 'defeated' ? 'defeated' : 'hunting',
    bossState: ['locked', 'available', 'defeated'].includes(progress.bossState)
      ? progress.bossState
      : defaults.bossState,
    bossKills: nonNegativeInteger(progress.bossKills),
    rewardPending: progress.rewardPending === true,
    extractionReady: progress.extractionReady === true,
    recovery: normalizeRecovery(progress.recovery),
    successfulRuns: nonNegativeInteger(progress.successfulRuns)
  };
}

function normalizeRecovery(recovery) {
  if (!isPlainObject(recovery)) return null;
  const createdAt = normalizeTimestamp(recovery.createdAt);
  const expiresAt = normalizeTimestamp(recovery.expiresAt);
  if (expiresAt < createdAt) return null;

  const resources = normalizeResources(recovery.resources);
  if (!RESOURCE_TYPES.some((type) => resources[type] > 0)) return null;

  return {
    resources,
    createdAt,
    expiresAt
  };
}

function normalizeResources(resources = EMPTY_RESOURCES) {
  return Object.fromEntries(
    RESOURCE_TYPES.map((type) => [type, nonNegativeNumber(resources?.[type])])
  );
}

function nonNegativeNumber(value) {
  return Number.isFinite(value) ? Math.max(0, value) : 0;
}

function nonNegativeInteger(value) {
  return Math.floor(nonNegativeNumber(value));
}

function normalizeTimestamp(value) {
  return nonNegativeNumber(value);
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
