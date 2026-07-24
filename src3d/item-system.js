export const ITEM_TEMPLATES = Object.freeze([
  Object.freeze({
    templateId: 'quarry_edge',
    name: 'Quarry Edge',
    slot: 'weapon',
    rarity: 'common',
    effect: '+4 ability damage · +1 ore yield',
    visual: 'blade',
    stats: { damage: 4, oreYield: 1 }
  }),
  Object.freeze({
    templateId: 'frostbite_wand',
    name: 'Frostbite Wand',
    slot: 'weapon',
    rarity: 'common',
    effect: '+3 Ice Shard damage',
    visual: 'wand',
    stats: { shardDamage: 3 }
  }),
  Object.freeze({
    templateId: 'rimefang',
    name: 'Rimefang',
    slot: 'weapon',
    rarity: 'rare',
    effect: 'Ice Shard gains +9 damage',
    visual: 'rimefang',
    stats: { damage: 3, shardDamage: 6 }
  }),
  Object.freeze({
    templateId: 'warden_leathers',
    name: 'Warden Leathers',
    slot: 'armor',
    rarity: 'common',
    effect: '+14 maximum health',
    visual: 'leather',
    stats: { maxHealth: 14 }
  }),
  Object.freeze({
    templateId: 'wayfarer_hood',
    name: 'Wayfarer Hood',
    slot: 'helmet',
    rarity: 'common',
    effect: '+10 maximum focus',
    visual: 'hood',
    stats: { maxFocus: 10 }
  }),
  Object.freeze({
    templateId: 'mineward_signet',
    name: 'Mineward Signet',
    slot: 'accessory',
    rarity: 'rare',
    effect: '+1 focus regeneration · +1 essence yield',
    visual: 'signet',
    stats: { focusRegen: 1, essenceYield: 1 }
  }),
  Object.freeze({
    templateId: 'frostweave_mantle',
    name: 'Frostweave Mantle',
    slot: 'armor',
    rarity: 'rare',
    effect: '+20 focus and +2 focus regeneration',
    visual: 'mantle',
    stats: { maxFocus: 20, focusRegen: 2 }
  }),
  Object.freeze({
    templateId: 'veinward_plate',
    name: 'Veinward Plate',
    slot: 'armor',
    rarity: 'rare',
    effect: 'Glacial Ward absorbs 16 additional damage',
    visual: 'plate',
    stats: { maxHealth: 18, wardAbsorb: 16 }
  }),
  Object.freeze({
    templateId: 'surveyors_lens',
    name: "Surveyor's Lens",
    slot: 'gadget',
    rarity: 'common',
    effect: '+1 focus regeneration · gather 25% faster',
    visual: 'lens',
    stats: { focusRegen: 1, gatherSpeed: 0.25 }
  }),
  Object.freeze({
    templateId: 'shard_prism',
    name: 'Shard Prism',
    slot: 'gadget',
    rarity: 'rare',
    effect: 'Ice Shard gains +6 damage',
    visual: 'prism',
    stats: { shardDamage: 6, maxFocus: 10 }
  }),
  Object.freeze({
    templateId: 'glacial_heart',
    name: 'Glacial Heart',
    slot: 'gadget',
    rarity: 'rare',
    effect: 'Frost Nova is larger and recovers faster',
    visual: 'heart',
    stats: { novaRadius: 0.8, novaCooldownReduction: 1.5 }
  }),
  Object.freeze({
    templateId: 'ossuary_reliquary',
    name: 'Ossuary Reliquary',
    slot: 'gadget',
    rarity: 'rare',
    effect: '+18 maximum health · +1 essence yield · Veiled Memory passive',
    visual: 'reliquary',
    stats: { maxHealth: 18, essenceYield: 1 }
  })
]);

