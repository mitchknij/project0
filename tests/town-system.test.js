import test from 'node:test';
import assert from 'node:assert/strict';
import {
  BLACKSMITH_UPGRADE,
  CRAFTING_RECIPES,
  TOWN_SAVE_KEY,
  TOWN_SAVE_VERSION,
  canAfford,
  craftRecipe,
  createTownProgress,
  depositResources,
  loadTownSave,
  parseTownSave,
  recordGather,
  saveTownState,
  serializeTownSave,
  upgradeBlacksmith
} from '../src3d/town-system.js';

function withResources(resources, progress = createTownProgress()) {
  return { ...progress, resources: { ...progress.resources, ...resources } };
}

function memoryStorage(initial = {}) {
  const values = new Map(Object.entries(initial));
  return {
    getItem(key) {
      return values.has(key) ? values.get(key) : null;
    },
    setItem(key, value) {
      values.set(key, value);
    }
  };
}

test('town defaults and public content have the required shape', () => {
  assert.deepEqual(createTownProgress(), {
    resources: { ore: 0, wood: 0, herb: 0, essence: 0 },
    blacksmithLevel: 0,
    crafted: [],
    quest: { stage: 0, gathered: { ore: 0, wood: 0 }, completed: false }
  });
  assert.deepEqual(BLACKSMITH_UPGRADE.cost, { ore: 6, wood: 4 });
  assert.equal(CRAFTING_RECIPES.length, 3);
  assert.deepEqual(CRAFTING_RECIPES.map(({ item }) => item.slot), ['weapon', 'armor', 'gadget']);
  for (const recipe of CRAFTING_RECIPES) {
    assert.ok(recipe.id && recipe.name && recipe.description);
    assert.ok(recipe.cost && recipe.item.templateId && recipe.item.effect && recipe.item.visual);
    assert.equal(recipe.item.rarity, 'rare');
    assert.ok(Object.keys(recipe.item.stats).length > 0);
  }
});

test('affordability accepts exact or surplus resources and rejects invalid costs', () => {
  const resources = { ore: 6, wood: 7, herb: 1, essence: 0 };
  assert.equal(canAfford(resources, { ore: 6, wood: 4 }), true);
  assert.equal(canAfford(resources, { ore: 7 }), false);
  assert.equal(canAfford(resources, { gold: 1 }), false);
  assert.equal(canAfford(resources, { ore: -1 }), false);
});

test('recording the gathering objective is immutable and advances at three ore and wood', () => {
  const initial = createTownProgress();
  const oreProgress = recordGather(initial, 'ore', 3);
  const ready = recordGather(oreProgress, 'wood', 3);

  assert.deepEqual(initial.quest.gathered, { ore: 0, wood: 0 });
  assert.deepEqual(oreProgress.quest.gathered, { ore: 3, wood: 0 });
  assert.equal(oreProgress.quest.stage, 0);
  assert.deepEqual(ready.quest.gathered, { ore: 3, wood: 3 });
  assert.equal(ready.quest.stage, 1);
  assert.deepEqual(recordGather(ready, 'ore', 5), ready);
  assert.deepEqual(recordGather(initial, 'herb', 10), initial);
});

test('deposit transfers every carried resource, clears carried, and advances the return stage', () => {
  let progress = recordGather(createTownProgress(), 'ore', 3);
  progress = recordGather(progress, 'wood', 3);
  progress = withResources({ ore: 2, herb: 1 }, progress);
  const carried = { ore: 4, wood: 5, herb: 2, essence: 1 };

  const result = depositResources(progress, carried);

  assert.deepEqual(result.progress.resources, { ore: 6, wood: 5, herb: 3, essence: 1 });
  assert.deepEqual(result.carried, { ore: 0, wood: 0, herb: 0, essence: 0 });
  assert.equal(result.progress.quest.stage, 2);
  assert.deepEqual(carried, { ore: 4, wood: 5, herb: 2, essence: 1 });
});

test('blacksmith upgrade reports insufficient resources without mutation', () => {
  const progress = createTownProgress();
  const result = upgradeBlacksmith(progress);
  assert.equal(result.success, false);
  assert.match(result.reason, /resources/i);
  assert.deepEqual(result.progress, progress);
});

test('blacksmith upgrade spends its cost once and advances the restoration quest', () => {
  const progress = {
    ...withResources({ ore: 8, wood: 7, herb: 2 }),
    quest: { stage: 2, gathered: { ore: 3, wood: 3 }, completed: false }
  };
  const result = upgradeBlacksmith(progress);

  assert.equal(result.success, true);
  assert.equal(result.progress.blacksmithLevel, 1);
  assert.deepEqual(result.progress.resources, { ore: 2, wood: 3, herb: 2, essence: 0 });
  assert.equal(result.progress.quest.stage, 3);
  assert.equal(upgradeBlacksmith(result.progress).success, false);
});

test('crafting validates recipe, blacksmith, and resources', () => {
  const locked = withResources({ ore: 99, wood: 99, herb: 99, essence: 99 });
  assert.match(craftRecipe(locked, CRAFTING_RECIPES[0].id, 1).reason, /blacksmith/i);

  const restored = { ...createTownProgress(), blacksmithLevel: 1 };
  assert.match(craftRecipe(restored, CRAFTING_RECIPES[0].id, 1).reason, /resources/i);
  assert.match(craftRecipe(restored, 'missing', 1).reason, /unknown/i);
});

