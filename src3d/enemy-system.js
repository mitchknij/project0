export const ENEMY_ARCHETYPES = Object.freeze({
  gravebound: Object.freeze({
    id: 'gravebound',
    name: 'Gravebound Warrior',
    championName: 'Gravebound Champion',
    kind: 'RESTLESS MINER',
    maxHp: 64,
    speed: 2.25,
    attackRange: 1.55,
    attackDamage: 7,
    scale: 1.12,
    tint: Object.freeze([0.55, 0.64, 0.65])
  }),
  stalker: Object.freeze({
    id: 'stalker',
    name: 'Gloam Stalker',
    championName: 'Gloam Packleader',
    kind: 'BLIGHTED HUNTER',
    maxHp: 48,
    speed: 3.05,
    attackRange: 1.35,
    attackDamage: 5,
    scale: 0.94,
    tint: Object.freeze([0.42, 0.66, 0.48])
  }),
  bulwark: Object.freeze({
    id: 'bulwark',
    name: 'Veinbound Bulwark',
    championName: 'Veinbound Castellan',
    kind: 'ARMORED DEAD',
    maxHp: 94,
    speed: 1.72,
    attackRange: 1.72,
    attackDamage: 10,
    scale: 1.28,
    tint: Object.freeze([0.68, 0.5, 0.38])
  })
});

export function createEnemyStats(type, champion = false) {
  const archetype = ENEMY_ARCHETYPES[type] ?? ENEMY_ARCHETYPES.gravebound;
  const hpMultiplier = champion ? 1.4 : 1;
  const damageMultiplier = champion ? 1.2 : 1;
  return {
    type: archetype.id,
    name: champion ? archetype.championName : archetype.name,
    kind: archetype.kind,
    maxHp: Math.round(archetype.maxHp * hpMultiplier),
    speed: archetype.speed,
    attackRange: archetype.attackRange,
    attackDamage: Math.round(archetype.attackDamage * damageMultiplier),
    collisionRadius: 0.52 * archetype.scale,
    scale: archetype.scale * (champion ? 1.08 : 1),
    tint: [...archetype.tint],
    champion
  };
}
