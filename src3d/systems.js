export function distanceXZ(a, b) {
  return Math.hypot(a.x - b.x, a.z - b.z);
}

export function circlePenetration(a, radiusA, b, radiusB) {
  return radiusA + radiusB - distanceXZ(a, b);
}

export function pointSegmentDistanceXZ(point, start, end) {
  const dx = end.x - start.x;
  const dz = end.z - start.z;
  const lengthSquared = dx * dx + dz * dz;
  const unclamped = lengthSquared
    ? ((point.x - start.x) * dx + (point.z - start.z) * dz) / lengthSquared
    : 0;
  const t = Math.max(0, Math.min(1, unclamped));
  const closest = {
    x: start.x + dx * t,
    z: start.z + dz * t
  };
  return { distance: distanceXZ(point, closest), t, closest };
}

export function nearestAlive(origin, actors, maxDistance = Infinity) {
  let nearest = null;
  let nearestDistance = maxDistance;
  for (const actor of actors) {
    if (!actor.alive) continue;
    const distance = distanceXZ(origin, actor.position ?? actor.entity.getPosition());
    if (distance < nearestDistance) {
      nearest = actor;
      nearestDistance = distance;
    }
  }
  return nearest;
}

export function moveTowardXZ(position, destination, maxStep) {
  const dx = destination.x - position.x;
  const dz = destination.z - position.z;
  const distance = Math.hypot(dx, dz);
  if (!distance || distance <= maxStep) {
    return { x: destination.x, z: destination.z, arrived: true };
  }
  return {
    x: position.x + (dx / distance) * maxStep,
    z: position.z + (dz / distance) * maxStep,
    arrived: false
  };
}

export function raySphereDistance(origin, direction, center, radius) {
  const ox = origin.x - center.x;
  const oy = origin.y - center.y;
  const oz = origin.z - center.z;
  const b = ox * direction.x + oy * direction.y + oz * direction.z;
  const c = ox * ox + oy * oy + oz * oz - radius * radius;
  const discriminant = b * b - c;
  if (discriminant < 0) return null;
  const distance = -b - Math.sqrt(discriminant);
  return distance >= 0 ? distance : null;
}
