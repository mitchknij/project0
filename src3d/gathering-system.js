export const RESOURCE_TYPES = Object.freeze({
  ore: Object.freeze({
    name: 'Mooniron Ore',
    shortName: 'Ore',
    gatherDuration: 2.4,
    respawnDuration: 18,
    minYield: 2,
    maxYield: 4,
    yieldStat: 'oreYield'
  }),
  wood: Object.freeze({
    name: 'Gloamwood Timber',
    shortName: 'Wood',
    gatherDuration: 2.8,
    respawnDuration: 22,
    minYield: 2,
    maxYield: 3,
    yieldStat: 'woodYield'
  }),
  herb: Object.freeze({
    name: 'Frostcap Herb',
    shortName: 'Herbs',
    gatherDuration: 1.8,
    respawnDuration: 14,
    minYield: 1,
    maxYield: 3,
    yieldStat: 'herbYield'
  }),
  essence: Object.freeze({
    name: 'Veil Essence',
    shortName: 'Essence',
    gatherDuration: 3.1,
    respawnDuration: 26,
    minYield: 1,
    maxYield: 2,
    yieldStat: 'essenceYield'
  })
});

export function createResourceStock() {
  return Object.fromEntries(Object.keys(RESOURCE_TYPES).map((type) => [type, 0]));
}

export function gatheringDuration(type, gatherSpeed = 0) {
  return RESOURCE_TYPES[type].gatherDuration / Math.max(0.25, 1 + gatherSpeed);
}

export function gatheringYield(type, random, gearStats = {}) {
  const resource = RESOURCE_TYPES[type];
  const range = resource.maxYield - resource.minYield + 1;
  const base = resource.minYield + Math.min(range - 1, Math.floor(random() * range));
  return base + (gearStats[resource.yieldStat] ?? 0);
}

export function depleteResourceNode(node, now) {
  node.available = false;
  node.respawnAt = now + RESOURCE_TYPES[node.type].respawnDuration;
  return node.respawnAt;
}

export function respawnResourceNode(node, now) {
  if (node.available || now < node.respawnAt) return false;
  node.available = true;
  node.respawnAt = 0;
  return true;
}
