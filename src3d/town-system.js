export const TOWN_SAVE_KEY = 'project0-town-v1';
export const TOWN_SAVE_VERSION = 1;

const RESOURCE_TYPES = Object.freeze(['ore', 'wood', 'herb', 'essence']);
const EQUIPMENT_SLOTS = Object.freeze(['weapon', 'armor', 'helmet', 'accessory', 'gadget']);

export const BLACKSMITH_UPGRADE = Object.freeze({
  cost: Object.freeze({ ore: 6, wood: 4 })
});

export const CRAFTING_RECIPES = Object.freeze([
  Object.freeze({
    id: 'forge_rimecleaver',
    name: 'Forge Rimecleaver',
    description: 'A frost-tempered blade that strengthens every damaging skill.',
    cost: Object.freeze({ ore: 5, wood: 2, essence: 1 }),
    item: Object.freeze({
      templateId: 'crafted_rimecleaver',
      name: 'Rimecleaver',
      slot: 'weapon',
      rarity: 'rare',
      effect: '+6 ability damage and +3 Ice Shard damage',
      visual: 'rimefang',
      stats: Object.freeze({ damage: 6, shardDamage: 3 })
    })
  }),
  Object.freeze({
    id: 'stitch_gloamguard',
    name: 'Stitch Gloamguard',
    description: 'Layered woodland armor reinforced with mooniron plates.',
    cost: Object.freeze({ ore: 3, wood: 5, herb: 2 }),
    item: Object.freeze({
      templateId: 'crafted_gloamguard',
      name: 'Gloamguard',
      slot: 'armor',
      rarity: 'rare',
      effect: '+24 maximum health and +8 Glacial Ward absorption',
      visual: 'plate',
      stats: Object.freeze({ maxHealth: 24, wardAbsorb: 8 })
    })
  }),
  Object.freeze({
    id: 'assemble_veillens',
    name: 'Assemble Veil Lens',
    description: 'A calibrated lens that reveals richer deposits and channels focus.',
    cost: Object.freeze({ ore: 2, wood: 2, herb: 2, essence: 3 }),
    item: Object.freeze({
      templateId: 'crafted_veil_lens',
      name: 'Veil Lens',
      slot: 'gadget',
      rarity: 'rare',
      effect: 'Gather 30% faster and gain +2 focus regeneration',
      visual: 'lens',
      stats: Object.freeze({ gatherSpeed: 0.3, focusRegen: 2 })
    })
  })
]);

export function createTownProgress() {
  return {
    resources: { ore: 0, wood: 0, herb: 0, essence: 0 },
    blacksmithLevel: 0,
    crafted: [],
    quest: {
      stage: 0,
      gathered: { ore: 0, wood: 0 },
      completed: false
    }
  };
}

export function recordGather(progress, type, amount) {
  const current = normalizeProgress(progress);
  if (!['ore', 'wood'].includes(type) || current.quest.stage !== 0) return current;

  const gatheredAmount = positiveNumber(amount);
  if (gatheredAmount === 0) return current;

  const gathered = {
    ...current.quest.gathered,
    [type]: current.quest.gathered[type] + gatheredAmount
  };
  const readyToReturn = gathered.ore >= 3 && gathered.wood >= 3;

  return {
    ...current,
    quest: {
      ...current.quest,
      stage: readyToReturn ? 1 : 0,
      gathered
    }
  };
}

export function depositResources(progress, carried) {
  const current = normalizeProgress(progress);
  const deposit = normalizeResources(carried);
  const resources = Object.fromEntries(
    RESOURCE_TYPES.map((type) => [type, current.resources[type] + deposit[type]])
  );
  const nextProgress = {
    ...current,
    resources,
    quest: current.quest.stage === 1
      ? { ...current.quest, stage: 2 }
      : { ...current.quest }
  };

  return {
    progress: nextProgress,
    carried: createTownProgress().resources
  };
}

export function canAfford(resources, cost) {
  const available = normalizeResources(resources);
  if (!isPlainObject(cost)) return false;

  return Object.entries(cost).every(([type, required]) => (
    RESOURCE_TYPES.includes(type)
    && isNonNegativeNumber(required)
    && available[type] >= required
  ));
}

export function upgradeBlacksmith(progress) {
  const current = normalizeProgress(progress);
  if (current.blacksmithLevel >= 1) {
    return { progress: current, success: false, reason: 'Blacksmith already restored.' };
  }
  if (!canAfford(current.resources, BLACKSMITH_UPGRADE.cost)) {
    return { progress: current, success: false, reason: 'Not enough resources.' };
  }

  const upgraded = {
    ...current,
    resources: spendResources(current.resources, BLACKSMITH_UPGRADE.cost),
    blacksmithLevel: 1,
    quest: current.quest.stage === 2
      ? { ...current.quest, stage: 3 }
      : { ...current.quest }
  };
  return { progress: upgraded, success: true };
}

export function craftRecipe(progress, recipeId, serial) {
  const current = normalizeProgress(progress);
  const recipe = CRAFTING_RECIPES.find((candidate) => candidate.id === recipeId);
  if (!recipe) {
    return { progress: current, success: false, reason: 'Unknown crafting recipe.' };
  }
  if (current.blacksmithLevel < 1) {
    return { progress: current, success: false, reason: 'Restore the blacksmith first.' };
  }
  if (!canAfford(current.resources, recipe.cost)) {
    return { progress: current, success: false, reason: 'Not enough resources.' };
  }

  const safeSerial = typeof serial === 'string' || Number.isFinite(serial)
    ? String(serial)
    : String(current.crafted.length + 1);
  const item = {
    ...recipe.item,
    stats: { ...recipe.item.stats },
    id: `${recipe.item.templateId}_${safeSerial}`
  };
  const finishesQuest = current.quest.stage === 3;
  const crafted = {
    ...current,
    resources: spendResources(current.resources, recipe.cost),
    crafted: [...current.crafted, item.id],
    quest: finishesQuest
      ? { ...current.quest, stage: 4, completed: true }
      : { ...current.quest }
  };
  return { progress: crafted, success: true, item };
}

