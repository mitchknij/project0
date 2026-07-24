import test from 'node:test';
import assert from 'node:assert/strict';
import {
  COMPANION_DEFINITIONS,
  COMPANION_IDS,
  awardCompanionXp,
  createCompanionRoster,
  getActiveCompanionBonuses,
  normalizeCompanionRoster,
  swapActiveCompanion,
  unlockCompanion,
  xpToNextLevel
} from '../src3d/companion-system.js';

test('creates three deep identities with starter, available, and slice unlock states', () => {
  const roster = createCompanionRoster();

  assert.equal(Object.keys(COMPANION_DEFINITIONS).length, 3);
  assert.equal(roster.activeId, COMPANION_IDS.STARTER);
  assert.equal(roster.companions.brann.unlocked, true);
  assert.equal(roster.companions.nyra.unlocked, true);
  assert.equal(roster.companions.elowen.unlocked, false);

  for (const definition of Object.values(COMPANION_DEFINITIONS)) {
    assert.ok(definition.name);
    assert.ok(definition.identity);
    assert.ok(definition.combatRole);
    assert.ok(definition.buildInfluence.name);
    assert.ok(Object.keys(definition.buildInfluence.modifiers).length);
  }
});

test('slice companion requires its milestone and unlock is immutable', () => {
  const initial = createCompanionRoster();
  const blocked = unlockCompanion(initial, COMPANION_IDS.SLICE_UNLOCK);

  assert.equal(blocked.success, false);
  assert.equal(blocked.reason, 'milestone-required');
  assert.equal(initial.companions.elowen.unlocked, false);

  const unlocked = unlockCompanion(initial, COMPANION_IDS.SLICE_UNLOCK, {
    sliceMilestoneCompleted: true
  });
  assert.equal(unlocked.success, true);
  assert.equal(unlocked.roster.companions.elowen.unlocked, true);
  assert.equal(initial.companions.elowen.unlocked, false);

  const duplicate = unlockCompanion(unlocked.roster, COMPANION_IDS.SLICE_UNLOCK, {
    sliceMilestoneCompleted: true
  });
  assert.equal(duplicate.success, false);
  assert.equal(duplicate.reason, 'already-unlocked');
});

test('only unlocked companions can be selected and swaps are refuge or town only', () => {
  const initial = createCompanionRoster();

  const fieldSwap = swapActiveCompanion(initial, COMPANION_IDS.AVAILABLE);
  assert.equal(fieldSwap.success, false);
  assert.equal(fieldSwap.reason, 'town-only');

  const lockedSwap = swapActiveCompanion(initial, COMPANION_IDS.SLICE_UNLOCK, {
    inTown: true
  });
  assert.equal(lockedSwap.success, false);
  assert.equal(lockedSwap.reason, 'companion-locked');

  const townSwap = swapActiveCompanion(initial, COMPANION_IDS.AVAILABLE, {
    inRefuge: true
  });
  assert.equal(townSwap.success, true);
  assert.equal(townSwap.roster.activeId, COMPANION_IDS.AVAILABLE);
  assert.equal(initial.activeId, COMPANION_IDS.STARTER);
});

test('companions gain levels and xp independently across multiple thresholds', () => {
  const initial = createCompanionRoster();
  const brann = awardCompanionXp(initial, COMPANION_IDS.STARTER, 270);

  assert.equal(brann.success, true);
  assert.equal(brann.levelsGained, 2);
  assert.deepEqual(brann.roster.companions.brann, {
    unlocked: true,
    level: 3,
    xp: 20
  });
  assert.deepEqual(brann.roster.companions.nyra, {
    unlocked: true,
    level: 1,
    xp: 0
  });
  assert.deepEqual(initial.companions.brann, {
    unlocked: true,
    level: 1,
    xp: 0
  });
  assert.equal(xpToNextLevel(3), 200);
});

test('locked companions cannot receive xp', () => {
  const initial = createCompanionRoster();
  const result = awardCompanionXp(initial, COMPANION_IDS.SLICE_UNLOCK, 100);

  assert.equal(result.success, false);
  assert.equal(result.reason, 'companion-locked');
  assert.equal(result.levelsGained, 0);
  assert.deepEqual(result.roster, initial);
});

test('active companion exposes only its unique build influence', () => {
  const initial = createCompanionRoster();
  const starterBonus = getActiveCompanionBonuses(initial);
  assert.deepEqual(starterBonus, {
    companionId: 'brann',
    influenceId: 'ashguard-oath',
    modifiers: {
      heroDamageReduction: 0.08,
      guardOnHit: 2
    }
  });

  const swapped = swapActiveCompanion(initial, COMPANION_IDS.AVAILABLE, {
    inTown: true
  });
  const availableBonus = getActiveCompanionBonuses(swapped.roster);
  assert.deepEqual(availableBonus, {
    companionId: 'nyra',
    influenceId: 'veilborn-catalyst',
    modifiers: {
      ailmentChance: 0.12,
      damageToAfflicted: 0.15
    }
  });
  assert.notDeepEqual(availableBonus.modifiers, starterBonus.modifiers);
});

test('normalization repairs malformed persistence data and returns frozen plain state', () => {
  const malformed = {
    activeId: 'missing',
    companions: {
      brann: { unlocked: false, level: -3, xp: Number.NaN },
      nyra: { unlocked: 'yes', level: 2.9, xp: -10 },
      elowen: { unlocked: true, level: 999, xp: 44 },
      intruder: { unlocked: true, level: 20, xp: 500 }
    }
  };
  const snapshot = structuredClone(malformed);
  const normalized = normalizeCompanionRoster(malformed);

  assert.deepEqual(normalized, {
    activeId: 'brann',
    companions: {
      brann: { unlocked: true, level: 1, xp: 0 },
      nyra: { unlocked: true, level: 2, xp: 0 },
      elowen: { unlocked: true, level: 20, xp: 44 }
    }
  });
  assert.deepEqual(malformed, snapshot);
  assert.equal(Object.isFrozen(normalized), true);
  assert.equal(Object.isFrozen(normalized.companions), true);
  assert.equal(Object.isFrozen(normalized.companions.brann), true);
  assert.deepEqual(JSON.parse(JSON.stringify(normalized)), normalized);
});

test('unknown IDs and invalid xp fail safely without changing the roster', () => {
  const initial = createCompanionRoster();

  assert.equal(unlockCompanion(initial, 'ghost', {
    sliceMilestoneCompleted: true
  }).reason, 'unknown-companion');
  assert.equal(swapActiveCompanion(initial, 'ghost', { inTown: true }).reason,
    'unknown-companion');

  const invalidXp = awardCompanionXp(initial, COMPANION_IDS.STARTER, -50);
  assert.equal(invalidXp.success, false);
  assert.equal(invalidXp.reason, 'invalid-xp');
  assert.deepEqual(invalidXp.roster, initial);
});
