import test from 'node:test';
import assert from 'node:assert/strict';
import {
  DISCOVERIES,
  FOURTH_ABILITY_SOURCES,
  TUTORIAL_STEPS,
  createDiscoveryProgress,
  deriveDiscoveryCompletion,
  normalizeDiscoveryProgress,
  recordEquipmentPassive,
  recordTutorialStep,
  removeEquipmentPassive,
  unlockDiscovery
} from '../src3d/discovery-system.js';

test('creates a frozen, persistence-friendly initial state', () => {
  const progress = createDiscoveryProgress();

  assert.deepEqual(progress, {
    version: 1,
    tutorial: {
      town: false,
      equipment: false,
      priority: false,
      movement: false,
      gather: false,
      combat: false,
      branch: false,
      boss: false,
      extraction: false
    },
    discoveries: {
      fourthActiveAbility: { unlocked: false, source: null }
    },
    equipmentPassives: []
  });
  assert.deepEqual(JSON.parse(JSON.stringify(progress)), progress);
  assert.ok(Object.isFrozen(progress));
  assert.ok(Object.isFrozen(progress.tutorial));
  assert.ok(Object.isFrozen(progress.discoveries.fourthActiveAbility));
  assert.ok(Object.isFrozen(progress.equipmentPassives));
});

test('exports the complete onboarding checklist and valid discovery sources', () => {
  assert.deepEqual(TUTORIAL_STEPS, [
    'town', 'equipment', 'priority', 'movement', 'gather',
    'combat', 'branch', 'boss', 'extraction'
  ]);
  assert.deepEqual(FOURTH_ABILITY_SOURCES, [
    'optional-lore-branch', 'first-boss'
  ]);
});

test('records every valid tutorial step without mutating prior state', () => {
  let progress = createDiscoveryProgress();
  for (const step of TUTORIAL_STEPS) {
    const previous = progress;
    progress = recordTutorialStep(progress, step);
    assert.equal(progress.tutorial[step], true);
    assert.equal(previous.tutorial[step], false);
  }

  const completion = deriveDiscoveryCompletion(progress);
  assert.equal(completion.tutorialComplete, true);
  assert.equal(completion.completedCount, 9);
  assert.deepEqual(completion.remainingSteps, []);
});

test('unknown and duplicate tutorial steps are safe normalized no-ops', () => {
  const malformed = { tutorial: { town: true, cheat: true } };
  const unknown = recordTutorialStep(malformed, 'cheat');
  const duplicate = recordTutorialStep(unknown, 'town');

  assert.equal(unknown.tutorial.town, true);
  assert.equal('cheat' in unknown.tutorial, false);
  assert.deepEqual(duplicate, unknown);
  assert.ok(Object.isFrozen(duplicate));
});

test('optional lore branch unlocks the discovered fourth active ability', () => {
  const initial = createDiscoveryProgress();
  const unlocked = unlockDiscovery(
    initial,
    DISCOVERIES.FOURTH_ACTIVE_ABILITY,
    'optional-lore-branch'
  );

  assert.deepEqual(unlocked.discoveries.fourthActiveAbility, {
    unlocked: true,
    source: 'optional-lore-branch'
  });
  assert.equal(initial.discoveries.fourthActiveAbility.unlocked, false);
  assert.equal(deriveDiscoveryCompletion(unlocked).fourthActiveAbilityUnlocked, true);
});

test('first boss is the alternative fourth ability unlock source', () => {
  const unlocked = unlockDiscovery(
    createDiscoveryProgress(),
    DISCOVERIES.FOURTH_ACTIVE_ABILITY,
    'first-boss'
  );

  assert.equal(
    deriveDiscoveryCompletion(unlocked).fourthActiveAbilitySource,
    'first-boss'
  );
});

test('invalid discovery ids and sources cannot unlock the fourth ability', () => {
  const initial = createDiscoveryProgress();
  const wrongId = unlockDiscovery(initial, 'fifth-active-ability', 'first-boss');
  const wrongSource = unlockDiscovery(
    initial,
    DISCOVERIES.FOURTH_ACTIVE_ABILITY,
    'ordinary-combat'
  );

  assert.equal(wrongId.discoveries.fourthActiveAbility.unlocked, false);
  assert.equal(wrongSource.discoveries.fourthActiveAbility.unlocked, false);
});

test('a discovery retains its original source when later unlocks are recorded', () => {
  const lore = unlockDiscovery(
    createDiscoveryProgress(),
    DISCOVERIES.FOURTH_ACTIVE_ABILITY,
    'optional-lore-branch'
  );
  const boss = unlockDiscovery(
    lore,
    DISCOVERIES.FOURTH_ACTIVE_ABILITY,
    'first-boss'
  );

  assert.equal(boss.discoveries.fourthActiveAbility.source, 'optional-lore-branch');
});

