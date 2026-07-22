import { describe, expect, it } from "vitest";
import { expectedDropTable, rollCoins, rollDropTable, rollDrops } from "./dropSystem";

function fixedRng(sequence: number[]): () => number {
  let index = 0;
  return () => {
    const value = sequence[index] ?? sequence[sequence.length - 1] ?? 0;
    index += 1;
    return value;
  };
}

describe("dropSystem", () => {
  it("rollDrops applies expected-value floor plus fractional roll", () => {
    const rng = fixedRng([0.1]);
    const loot = rollDrops([
      { itemId: "ore", chance: 0.5, min: 1, max: 1 }
    ], 3, rng, 1);

    // expected = 1.5, floor = 1, rng < 0.5 => +1
    expect(loot[0].qty).toBe(2);
  });

  it("rollDropTable resolves weighted and tertiary entries", () => {
    const rng = fixedRng([0.0, 0.6, 0.0, 0.05, 0.0]);
    const loot = rollDropTable({
      always: [{ itemId: "bone", min: 1, max: 1 }],
      main: {
        rolls: 1,
        slots: [
          { weight: 60, nothing: true },
          { weight: 40, itemId: "gel", min: 1, max: 1 }
        ]
      },
      tertiary: [{ itemId: "rare_shard", chance: 0.1, min: 1, max: 1 }]
    }, rng, 0);

    const byId = Object.fromEntries(loot.map((entry) => [entry.itemId, entry.qty]));
    expect(byId.bone).toBe(1);
    expect(byId.gel).toBe(1);
    expect(byId.rare_shard).toBe(1);
  });

  it("expectedDropTable produces expected quantities with rounding draw", () => {
    const rng = fixedRng([0.3]);
    const loot = expectedDropTable({
      always: [],
      main: {
        rolls: 1,
        slots: [
          { weight: 50, nothing: true },
          { weight: 50, itemId: "gel", min: 1, max: 1 }
        ]
      },
      tertiary: []
    }, 5, rng, 0);

    // expected gel = 5 * 1 * 0.5 * 1 = 2.5, rng < 0.5 => 3
    expect(loot.find((entry) => entry.itemId === "gel")?.qty).toBe(3);
  });

  it("rollCoins returns inclusive integer", () => {
    const rng = fixedRng([0.99]);
    expect(rollCoins(1, 4, rng)).toBe(4);
  });
});
