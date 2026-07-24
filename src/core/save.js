const KEY = 'project0-save-v2';
export const SAVE_VERSION = 2;

export function createDefaultSave() {
  return {
    version: SAVE_VERSION,
    hero: { id: 'hero_mara', level: 1, xp: 0, health: 100, maxHealth: 100, abilities: ['basic_strike', 'ember_bolt'], equipped: { weapon: 'rusted_blade', armor: null, helmet: null, accessory: null, gadget: null } },
    companions: ['guardian', 'occultist'],
    unlockedCompanions: ['guardian', 'occultist'],
    town: { blacksmithLevel: 0, resources: { ore: 0, wood: 0 } },
    inventory: ['rusted_blade'],
    quest: { id: 'the_hollow_vein', stage: 0, completed: false },
    expedition: { state: 'town', routeId: null, nodeIndex: 0, startedAt: null, carried: { ore: 0, wood: 0 }, skipped: [] },
    recovery: null,
    pendingLoot: [],
    log: ['The lanterns of Blackfen Refuge are still burning.']
  };
}

function mergeSave(raw) {
  const base = createDefaultSave();
  if (!raw || raw.version !== SAVE_VERSION) return base;
  return { ...base, ...raw, hero: { ...base.hero, ...raw.hero, equipped: { ...base.hero.equipped, ...raw.hero?.equipped } }, town: { ...base.town, ...raw.town, resources: { ...base.town.resources, ...raw.town?.resources } }, expedition: { ...base.expedition, ...raw.expedition, carried: { ...base.expedition.carried, ...raw.expedition?.carried } } };
}

export function loadSave(storage = localStorage) {
  try { return mergeSave(JSON.parse(storage.getItem(KEY))); } catch { return createDefaultSave(); }
}
export function saveGame(state, storage = localStorage) { storage.setItem(KEY, JSON.stringify(state)); return new Date().toISOString(); }
export function serializeSave(state) { return JSON.stringify({ ...state, version: SAVE_VERSION }, null, 2); }
export function parseImportedSave(text) { const value = JSON.parse(text); if (value.version !== SAVE_VERSION) throw new Error('This save uses an unsupported version.'); return mergeSave(value); }
