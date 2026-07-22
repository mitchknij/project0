import { describe, expect, it } from "vitest";
import { migratePersistedState } from "./saveSchema";
import unityLegacyAccount from "./fixtures/unity-legacy-account.json";
import unityLegacyAccountWithCharacter from "./fixtures/unity-legacy-account-with-character.json";

describe("save schema migration", () => {
  it("migrates legacy v0 object without schema version", () => {
    const migrated = migratePersistedState({
      account: {
        id: "acc_1",
        name: "Legacy",
        lastSeenAt: 100,
        autoLoot: true,
        autoCombatDisabled: false,
        bank: { coins: 20, slots: [], maxSlots: 48 },
        characters: [
          {
            id: "char_1",
            name: "Aria",
            classId: "Warrior",
            level: 1,
            xp: 0,
            mapId: "grass_1",
            inventory: [],
            maxInventorySlots: 16,
            activity: { kind: "Idle", targetId: null, startedAt: 100 },
            efficiency: null,
            skills: {
              Combat: { level: 1, xp: 0 },
              Mining: { level: 1, xp: 0 },
              Chopping: { level: 1, xp: 0 },
              Gathering: { level: 1, xp: 0 }
            }
          }
        ]
      }
    });

    expect(migrated?.saveSchemaVersion).toBe(1);
    expect(migrated?.account.characters[0].freeStatPoints).toBe(0);
  });

  it("migrates wrapped zustand persist payload", () => {
    const migrated = migratePersistedState({
      state: {
        saveSchemaVersion: 1,
        account: {
          id: "acc_2",
          name: "Wrapped",
          lastSeenAt: 200,
          autoLoot: true,
          autoCombatDisabled: false,
          bank: { coins: 20, slots: [], maxSlots: 48 },
          characters: [
            {
              id: "char_1",
              name: "Bex",
              classId: "Mage",
              level: 2,
              xp: 30,
              mapId: "grass_1",
              inventory: [],
              maxInventorySlots: 16,
              activity: { kind: "Idle", targetId: null, startedAt: 100 },
              efficiency: null,
              freeStatPoints: 2,
              skills: {
                Combat: { level: 1, xp: 0 },
                Mining: { level: 1, xp: 0 },
                Chopping: { level: 1, xp: 0 },
                Gathering: { level: 1, xp: 0 }
              }
            }
          ]
        },
        offlineReport: null
      }
    });

    expect(migrated?.saveSchemaVersion).toBe(1);
    expect(migrated?.account.name).toBe("Wrapped");
  });

  it("imports the Unity PascalCase legacy account fixture", () => {
    const migrated = migratePersistedState(unityLegacyAccount);

    expect(migrated?.saveSchemaVersion).toBe(1);
    expect(migrated?.account.id).toBe("legacy-account");
    expect(migrated?.account.name).toBe("Legacy Family");
    expect(migrated?.account.lastSeenAt).toBe(2000);
    expect(migrated?.account.bank.coins).toBe(42);
    expect(migrated?.account.autoLoot).toBe(false);
    expect(migrated?.account.characters).toEqual([]);
  });

  it("imports Unity PascalCase character state, inventory, activity, and skills", () => {
    const migrated = migratePersistedState(unityLegacyAccountWithCharacter);
    const character = migrated?.account.characters[0];

    expect(migrated?.account.bank.slots).toEqual([{ itemId: "copper_ore", qty: 9 }]);
    expect(migrated?.account.autoCombatDisabled).toBe(true);
    expect(character).toMatchObject({
      id: "legacy-mage",
      classId: "Mage",
      level: 7,
      inventory: [{ itemId: "gel", qty: 3 }],
      activity: { kind: "Fighting", targetId: "slime_1", startedAt: 1500 },
      skills: { Combat: { level: 3, xp: 12 }, Chopping: { level: 2, xp: 4 } }
    });
  });
});
