import { describe, expect, it } from "vitest";
import { resolvePatternActors, resolvePatternTiles } from "./tilePatterns";

describe("tile pattern simulation", () => {
  it("resolves cross tiles in Unity anchor/north/east/south/west order", () => {
    expect(resolvePatternTiles({ x: 4, y: 9 }, {
      patternKind: "Cross",
      size: 1,
      floorPolicy: "SameFloor"
    })).toEqual([
      { x: 4, y: 9 },
      { x: 4, y: 10 },
      { x: 5, y: 9 },
      { x: 4, y: 8 },
      { x: 3, y: 9 }
    ]);
  });

  it("resolves square actors by tile order, then ordinal actor ID, with a cap", () => {
    const result = resolvePatternActors({ x: 0, y: 0 }, 2, "Hostile", [
      { actorId: "z", definitionId: "slime", tile: { x: 0, y: 0 }, floor: 2, faction: "Hostile" },
      { actorId: "a", definitionId: "slime", tile: { x: 0, y: 0 }, floor: 2, faction: "Hostile" },
      { actorId: "north", definitionId: "slime", tile: { x: 0, y: 1 }, floor: 2, faction: "Hostile" },
      { actorId: "wrong-floor", definitionId: "slime", tile: { x: -1, y: -1 }, floor: 1, faction: "Hostile" }
    ], {
      patternKind: "SquareRadius",
      size: 1,
      floorPolicy: "SameFloor",
      maxTargets: 2
    });

    expect(result.map((actor) => actor.actorId)).toEqual(["a", "z"]);
  });
});