export function serializeTownSave(payload) {
  return JSON.stringify({
    version: TOWN_SAVE_VERSION,
    payload: normalizePayload(payload)
  });
}

export function parseTownSave(text) {
  try {
    const decoded = JSON.parse(text);
    if (!isPlainObject(decoded)) return createTownProgress();

    if (decoded.version === TOWN_SAVE_VERSION && isPlainObject(decoded.payload)) {
      return normalizePayload(decoded.payload);
    }

    // Migrate unversioned and legacy top-level town payloads.
    if (decoded.version === undefined) return normalizePayload(decoded);
    if (decoded.version < TOWN_SAVE_VERSION) {
      return normalizePayload(isPlainObject(decoded.payload) ? decoded.payload : decoded);
    }
    return createTownProgress();
  } catch {
    return createTownProgress();
  }
}

export function loadTownSave(storage = globalThis.localStorage) {
  try {
    const text = storage?.getItem(TOWN_SAVE_KEY);
    return typeof text === 'string' ? parseTownSave(text) : createTownProgress();
  } catch {
    return createTownProgress();
  }
}

export function saveTownState(payload, storage = globalThis.localStorage) {
  const normalized = normalizePayload(payload);
  try {
    storage?.setItem(TOWN_SAVE_KEY, serializeTownSave(normalized));
  } catch {
    // Storage can be unavailable or full; returning normalized state keeps play safe.
  }
  return normalized;
}

function normalizePayload(payload) {
  const source = isPlainObject(payload) ? payload : {};
  const progressSource = isPlainObject(source.progress) ? source.progress : source;
  const normalized = normalizeProgress(progressSource);

  if (isValidInventory(source.inventory)) normalized.inventory = cloneJson(source.inventory);
  if (isValidEquipment(source.equipment)) normalized.equipment = cloneJson(source.equipment);
  if (isPlainObject(source.carried)) normalized.carried = normalizeResources(source.carried);
  if (Number.isInteger(source.itemSerial) && source.itemSerial >= 0) normalized.itemSerial = source.itemSerial;
  if (isNonNegativeNumber(source.loot)) normalized.loot = source.loot;
  if (
    Array.isArray(source.abilityOrder)
    && source.abilityOrder.length > 0
    && source.abilityOrder.every((id) => typeof id === 'string')
  ) {
    normalized.abilityOrder = [...source.abilityOrder];
  }
  if (isPlainObject(source.expedition)) normalized.expedition = cloneJson(source.expedition);
  if (isValidInventory(source.pendingBossRewards)) {
    normalized.pendingBossRewards = cloneJson(source.pendingBossRewards);
  }
  if (isPlainObject(source.routeProgress)) normalized.routeProgress = cloneJson(source.routeProgress);
  if (isPlainObject(source.companionRoster)) normalized.companionRoster = cloneJson(source.companionRoster);
  if (isPlainObject(source.discoveryProgress)) normalized.discoveryProgress = cloneJson(source.discoveryProgress);
  if (typeof source.soundEnabled === 'boolean') normalized.soundEnabled = source.soundEnabled;
  return normalized;
}

function normalizeProgress(progress) {
  const base = createTownProgress();
  if (!isPlainObject(progress)) return base;

  const quest = isPlainObject(progress.quest) ? progress.quest : {};
  const gathered = isPlainObject(quest.gathered) ? quest.gathered : {};
  const stage = Number.isInteger(quest.stage) && quest.stage >= 0 && quest.stage <= 4
    ? quest.stage
    : base.quest.stage;

  return {
    resources: normalizeResources(progress.resources),
    blacksmithLevel: Number.isInteger(progress.blacksmithLevel) && progress.blacksmithLevel >= 0
      ? progress.blacksmithLevel
      : base.blacksmithLevel,
    crafted: Array.isArray(progress.crafted)
      ? progress.crafted.filter((id) => typeof id === 'string')
      : base.crafted,
    quest: {
      stage,
      gathered: {
        ore: positiveNumber(gathered.ore),
        wood: positiveNumber(gathered.wood)
      },
      completed: stage === 4 || quest.completed === true
    }
  };
}

function normalizeResources(resources) {
  const source = isPlainObject(resources) ? resources : {};
  return Object.fromEntries(
    RESOURCE_TYPES.map((type) => [type, positiveNumber(source[type])])
  );
}

function spendResources(resources, cost) {
  return Object.fromEntries(
    RESOURCE_TYPES.map((type) => [type, resources[type] - (cost[type] ?? 0)])
  );
}

function isValidInventory(value) {
  return Array.isArray(value) && value.every((item) => (
    typeof item === 'string'
    || (isPlainObject(item) && isJsonValue(item))
  ));
}

function isValidEquipment(value) {
  return isPlainObject(value)
    && Object.entries(value).every(([slot, item]) => (
      EQUIPMENT_SLOTS.includes(slot)
      && (item === null || typeof item === 'string' || (isPlainObject(item) && isJsonValue(item)))
    ));
}

function cloneJson(value) {
  return JSON.parse(JSON.stringify(value));
}

function isJsonValue(value) {
  try {
    JSON.stringify(value);
    return true;
  } catch {
    return false;
  }
}

function positiveNumber(value) {
  return isNonNegativeNumber(value) ? value : 0;
}

function isNonNegativeNumber(value) {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0;
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
