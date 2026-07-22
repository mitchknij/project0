import type { CombatSpatialSnapshot, CombatTileCoordinate, TilePatternDef } from "../types";

export const MAX_SAFE_TILE_OFFSET = 32;

function sameTile(left: CombatTileCoordinate, right: CombatTileCoordinate): boolean {
  return left.x === right.x && left.y === right.y;
}

function addUnique(tiles: CombatTileCoordinate[], tile: CombatTileCoordinate): void {
  if (!tiles.some((existing) => sameTile(existing, tile))) tiles.push(tile);
}

function offset(anchor: CombatTileCoordinate, x: number, y: number): CombatTileCoordinate | null {
  const nextX = anchor.x + x;
  const nextY = anchor.y + y;
  if (!Number.isSafeInteger(nextX) || !Number.isSafeInteger(nextY)) return null;
  return { x: nextX, y: nextY };
}

function addOffset(tiles: CombatTileCoordinate[], anchor: CombatTileCoordinate, x: number, y: number): void {
  const tile = offset(anchor, x, y);
  if (tile) addUnique(tiles, tile);
}

/**
 * Mirrors Unity TilePatternResolver ordering: anchor first; cross rings use
 * north/east/south/west; square patterns use negative-Y to positive-Y rows.
 */
export function resolvePatternTiles(anchor: CombatTileCoordinate, pattern: TilePatternDef): CombatTileCoordinate[] {
  const tiles: CombatTileCoordinate[] = [];
  const size = Math.min(Math.max(pattern.size ?? 1, 0), MAX_SAFE_TILE_OFFSET);

  if (pattern.patternKind === "SingleTile") {
    addUnique(tiles, anchor);
    return tiles;
  }

  if (pattern.patternKind === "Cross") {
    addUnique(tiles, anchor);
    for (let distance = 1; distance <= size; distance += 1) {
      addOffset(tiles, anchor, 0, distance);
      addOffset(tiles, anchor, distance, 0);
      addOffset(tiles, anchor, 0, -distance);
      addOffset(tiles, anchor, -distance, 0);
    }
    return tiles;
  }

  if (pattern.patternKind === "SquareRadius") {
    addUnique(tiles, anchor);
    for (let y = -size; y <= size; y += 1) {
      for (let x = -size; x <= size; x += 1) {
        if (x !== 0 || y !== 0) addOffset(tiles, anchor, x, y);
      }
    }
    return tiles;
  }

  for (const entry of pattern.customOffsets ?? []) {
    if (Math.abs(entry.x) > MAX_SAFE_TILE_OFFSET || Math.abs(entry.y) > MAX_SAFE_TILE_OFFSET) continue;
    addOffset(tiles, anchor, entry.x, entry.y);
  }
  return tiles;
}

/** Resolves targetable actors in deterministic tile then actor-ID order. */
export function resolvePatternActors(
  anchor: CombatTileCoordinate,
  anchorFloor: number,
  targetFaction: CombatSpatialSnapshot["faction"],
  candidates: CombatSpatialSnapshot[],
  pattern: TilePatternDef
): CombatSpatialSnapshot[] {
  if (pattern.floorPolicy !== "SameFloor") return [];

  const actors: CombatSpatialSnapshot[] = [];
  const seenActorIds = new Set<string>();
  for (const tile of resolvePatternTiles(anchor, pattern)) {
    const onTile = candidates
      .filter((actor) =>
        actor.alive !== false &&
        actor.targetable !== false &&
        actor.floor === anchorFloor &&
        actor.faction === targetFaction &&
        sameTile(actor.tile, tile)
      )
      .sort((left, right) => left.actorId.localeCompare(right.actorId));

    for (const actor of onTile) {
      if (seenActorIds.has(actor.actorId)) continue;
      seenActorIds.add(actor.actorId);
      actors.push(actor);
      if ((pattern.maxTargets ?? 0) > 0 && actors.length >= (pattern.maxTargets ?? 0)) return actors;
    }
  }
  return actors;
}
