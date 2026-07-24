export const TUTORIAL_STEPS = Object.freeze([
  'town',
  'equipment',
  'priority',
  'movement',
  'gather',
  'combat',
  'branch',
  'boss',
  'extraction'
]);

export const DISCOVERIES = Object.freeze({
  FOURTH_ACTIVE_ABILITY: 'fourth-active-ability'
});

export const FOURTH_ABILITY_SOURCES = Object.freeze([
  'optional-lore-branch',
  'first-boss'
]);

const VERSION = 1;

export function createDiscoveryProgress() {
  return freezeState({
    version: VERSION,
    tutorial: emptyTutorial(),
    discoveries: {
      fourthActiveAbility: {
        unlocked: false,
        source: null
      }
    },
    equipmentPassives: []
  });
}

export function normalizeDiscoveryProgress(progress) {
  if (!isPlainObject(progress)) return createDiscoveryProgress();

  const tutorialSource = isPlainObject(progress.tutorial)
    ? progress.tutorial
    : {};
  const discoverySource = isPlainObject(progress.discoveries)
    && isPlainObject(progress.discoveries.fourthActiveAbility)
    ? progress.discoveries.fourthActiveAbility
    : {};
  const source = FOURTH_ABILITY_SOURCES.includes(discoverySource.source)
    ? discoverySource.source
    : null;
  const unlocked = discoverySource.unlocked === true && source !== null;

  return freezeState({
    version: VERSION,
    tutorial: Object.fromEntries(
      TUTORIAL_STEPS.map((step) => [step, tutorialSource[step] === true])
    ),
    discoveries: {
      fourthActiveAbility: {
        unlocked,
        source: unlocked ? source : null
      }
    },
    equipmentPassives: normalizePassiveGrants(progress.equipmentPassives)
  });
}

export function recordTutorialStep(progress, step) {
  const current = normalizeDiscoveryProgress(progress);
  if (!TUTORIAL_STEPS.includes(step) || current.tutorial[step]) return current;

  return freezeState({
    ...current,
    tutorial: {
      ...current.tutorial,
      [step]: true
    }
  });
}

export function unlockDiscovery(progress, discovery, source) {
  const current = normalizeDiscoveryProgress(progress);
  if (
    discovery !== DISCOVERIES.FOURTH_ACTIVE_ABILITY
    || !FOURTH_ABILITY_SOURCES.includes(source)
    || current.discoveries.fourthActiveAbility.unlocked
  ) {
    return current;
  }

  return freezeState({
    ...current,
    discoveries: {
      ...current.discoveries,
      fourthActiveAbility: {
        unlocked: true,
        source
      }
    }
  });
}

export function recordEquipmentPassive(progress, equipmentId, abilityId) {
  const current = normalizeDiscoveryProgress(progress);
  const grant = normalizePassiveGrant({ equipmentId, abilityId });
  if (!grant) return current;

  const key = passiveKey(grant);
  if (current.equipmentPassives.some((entry) => passiveKey(entry) === key)) {
    return current;
  }

  return freezeState({
    ...current,
    equipmentPassives: [...current.equipmentPassives, grant]
      .sort(comparePassiveGrants)
  });
}

export function removeEquipmentPassive(progress, equipmentId, abilityId) {
  const current = normalizeDiscoveryProgress(progress);
  const grant = normalizePassiveGrant({ equipmentId, abilityId });
  if (!grant) return current;

  const key = passiveKey(grant);
  const equipmentPassives = current.equipmentPassives.filter(
    (entry) => passiveKey(entry) !== key
  );
  if (equipmentPassives.length === current.equipmentPassives.length) {
    return current;
  }

  return freezeState({
    ...current,
    equipmentPassives
  });
}

export function deriveDiscoveryCompletion(progress) {
  const current = normalizeDiscoveryProgress(progress);
  const completedSteps = TUTORIAL_STEPS.filter(
    (step) => current.tutorial[step]
  );
  const passiveAbilities = [...new Set(
    current.equipmentPassives.map((grant) => grant.abilityId)
  )].sort();
  const tutorialComplete = completedSteps.length === TUTORIAL_STEPS.length;
  const fourthActiveAbilityUnlocked =
    current.discoveries.fourthActiveAbility.unlocked;

  return deepFreeze({
    completedSteps,
    remainingSteps: TUTORIAL_STEPS.filter(
      (step) => !current.tutorial[step]
    ),
    completedCount: completedSteps.length,
    totalCount: TUTORIAL_STEPS.length,
    tutorialComplete,
    fourthActiveAbilityUnlocked,
    fourthActiveAbilitySource:
      current.discoveries.fourthActiveAbility.source,
    equipmentPassiveCount: current.equipmentPassives.length,
    passiveAbilities,
    complete: tutorialComplete && fourthActiveAbilityUnlocked
  });
}

function emptyTutorial() {
  return Object.fromEntries(TUTORIAL_STEPS.map((step) => [step, false]));
}

function normalizePassiveGrants(value) {
  if (!Array.isArray(value)) return [];

  const unique = new Map();
  for (const candidate of value) {
    const grant = normalizePassiveGrant(candidate);
    if (grant) unique.set(passiveKey(grant), grant);
  }
  return [...unique.values()].sort(comparePassiveGrants);
}

function normalizePassiveGrant(value) {
  if (!isPlainObject(value)) return null;
  const equipmentId = normalizeId(value.equipmentId);
  const abilityId = normalizeId(value.abilityId);
  return equipmentId && abilityId ? { equipmentId, abilityId } : null;
}

function normalizeId(value) {
  return typeof value === 'string' && value.trim()
    ? value.trim()
    : null;
}

function passiveKey(grant) {
  return `${grant.equipmentId}\u0000${grant.abilityId}`;
}

function comparePassiveGrants(left, right) {
  return left.equipmentId.localeCompare(right.equipmentId)
    || left.abilityId.localeCompare(right.abilityId);
}

function freezeState(state) {
  return deepFreeze(state);
}

function deepFreeze(value) {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.freeze(value);
    for (const child of Object.values(value)) deepFreeze(child);
  }
  return value;
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