test('crafting creates a modular rare item, spends resources, and completes the quest', () => {
  const recipe = CRAFTING_RECIPES[2];
  const progress = {
    ...withResources({ ore: 10, wood: 10, herb: 10, essence: 10 }),
    blacksmithLevel: 1,
    quest: { stage: 3, gathered: { ore: 3, wood: 3 }, completed: false }
  };
  const result = craftRecipe(progress, recipe.id, 42);

  assert.equal(result.success, true);
  assert.equal(result.item.id, `${recipe.item.templateId}_42`);
  assert.equal(result.item.slot, 'gadget');
  assert.equal(result.item.rarity, 'rare');
  assert.deepEqual(result.progress.resources, { ore: 8, wood: 8, herb: 8, essence: 7 });
  assert.deepEqual(result.progress.crafted, [result.item.id]);
  assert.equal(result.progress.quest.stage, 4);
  assert.equal(result.progress.quest.completed, true);
  assert.equal(progress.resources.ore, 10);
});

test('save roundtrip preserves town and active PlayCanvas progression data', () => {
  const item = { id: 'crafted_rimecleaver_7', slot: 'weapon', stats: { damage: 6 } };
  const payload = {
    ...withResources({ ore: 12, essence: 4 }),
    inventory: [item],
    equipment: { weapon: item.id, armor: null, gadget: null },
    carried: { ore: 2, wood: 3, herb: 1, essence: 4 },
    itemSerial: 17,
    loot: 9,
    abilityOrder: ['iceShard', 'frostNova', 'glacialWard'],
    expedition: {
      state: 'defeated',
      bossState: 'locked',
      recovery: {
        resources: { ore: 2, wood: 1, herb: 0, essence: 3 },
        createdAt: 1000,
        expiresAt: 86401000
      }
    },
    pendingBossRewards: [{
      id: 'rimefang_12',
      templateId: 'rimefang',
      name: 'Rimefang',
      slot: 'weapon',
      rarity: 'rare',
      effect: 'Ice Shard gains +9 damage',
      visual: 'rimefang',
      stats: { damage: 3, shardDamage: 6 }
    }],
    routeProgress: {
      location: 'route',
      selectedRouteId: null,
      activeRouteId: 'hollow-vein',
      completedRouteIds: [],
      claimedRewardIds: []
    },
    companionRoster: {
      activeId: 'nyra',
      companions: {
        brann: { unlocked: true, level: 1, xp: 0 },
        nyra: { unlocked: true, level: 2, xp: 10 },
        elowen: { unlocked: false, level: 1, xp: 0 }
      }
    },
    discoveryProgress: {
      version: 1,
      tutorial: { town: true },
      discoveries: { fourthActiveAbility: { unlocked: false, source: null } },
      equipmentPassives: []
    },
    soundEnabled: false
  };

  const text = serializeTownSave(payload);
  const envelope = JSON.parse(text);
  const restored = parseTownSave(text);

  assert.equal(envelope.version, TOWN_SAVE_VERSION);
  assert.equal(restored.resources.ore, 12);
  assert.deepEqual(restored.inventory, [item]);
  assert.deepEqual(restored.equipment, payload.equipment);
  assert.deepEqual(restored.carried, payload.carried);
  assert.equal(restored.itemSerial, 17);
  assert.equal(restored.loot, 9);
  assert.deepEqual(restored.abilityOrder, payload.abilityOrder);
  assert.deepEqual(restored.expedition, payload.expedition);
  assert.deepEqual(restored.pendingBossRewards, payload.pendingBossRewards);
  assert.deepEqual(restored.routeProgress, payload.routeProgress);
  assert.deepEqual(restored.companionRoster, payload.companionRoster);
  assert.deepEqual(restored.discoveryProgress, payload.discoveryProgress);
  assert.equal(restored.soundEnabled, false);
});

test('malformed and unsupported saves safely migrate to defaults', () => {
  assert.deepEqual(parseTownSave('{not json'), createTownProgress());
  assert.deepEqual(parseTownSave(JSON.stringify({ version: 999, payload: { resources: { ore: 8 } } })), createTownProgress());

  const migrated = parseTownSave(JSON.stringify({
    resources: { ore: 5, wood: -2, herb: 'bad' },
    blacksmithLevel: 'bad',
    quest: { stage: 9, gathered: { ore: 3 } },
    inventory: [{ id: 'safe-item' }],
    equipment: { weapon: 'safe-item', invalidSlot: 'discard-me' }
  }));
  assert.deepEqual(migrated.resources, { ore: 5, wood: 0, herb: 0, essence: 0 });
  assert.equal(migrated.blacksmithLevel, 0);
  assert.deepEqual(migrated.inventory, [{ id: 'safe-item' }]);
  assert.equal('equipment' in migrated, false);
});

test('storage helpers use the exported key and tolerate unavailable storage', () => {
  const storage = memoryStorage();
  const payload = withResources({ wood: 9 });
  const saved = saveTownState(payload, storage);
  const loaded = loadTownSave(storage);

  assert.equal(saved.resources.wood, 9);
  assert.equal(loaded.resources.wood, 9);
  assert.equal(JSON.parse(storage.getItem(TOWN_SAVE_KEY)).version, TOWN_SAVE_VERSION);
  assert.deepEqual(loadTownSave({ getItem() { throw new Error('blocked'); } }), createTownProgress());
});
