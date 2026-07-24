export const ABILITIES = Object.freeze({
  glacialWard: Object.freeze({
    id: 'glacialWard',
    name: 'Glacial Ward',
    shortName: 'WARD',
    condition: 'Below 35% health',
    cooldown: 12,
    cost: 32,
    castTime: 0.72,
    trigger: 0.42,
    duration: 5,
    absorb: 36
  }),
  frostNova: Object.freeze({
    id: 'frostNova',
    name: 'Frost Nova',
    shortName: 'NOVA',
    condition: '2+ enemies nearby',
    cooldown: 6.5,
    cost: 24,
    castTime: 0.76,
    trigger: 0.46,
    radius: 4.2,
    damage: 18,
    slowDuration: 2.8
  }),
  frostLance: Object.freeze({
    id: 'frostLance',
    name: 'Frost Lance',
    shortName: 'LANCE',
    condition: 'Elite or boss target',
    cooldown: 4.2,
    cost: 18,
    castTime: 0.82,
    trigger: 0.52,
    range: 8.4,
    damage: 48,
    requiresTarget: true
  }),
  iceShard: Object.freeze({
    id: 'iceShard',
    name: 'Ice Shard',
    shortName: 'SHARD',
    condition: 'Default attack',
    cooldown: 0.78,
    cost: 6,
    castTime: 0.68,
    trigger: 0.48,
    range: 7.25,
    damage: 26,
    requiresTarget: true
  })
});

export const DEFAULT_ABILITY_ORDER = Object.freeze([
  'glacialWard',
  'frostNova',
  'iceShard'
]);

export function createAbilityState(order = DEFAULT_ABILITY_ORDER) {
  return {
    order: [...order],
    cooldowns: Object.fromEntries(order.map((id) => [id, 0]))
  };
}

export function tickAbilityCooldowns(cooldowns, dt) {
  for (const id of Object.keys(cooldowns)) {
    cooldowns[id] = Math.max(0, cooldowns[id] - dt);
  }
}

export function moveAbilityUp(order, id) {
  const index = order.indexOf(id);
  if (index <= 0) return [...order];
  const next = [...order];
  [next[index - 1], next[index]] = [next[index], next[index - 1]];
  return next;
}

export function choosePriorityAbility({
  order,
  cooldowns,
  health,
  maxHealth,
  resource = Infinity,
  nearbyEnemyCount,
  hasTarget
}) {
  for (const id of order) {
    if ((cooldowns[id] ?? 0) > 0) continue;
    if (resource < ABILITIES[id].cost) continue;
    if (id === 'glacialWard' && health / maxHealth <= 0.35) return id;
    if (id === 'frostNova' && nearbyEnemyCount >= 2) return id;
    if (id === 'frostLance' && hasTarget) return id;
    if (id === 'iceShard' && hasTarget) return id;
  }
  return null;
}
