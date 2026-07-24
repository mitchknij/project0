export const RECOVERY_WINDOW_MS = 24 * 60 * 60 * 1000;

export function startExpedition(state, routeId, now = Date.now()) {
  if (state.expedition.state !== 'town') return state;
  return { ...state, hero: { ...state.hero, health: state.hero.maxHealth }, pendingLoot: [], expedition: { state: 'traveling', routeId, nodeIndex: 0, startedAt: now, carried: { ore: 0, wood: 0 }, skipped: [] } };
}
export function advanceNode(state) { return { ...state, expedition: { ...state.expedition, nodeIndex: state.expedition.nodeIndex + 1 } }; }
export function gatherAndAdvance(state, resource, amount) {
  if (state.expedition.state !== 'traveling') return state;
  return advanceNode({ ...state, expedition: { ...state.expedition, carried: { ...state.expedition.carried, [resource]: state.expedition.carried[resource] + amount } } });
}
export function skipAndAdvance(state, nodeId) { return advanceNode({ ...state, expedition: { ...state.expedition, skipped: [...state.expedition.skipped, nodeId] } }); }
export function completeExpedition(state) {
  const { ore, wood } = state.expedition.carried;
  return { ...state, hero: { ...state.hero, xp: state.hero.xp + 100, health: state.hero.maxHealth }, town: { ...state.town, blacksmithLevel: Math.max(1, state.town.blacksmithLevel), resources: { ore: state.town.resources.ore + ore, wood: state.town.resources.wood + wood } }, quest: { ...state.quest, stage: 5, completed: true }, unlockedCompanions: [...new Set([...state.unlockedCompanions, 'scavenger'])], expedition: { state: 'town', routeId: null, nodeIndex: 0, startedAt: null, carried: { ore: 0, wood: 0 }, skipped: [] } };
}
export function defeatExpedition(state, now = Date.now()) {
  const carried = { ...state.expedition.carried };
  const hasCache = carried.ore > 0 || carried.wood > 0;
  return { ...state, hero: { ...state.hero, health: 0 }, recovery: hasCache ? { resources: carried, createdAt: now, expiresAt: now + RECOVERY_WINDOW_MS } : state.recovery, expedition: { state: 'defeated', routeId: state.expedition.routeId, nodeIndex: state.expedition.nodeIndex, startedAt: state.expedition.startedAt, carried: { ore: 0, wood: 0 }, skipped: state.expedition.skipped } };
}
export function returnToTown(state) { return { ...state, hero: { ...state.hero, health: state.hero.maxHealth }, expedition: { state: 'town', routeId: null, nodeIndex: 0, startedAt: null, carried: { ore: 0, wood: 0 }, skipped: [] } }; }
export function recoverCache(state, now = Date.now()) {
  if (!state.recovery || state.recovery.expiresAt <= now) return { ...state, recovery: null };
  return { ...state, town: { ...state.town, resources: { ore: state.town.resources.ore + state.recovery.resources.ore, wood: state.town.resources.wood + state.recovery.resources.wood } }, recovery: null };
}
export function pruneExpiredRecovery(state, now = Date.now()) { return state.recovery?.expiresAt <= now ? { ...state, recovery: null } : state; }
