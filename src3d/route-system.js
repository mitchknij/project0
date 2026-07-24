const ROUTE_IDS = Object.freeze([
  'hollow-vein',
  'ironroot-grove',
  'forgotten-ossuary'
]);

const EMPTY_REWARD = Object.freeze({
  resources: Object.freeze({ ore: 0, wood: 0, herb: 0, essence: 0 }),
  rareReward: null,
  lore: Object.freeze([])
});

export const ROUTES = Object.freeze([
  Object.freeze({
    id: 'hollow-vein',
    name: 'Hollow Vein',
    kind: 'main',
    difficulty: 'standard',
    requires: Object.freeze([]),
    reward: Object.freeze({
      resources: Object.freeze({ ore: 6, wood: 3, herb: 2, essence: 1 }),
      rareReward: null,
      lore: Object.freeze(['The first excavation broke into something older than the mine.'])
    })
  }),
  Object.freeze({
    id: 'ironroot-grove',
    name: 'Ironroot Grove',
    kind: 'optional-resource',
    difficulty: 'standard',
    requires: Object.freeze(['hollow-vein']),
    reward: Object.freeze({
      resources: Object.freeze({ ore: 12, wood: 18, herb: 8, essence: 2 }),
      rareReward: null,
      lore: Object.freeze(['Ironroot trees drink the mineral-rich water beneath Hollow Vein.'])
    })
  }),
  Object.freeze({
    id: 'forgotten-ossuary',
    name: 'Forgotten Ossuary',
    kind: 'optional-rare-lore',
    difficulty: 'hard',
    requires: Object.freeze(['hollow-vein']),
    reward: Object.freeze({
      resources: Object.freeze({ ore: 3, wood: 0, herb: 2, essence: 10 }),
      rareReward: 'ossuary-reliquary',
      lore: Object.freeze([
        'The Overseer was appointed to guard the miners from what slept below.',
        'The sealed reliquary bears the crest of Warden Refuge.'
      ])
    })
  })
]);

const ROUTE_BY_ID = new Map(ROUTES.map((route) => [route.id, route]));

export function createRouteProgress() {
  return {
    location: 'refuge',
    selectedRouteId: null,
    activeRouteId: null,
    completedRouteIds: [],
    claimedRewardIds: []
  };
}

export function normalizeRouteProgress(progress) {
  if (!isPlainObject(progress)) return createRouteProgress();

  const completedRouteIds = normalizeRouteIds(progress.completedRouteIds);
  const claimedRewardIds = normalizeRouteIds(progress.claimedRewardIds)
    .filter((id) => completedRouteIds.includes(id));
  const activeRouteId = validRouteId(progress.activeRouteId);
  const activeIsUnlocked = activeRouteId
    ? routeIsUnlocked(activeRouteId, completedRouteIds)
      && !completedRouteIds.includes(activeRouteId)
    : false;

  if (progress.location === 'route' && activeIsUnlocked) {
    return {
      location: 'route',
      selectedRouteId: null,
      activeRouteId,
      completedRouteIds,
      claimedRewardIds
    };
  }

  const selectedRouteId = validRouteId(progress.selectedRouteId);
  const selectedIsAvailable = selectedRouteId
    ? routeIsUnlocked(selectedRouteId, completedRouteIds)
      && !completedRouteIds.includes(selectedRouteId)
    : false;

  return {
    location: 'refuge',
    selectedRouteId: selectedIsAvailable ? selectedRouteId : null,
    activeRouteId: null,
    completedRouteIds,
    claimedRewardIds
  };
}

export function getAvailableRoutes(progress) {
  const current = normalizeRouteProgress(progress);
  if (current.location !== 'refuge') return [];

  return ROUTES
    .filter((route) => (
      !current.completedRouteIds.includes(route.id)
      && routeIsUnlocked(route.id, current.completedRouteIds)
    ))
    .map(cloneRoute);
}

export function selectRoute(progress, routeId) {
  const current = normalizeRouteProgress(progress);
  if (current.location !== 'refuge') {
    return failure(current, 'not-at-refuge');
  }

  const route = ROUTE_BY_ID.get(routeId);
  if (!route) return failure(current, 'unknown-route');
  if (current.completedRouteIds.includes(route.id)) {
    return failure(current, 'route-completed');
  }
  if (!routeIsUnlocked(route.id, current.completedRouteIds)) {
    return failure(current, 'route-locked');
  }

  return {
    progress: {
      ...current,
      selectedRouteId: route.id
    },
    success: true
  };
}

export function confirmRouteSelection(progress) {
  const current = normalizeRouteProgress(progress);
  if (current.location !== 'refuge') {
    return failure(current, 'not-at-refuge');
  }
  if (!current.selectedRouteId) {
    return failure(current, 'no-route-selected');
  }

  return {
    progress: {
      ...current,
      location: 'route',
      selectedRouteId: null,
      activeRouteId: current.selectedRouteId
    },
    success: true,
    route: cloneRoute(ROUTE_BY_ID.get(current.selectedRouteId))
  };
}

export function completeActiveRoute(progress) {
  const current = normalizeRouteProgress(progress);
  if (current.location !== 'route' || !current.activeRouteId) {
    return {
      ...failure(current, 'no-active-route'),
      reward: cloneReward(EMPTY_REWARD)
    };
  }

  const routeId = current.activeRouteId;
  const firstCompletion = !current.completedRouteIds.includes(routeId);
  const rewardAvailable = firstCompletion && !current.claimedRewardIds.includes(routeId);
  const completedRouteIds = firstCompletion
    ? canonicalRouteIds([...current.completedRouteIds, routeId])
    : current.completedRouteIds.slice();
  const claimedRewardIds = rewardAvailable
    ? canonicalRouteIds([...current.claimedRewardIds, routeId])
    : current.claimedRewardIds.slice();

  return {
    progress: {
      location: 'refuge',
      selectedRouteId: null,
      activeRouteId: null,
      completedRouteIds,
      claimedRewardIds
    },
    success: true,
    firstCompletion,
    reward: rewardAvailable
      ? cloneReward(ROUTE_BY_ID.get(routeId).reward)
      : cloneReward(EMPTY_REWARD)
  };
}

function routeIsUnlocked(routeId, completedRouteIds) {
  const route = ROUTE_BY_ID.get(routeId);
  return Boolean(route)
    && route.requires.every((requiredId) => completedRouteIds.includes(requiredId));
}

function normalizeRouteIds(value) {
  if (!Array.isArray(value)) return [];
  return canonicalRouteIds(value.filter((id) => typeof id === 'string'));
}

function canonicalRouteIds(ids) {
  const unique = new Set(ids);
  return ROUTE_IDS.filter((id) => unique.has(id));
}

function validRouteId(value) {
  return typeof value === 'string' && ROUTE_BY_ID.has(value) ? value : null;
}

function cloneRoute(route) {
  return {
    id: route.id,
    name: route.name,
    kind: route.kind,
    difficulty: route.difficulty,
    requires: route.requires.slice(),
    reward: cloneReward(route.reward)
  };
}

function cloneReward(reward) {
  return {
    resources: { ...reward.resources },
    rareReward: reward.rareReward,
    lore: reward.lore.slice()
  };
}

function failure(progress, reason) {
  return {
    progress,
    success: false,
    reason
  };
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