export const EMPTY_GEAR_STATS = Object.freeze({
  damage: 0,
  shardDamage: 0,
  maxHealth: 0,
  maxFocus: 0,
  focusRegen: 0,
  wardAbsorb: 0,
  novaRadius: 0,
  novaCooldownReduction: 0,
  gatherSpeed: 0,
  oreYield: 0,
  woodYield: 0,
  herbYield: 0,
  essenceYield: 0
});

export const ITEM_MODIFIERS = Object.freeze([
  Object.freeze({ id: 'stout', name: 'Stout', effect: '+8 maximum health', stats: { maxHealth: 8 } }),
  Object.freeze({ id: 'focused', name: 'Focused', effect: '+8 maximum focus', stats: { maxFocus: 8 } }),
  Object.freeze({ id: 'keen', name: 'Keen', effect: '+2 ability damage', stats: { damage: 2 } }),
  Object.freeze({ id: 'surveying', name: 'Surveying', effect: '+1 ore yield', stats: { oreYield: 1 } }),
  Object.freeze({ id: 'verdant', name: 'Verdant', effect: '+1 herb yield', stats: { herbYield: 1 } })
]);

export function rollLootItem(random, serial, forceRare = false) {
  const rarity = forceRare || random() < 0.22 ? 'rare' : 'common';
  const pool = ITEM_TEMPLATES.filter((item) => item.rarity === rarity);
  const template = pool[Math.min(pool.length - 1, Math.floor(random() * pool.length))];
  const modifierRoll = typeof random === 'function' ? random() : 0;
  const modifier = ITEM_MODIFIERS[
    Math.min(
      ITEM_MODIFIERS.length - 1,
      Math.floor((Number.isFinite(modifierRoll) ? modifierRoll : 0) * ITEM_MODIFIERS.length)
    )
  ];
  return {
    ...template,
    name: `${modifier.name} ${template.name}`,
    effect: `${template.effect} · ${modifier.effect}`,
    stats: mergeStats(template.stats, modifier.stats),
    modifiers: [{ id: modifier.id, name: modifier.name, effect: modifier.effect }],
    id: `${template.templateId}_${serial}`
  };
}

export function createBossRewardChoices(random, serialStart, count = 3) {
  const rarePool = ITEM_TEMPLATES.filter((item) => item.rarity === 'rare');
  const available = [...rarePool];
  const requested = Math.max(0, Math.min(available.length, Math.floor(count)));
  const rewards = [];

  for (let index = 0; index < requested; index += 1) {
    const rolledValue = typeof random === 'function' ? random() : 0;
    const roll = Number.isFinite(rolledValue)
      ? Math.max(0, Math.min(0.999999, rolledValue))
      : 0;
    const poolIndex = Math.floor(roll * available.length);
    const [template] = available.splice(poolIndex, 1);
    rewards.push({
      ...template,
      stats: { ...template.stats },
      id: `${template.templateId}_${serialStart + index}`
    });
  }
  return rewards;
}

export function createItemFromTemplate(templateId, serial) {
  const template = ITEM_TEMPLATES.find((item) => item.templateId === templateId);
  if (!template) return null;
  return {
    ...template,
    stats: { ...template.stats },
    id: `${template.templateId}_${serial}`
  };
}

export function equipInventoryItem(inventory, equipment, itemId) {
  const item = inventory.find((candidate) => candidate.id === itemId);
  if (!item) return { ...equipment };
  return { ...equipment, [item.slot]: item.id };
}

export function deriveGearStats(inventory, equipment) {
  const stats = { ...EMPTY_GEAR_STATS };
  for (const itemId of Object.values(equipment)) {
    const item = inventory.find((candidate) => candidate.id === itemId);
    if (!item) continue;
    for (const [key, value] of Object.entries(item.stats)) {
      stats[key] = (stats[key] ?? 0) + value;
    }
  }
  return stats;
}

function mergeStats(...sources) {
  const merged = {};
  for (const source of sources) {
    for (const [key, value] of Object.entries(source ?? {})) {
      merged[key] = (merged[key] ?? 0) + value;
    }
  }
  return merged;
}
