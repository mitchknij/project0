import type { ContentBundle, OfflineBalanceConfig } from "../domain/types";

export const sampleOfflineBalance: OfflineBalanceConfig = {
  rate: 0.4,
  capMs: 24 * 60 * 60 * 1000,
  minimumDurationMs: 60 * 1000,
};

export const sampleContent: ContentBundle = {
  monsters: {
    slime_1: {
      id: "slime_1",
      name: "Green Slime",
      xp: 9,
      coinsMin: 1,
      coinsMax: 4,
      drops: [
        { itemId: "gel", chance: 0.65, min: 1, max: 2 },
        { itemId: "tiny_core", chance: 0.08, min: 1, max: 1 }
      ],
      dropTable: {
        always: [],
        main: {
          rolls: 1,
          slots: [
            { weight: 58, nothing: true },
            { weight: 35, itemId: "gel", min: 1, max: 2 },
            { weight: 7, itemId: "tiny_core", min: 1, max: 1 }
          ]
        },
        tertiary: [
          { itemId: "slime_membrane", chance: 0.05, min: 1, max: 1 }
        ]
      }
    }
  },
  nodes: {
    copper_rock: {
      id: "copper_rock",
      name: "Copper Rock",
      skill: "Mining",
      xp: 6,
      drops: [
        { itemId: "copper_ore", chance: 0.82, min: 1, max: 2 },
        { itemId: "stone", chance: 0.25, min: 1, max: 3 }
      ]
    }
  }
};