test('tracks, deduplicates, normalizes and sorts equipment passive grants', () => {
  let progress = recordEquipmentPassive(
    createDiscoveryProgress(),
    ' ring-ember ',
    ' passive-burn '
  );
  progress = recordEquipmentPassive(progress, 'amulet-frost', 'passive-chill');
  progress = recordEquipmentPassive(progress, 'ring-ember', 'passive-burn');

  assert.deepEqual(progress.equipmentPassives, [
    { equipmentId: 'amulet-frost', abilityId: 'passive-chill' },
    { equipmentId: 'ring-ember', abilityId: 'passive-burn' }
  ]);
  assert.equal(deriveDiscoveryCompletion(progress).equipmentPassiveCount, 2);
});

test('supports multiple equipment sources granting the same passive ability', () => {
  let progress = recordEquipmentPassive(
    createDiscoveryProgress(),
    'ring-one',
    'passive-ward'
  );
  progress = recordEquipmentPassive(progress, 'ring-two', 'passive-ward');
  const completion = deriveDiscoveryCompletion(progress);

  assert.equal(completion.equipmentPassiveCount, 2);
  assert.deepEqual(completion.passiveAbilities, ['passive-ward']);
});

test('removes only the requested equipment passive grant immutably', () => {
  let progress = recordEquipmentPassive(
    createDiscoveryProgress(),
    'helm',
    'passive-focus'
  );
  progress = recordEquipmentPassive(progress, 'boots', 'passive-haste');
  const before = progress;
  const removed = removeEquipmentPassive(progress, 'helm', 'passive-focus');

  assert.deepEqual(removed.equipmentPassives, [
    { equipmentId: 'boots', abilityId: 'passive-haste' }
  ]);
  assert.equal(before.equipmentPassives.length, 2);
  assert.ok(Object.isFrozen(removed.equipmentPassives[0]));
});

test('invalid passive records and missing removals are safe no-ops', () => {
  const initial = createDiscoveryProgress();
  const invalid = recordEquipmentPassive(initial, '', 'passive');
  const missing = removeEquipmentPassive(invalid, 'missing', 'passive');

  assert.deepEqual(invalid.equipmentPassives, []);
  assert.deepEqual(missing.equipmentPassives, []);
});

test('normalization repairs malformed persisted data without mutating it', () => {
  const malformed = {
    version: 99,
    tutorial: { town: true, boss: 'yes', extra: true },
    discoveries: {
      fourthActiveAbility: {
        unlocked: true,
        source: 'invalid-source'
      }
    },
    equipmentPassives: [
      { equipmentId: ' sword ', abilityId: ' guard ' },
      { equipmentId: 'sword', abilityId: 'guard' },
      { equipmentId: '', abilityId: 'broken' },
      null
    ],
    foreign: 'discard me'
  };
  const snapshot = structuredClone(malformed);
  const normalized = normalizeDiscoveryProgress(malformed);

  assert.equal(normalized.version, 1);
  assert.equal(normalized.tutorial.town, true);
  assert.equal(normalized.tutorial.boss, false);
  assert.equal('extra' in normalized.tutorial, false);
  assert.deepEqual(normalized.discoveries.fourthActiveAbility, {
    unlocked: false,
    source: null
  });
  assert.deepEqual(normalized.equipmentPassives, [
    { equipmentId: 'sword', abilityId: 'guard' }
  ]);
  assert.equal('foreign' in normalized, false);
  assert.deepEqual(malformed, snapshot);
  assert.ok(Object.isFrozen(normalized));
});

test('normalization rejects unlocked persistence state without a valid source', () => {
  const missingSource = normalizeDiscoveryProgress({
    discoveries: { fourthActiveAbility: { unlocked: true } }
  });
  const falseWithSource = normalizeDiscoveryProgress({
    discoveries: {
      fourthActiveAbility: {
        unlocked: false,
        source: 'first-boss'
      }
    }
  });

  assert.deepEqual(missingSource.discoveries.fourthActiveAbility, {
    unlocked: false,
    source: null
  });
  assert.deepEqual(falseWithSource.discoveries.fourthActiveAbility, {
    unlocked: false,
    source: null
  });
});

test('derived completion requires checklist and fourth ability discovery', () => {
  let tutorialOnly = createDiscoveryProgress();
  for (const step of TUTORIAL_STEPS) {
    tutorialOnly = recordTutorialStep(tutorialOnly, step);
  }
  assert.equal(deriveDiscoveryCompletion(tutorialOnly).complete, false);

  const fullyComplete = unlockDiscovery(
    tutorialOnly,
    DISCOVERIES.FOURTH_ACTIVE_ABILITY,
    'first-boss'
  );
  const result = deriveDiscoveryCompletion(fullyComplete);

  assert.equal(result.complete, true);
  assert.deepEqual(result.completedSteps, TUTORIAL_STEPS);
  assert.ok(Object.isFrozen(result));
  assert.ok(Object.isFrozen(result.completedSteps));
});
