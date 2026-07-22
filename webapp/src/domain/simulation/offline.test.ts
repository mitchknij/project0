import { describe, expect, it } from "vitest";
import { sampleContent, sampleOfflineBalance } from "../../content/sampleContent";
import { simulateOffline } from "./offline";

describe("simulateOffline", () => {
  it("uses weighted drop table path for fighting snapshots", () => {
    const account = {
      id: "acc_1",
      name: "A",
      lastSeenAt: 0,
      autoLoot: true,
      autoCombatDisabled: false,
      bank: { coins: 0, slots: [], maxSlots: 48 },
      characters: [
        {
          id: "char_1",
          name: "Aria",
          classId: "Warrior" as const,
          level: 1,
          xp: 0,
          mapId: "grass_1",
          inventory: [],
          maxInventorySlots: 24,
          activity: { kind: "Fighting" as const, targetId: "slime_1", startedAt: 0 },
          efficiency: {
            kind: "Fighting" as const,
            targetId: "slime_1",
            actionsPerHour: 100,
            xpPerAction: 9,
            coinsPerAction: 2,
            snapshotAt: 0,
            mapDensity: 1,
            travelOverheadMs: 1000,
            survivalFactor: 1
          },
          freeStatPoints: 0,
          skills: {
            Combat: { level: 1, xp: 0 },
            Mining: { level: 1, xp: 0 },
            Chopping: { level: 1, xp: 0 },
            Gathering: { level: 1, xp: 0 }
          }
        }
      ]
    };

    const result = simulateOffline(account, 4 * 60 * 60 * 1000, sampleContent, sampleOfflineBalance);
    const report = result.report;
    expect(report).not.toBeNull();
    expect(report?.characters[0].actions).toBeGreaterThan(0);
    expect(result.account.characters[0].inventory.length).toBeGreaterThan(0);
  });
});
