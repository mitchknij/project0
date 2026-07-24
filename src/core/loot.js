export function lootChoicesFor(enemy, items) {
  const preferred = enemy.boss ? ['ashen_hook', 'veinward_plate', 'lantern_of_cinders'] : ['miners_ward', 'ember_lens', 'surveyors_charm'];
  return preferred.map(id => items.find(item => item.id === id)).filter(Boolean);
}
export function claimLoot(state, itemId) {
  if (!state.pendingLoot.includes(itemId)) return state;
  return { ...state, inventory: [...new Set([...state.inventory, itemId])], pendingLoot: [] };
}
