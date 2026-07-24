function weaponPower(state, items) { return items.find(item => item.id === state.hero.equipped.weapon)?.power ?? 0; }

export function simulateEncounter(state, enemy, items) {
  if (state.expedition.state !== 'traveling') return { state, result: 'not-ready', log: ['Start an expedition before entering combat.'], events: [] };
  let heroHealth = state.hero.health;
  let enemyHealth = enemy.health;
  const baseDamage = 15 + weaponPower(state, items);
  const events = [];
  const log = [];

  for (let round = 1; round <= 24 && heroHealth > 0 && enemyHealth > 0; round += 1) {
    const ability = round % 3 === 0 ? 'Ember Bolt' : 'Basic Strike';
    const damage = ability === 'Ember Bolt' ? baseDamage + 8 : baseDamage;
    enemyHealth = Math.max(0, enemyHealth - damage);
    events.push({ type: 'hero-attack', ability, damage, remainingHealth: enemyHealth, maxHealth: enemy.health });
    log.push(`${ability} dealt ${damage} to ${enemy.name}.`);
    if (enemyHealth <= 0) break;

    const incoming = enemy.damage;
    heroHealth = Math.max(0, heroHealth - incoming);
    events.push({ type: 'enemy-attack', ability: 'Counterattack', damage: incoming, remainingHealth: heroHealth, maxHealth: state.hero.maxHealth });
    log.push(`${enemy.name} struck Mara for ${incoming}.`);
  }

  const nextState = { ...state, hero: { ...state.hero, health: heroHealth } };
  return enemyHealth <= 0
    ? { state: nextState, result: 'victory', log, events }
    : { state: nextState, result: 'defeat', log, events };
}
