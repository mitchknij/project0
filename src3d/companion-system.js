export const COMPANION_IDS = Object.freeze({
  STARTER: 'brann',
  AVAILABLE: 'nyra',
  SLICE_UNLOCK: 'elowen'
});

export const COMPANION_DEFINITIONS = deepFreeze({
  brann: {
    id: 'brann',
    name: 'Brann Ashguard',
    identity: 'A disgraced sentinel who treats every expedition as a chance at redemption.',
    combatRole: 'vanguard',
    buildInfluence: {
      id: 'ashguard-oath',
      name: 'Ashguard Oath',
      modifiers: {
        heroDamageReduction: 0.08,
        guardOnHit: 2
      }
    },
    availability: 'starter'
  },
  nyra: {
    id: 'nyra',
    name: 'Nyra Veilborn',
    identity: 'A pragmatic occultist who turns enemy afflictions into openings for the party.',
    combatRole: 'hexweaver',
    buildInfluence: {
      id: 'veilborn-catalyst',
      name: 'Veilborn Catalyst',
      modifiers: {
        ailmentChance: 0.12,
        damageToAfflicted: 0.15
      }
    },
    availability: 'available'
  },
  elowen: {
    id: 'elowen',
    name: 'Elowen Rootbound',
    identity: 'The last warden of a blighted grove, seeking the source of its corruption.',
    combatRole: 'warden',
    buildInfluence: {
      id: 'rootbound-covenant',
      name: 'Rootbound Covenant',
      modifiers: {
        resourceYield: 0.15,
        healingReceived: 0.1
      }
    },
    availability: 'slice-milestone'
  }
});

const DEFAULT_LEVEL = 1;
const MAX_LEVEL = 20;
const BASE_XP_TO_LEVEL = 100;
const XP_STEP = 50;

export function createCompanionRoster() {
  return freezeRoster({
    activeId: COMPANION_IDS.STARTER,
    companions: {
      brann: createMember(true),
      nyra: createMember(true),
      elowen: createMember(false)
    }
  });
}

export function normalizeCompanionRoster(value) {
  const defaults = createCompanionRoster();
  if (!isPlainObject(value)) return defaults;

  const companions = Object.fromEntries(
    Object.keys(COMPANION_DEFINITIONS).map((id) => {
      const fallback = defaults.companions[id];
      const source = isPlainObject(value.companions?.[id])
        ? value.companions[id]
        : fallback;
      const mustBeUnlocked = id === COMPANION_IDS.STARTER
        || id === COMPANION_IDS.AVAILABLE;

      return [id, {
        unlocked: mustBeUnlocked || source.unlocked === true,
        level: clampInteger(source.level, DEFAULT_LEVEL, MAX_LEVEL),
        xp: nonNegativeInteger(source.xp)
      }];
    })
  );

  const requestedActive = typeof value.activeId === 'string'
    ? value.activeId
    : defaults.activeId;
  const activeId = companions[requestedActive]?.unlocked
    ? requestedActive
    : COMPANION_IDS.STARTER;

  return freezeRoster({ activeId, companions });
}

export function unlockCompanion(roster, companionId, context = {}) {
  const current = normalizeCompanionRoster(roster);
  const definition = COMPANION_DEFINITIONS[companionId];

  if (!definition) {
    return result(current, false, 'unknown-companion');
  }
  if (current.companions[companionId].unlocked) {
    return result(current, false, 'already-unlocked');
  }
  if (
    definition.availability === 'slice-milestone'
    && context.sliceMilestoneCompleted !== true
  ) {
    return result(current, false, 'milestone-required');
  }

  return result(freezeRoster({
    ...current,
    companions: {
      ...current.companions,
      [companionId]: {
        ...current.companions[companionId],
        unlocked: true
      }
    }
  }), true);
}

export function swapActiveCompanion(roster, companionId, context = {}) {
  const current = normalizeCompanionRoster(roster);

  if (!COMPANION_DEFINITIONS[companionId]) {
    return result(current, false, 'unknown-companion');
  }
  if (!current.companions[companionId].unlocked) {
    return result(current, false, 'companion-locked');
  }
  if (context.inTown !== true && context.inRefuge !== true) {
    return result(current, false, 'town-only');
  }
  if (current.activeId === companionId) {
    return result(current, false, 'already-active');
  }

  return result(freezeRoster({
    ...current,
    activeId: companionId
  }), true);
}

export function awardCompanionXp(roster, companionId, amount) {
  const current = normalizeCompanionRoster(roster);

  if (!COMPANION_DEFINITIONS[companionId]) {
    return {
      ...result(current, false, 'unknown-companion'),
      levelsGained: 0
    };
  }
  if (!current.companions[companionId].unlocked) {
    return {
      ...result(current, false, 'companion-locked'),
      levelsGained: 0
    };
  }

  const gained = nonNegativeInteger(amount);
  if (gained === 0) {
    return {
      ...result(current, false, 'invalid-xp'),
      levelsGained: 0
    };
  }

  const before = current.companions[companionId];
  const after = applyXp(before, gained);
  const next = freezeRoster({
    ...current,
    companions: {
      ...current.companions,
      [companionId]: after
    }
  });

  return {
    roster: next,
    success: true,
    levelsGained: after.level - before.level
  };
}

export function xpToNextLevel(level) {
  const normalizedLevel = clampInteger(level, DEFAULT_LEVEL, MAX_LEVEL);
  if (normalizedLevel >= MAX_LEVEL) return 0;
  return BASE_XP_TO_LEVEL + (normalizedLevel - 1) * XP_STEP;
}

export function getActiveCompanionBonuses(roster) {
  const current = normalizeCompanionRoster(roster);
  const definition = COMPANION_DEFINITIONS[current.activeId];

  return deepFreeze({
    companionId: definition.id,
    influenceId: definition.buildInfluence.id,
    modifiers: { ...definition.buildInfluence.modifiers }
  });
}

function applyXp(member, amount) {
  let level = member.level;
  let xp = member.xp + amount;

  while (level < MAX_LEVEL && xp >= xpToNextLevel(level)) {
    xp -= xpToNextLevel(level);
    level += 1;
  }
  if (level === MAX_LEVEL) xp = 0;

  return { ...member, level, xp };
}

function createMember(unlocked) {
  return { unlocked, level: DEFAULT_LEVEL, xp: 0 };
}

function result(roster, success, reason) {
  return reason ? { roster, success, reason } : { roster, success };
}

function freezeRoster(roster) {
  return deepFreeze({
    activeId: roster.activeId,
    companions: Object.fromEntries(
      Object.entries(roster.companions).map(([id, member]) => [id, { ...member }])
    )
  });
}

function deepFreeze(value) {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.values(value).forEach(deepFreeze);
    Object.freeze(value);
  }
  return value;
}

function nonNegativeInteger(value) {
  return Number.isFinite(value) ? Math.max(0, Math.floor(value)) : 0;
}

function clampInteger(value, minimum, maximum) {
  return Math.min(maximum, Math.max(minimum, nonNegativeInteger(value)));
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
