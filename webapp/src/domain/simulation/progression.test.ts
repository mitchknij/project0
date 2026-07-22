import { describe, expect, it } from "vitest";
import { applyCharacterXp, applySkillXp, totalXpForLevel, xpToNext } from "./progression";

describe("progression", () => {
  it("matches xp curve sample points", () => {
    expect(xpToNext(1)).toBe(100);
    expect(xpToNext(2)).toBe(224);
    expect(xpToNext(3)).toBe(411);
  });

  it("computes total xp from level 1", () => {
    expect(totalXpForLevel(1)).toBe(0);
    expect(totalXpForLevel(2)).toBe(xpToNext(1));
    expect(totalXpForLevel(3)).toBe(xpToNext(1) + xpToNext(2));
  });

  it("applies character xp with multi-level carry", () => {
    const result = applyCharacterXp(
      {
        id: "char_1",
        name: "Aria",
        classId: "Warrior",
        level: 1,
        xp: 0,
        mapId: "grass_1",
        inventory: [],
        maxInventorySlots: 20,
        activity: { kind: "Idle", targetId: null, startedAt: 0 },
        efficiency: null,
        freeStatPoints: 0,
        skills: {
          Combat: { level: 1, xp: 0 },
          Mining: { level: 1, xp: 0 },
          Chopping: { level: 1, xp: 0 },
          Gathering: { level: 1, xp: 0 }
        }
      },
      330
    );

    expect(result.newLevel).toBe(2);
    expect(result.character.xp).toBe(230);
    expect(result.character.freeStatPoints).toBe(1);
  });

  it("applies skill xp with carry", () => {
    const result = applySkillXp(
      {
        Combat: { level: 1, xp: 0 },
        Mining: { level: 1, xp: 0 },
        Chopping: { level: 1, xp: 0 },
        Gathering: { level: 1, xp: 0 }
      },
      "Combat",
      120
    );

    expect(result.skills.Combat.level).toBe(2);
    expect(result.skills.Combat.xp).toBe(20);
  });
});
