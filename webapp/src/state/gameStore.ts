import { create } from "zustand";
import { persist } from "zustand/middleware";
import { sampleContent, sampleOfflineBalance } from "../content/sampleContent";
import { createInitialActiveCombatState, tickActiveCombat } from "../domain/simulation/activeCombat";
import { rollCoins, rollDropTable, rollDrops } from "../domain/simulation/dropSystem";
import { simulateOffline } from "../domain/simulation/offline";
import { applyCharacterXp, applySkillXp } from "../domain/simulation/progression";
import type {
  Account,
  ActiveCombatState,
  ActivityKind,
  CombatEvent,
  CombatSkillDef,
  OfflineReport
} from "../domain/types";
import { CURRENT_SAVE_SCHEMA_VERSION, migratePersistedState } from "./saveSchema";

const now = Date.now();

const initialAccount: Account = {
  id: "acc_web_01",
  name: "IdleCloud Expedition",
  lastSeenAt: now - 2 * 60 * 60 * 1000,
  autoLoot: true,
  autoCombatDisabled: false,
  bank: {
    coins: 240,
    slots: [],
    maxSlots: 48
  },
  characters: [
    {
      id: "char_war_1",
      name: "Aria",
      classId: "Warrior",
      level: 3,
      xp: 60,
      mapId: "grass_1",
      inventory: [],
      maxInventorySlots: 24,
      activity: {
        kind: "Idle",
        targetId: null,
        startedAt: now
      },
      efficiency: null,
      skills: {
        Combat: { level: 3, xp: 0 },
        Mining: { level: 1, xp: 0 },
        Chopping: { level: 1, xp: 0 },
        Gathering: { level: 1, xp: 0 }
      }
    }
  ]
};

interface GameStore {
  saveSchemaVersion: number;
  account: Account;
  offlineReport: OfflineReport | null;
  combatState: ActiveCombatState;
  combatLog: CombatEvent[];
  combatCanFight: boolean;
  assignDemoActivity: (characterId: string, kind: ActivityKind) => void;
  simulateOfflineWindow: (hours: number) => void;
  runCombatTick: () => void;
  selectCombatTarget: () => void;
  triggerCombatSkill: (skillId: string) => void;
  toggleCombatRange: () => void;
  clearOfflineReport: () => void;
}

const demoSkills: CombatSkillDef[] = [
  {
    id: "power_strike",
    name: "Power Strike",
    cooldownMs: 3000,
    damageMultiplier: 1.8,
    manaCost: 8,
    range: 1,
    autoEnabled: true
  },
  {
    id: "quick_slash",
    name: "Quick Slash",
    cooldownMs: 1600,
    damageMultiplier: 1.1,
    manaCost: 4,
    range: 1,
    autoEnabled: true
  },
  {
    id: "delayed_bolt",
    name: "Delayed Bolt",
    cooldownMs: 3200,
    damageMultiplier: 2,
    manaCost: 7,
    range: 1,
    autoEnabled: true,
    timing: "ScheduledImpact",
    impactDelayTicks: 2
  },
  {
    id: "flame_burst",
    name: "Flame Burst",
    cooldownMs: 3000,
    damageMultiplier: 1,
    manaCost: 5,
    range: 1,
    autoEnabled: true,
    inflicts: [
      {
        kind: "Burn",
        magnitude: 0.2,
        durationTicks: 3,
        intervalTicks: 1
      }
    ]
  }
];

function createDeterministicRng(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (Math.imul(1664525, state) + 1013904223) >>> 0;
    return state / 4294967296;
  };
}

function pushStack(target: { itemId: string; qty: number }[], itemId: string, qty: number): void {
  if (qty <= 0) return;
  const existing = target.find((entry) => entry.itemId === itemId);
  if (existing) {
    existing.qty += qty;
    return;
  }
  target.push({ itemId, qty });
}

function buildDemoSnapshot(kind: ActivityKind): Account["characters"][number]["efficiency"] {
  if (kind === "Fighting") {
    return {
      kind,
      targetId: "slime_1",
      actionsPerHour: 420,
      xpPerAction: 9,
      coinsPerAction: 2.5,
      snapshotAt: Date.now(),
      mapDensity: 1,
      travelOverheadMs: 1200,
      survivalFactor: 0.96
    };
  }

  if (kind === "Mining") {
    return {
      kind,
      targetId: "copper_rock",
      actionsPerHour: 250,
      xpPerAction: 6,
      coinsPerAction: 0,
      snapshotAt: Date.now(),
      mapDensity: 1,
      travelOverheadMs: 0,
      survivalFactor: 1
    };
  }

  return null;
}

export const useGameStore = create<GameStore>()(persist((set) => ({
  saveSchemaVersion: CURRENT_SAVE_SCHEMA_VERSION,
  account: initialAccount,
  offlineReport: null,
  combatState: createInitialActiveCombatState(now, 110, 30, 85),
  combatLog: [],
  combatCanFight: true,
  assignDemoActivity: (characterId, kind) => {
    set((state) => {
      const characters: Account["characters"] = state.account.characters.map((character) => {
        if (character.id !== characterId) return character;

        const snapshot = buildDemoSnapshot(kind);
        if (kind === "Idle" || !snapshot) {
          return {
            ...character,
            activity: {
              kind: "Idle",
              targetId: null,
              startedAt: Date.now()
            },
            efficiency: null
          };
        }

        return {
          ...character,
          activity: {
            kind: snapshot.kind,
            targetId: snapshot.targetId,
            startedAt: Date.now()
          },
          efficiency: snapshot
        };
      });

      return {
        account: {
          ...state.account,
          characters
        }
      };
    });
  },
  simulateOfflineWindow: (hours) => {
    set((state) => {
      const nowMs = state.account.lastSeenAt + Math.floor(hours * 3_600_000);
      const simulation = simulateOffline(state.account, nowMs, sampleContent, sampleOfflineBalance);
      return {
        account: simulation.account,
        offlineReport: simulation.report
      };
    });
  },
  runCombatTick: () => {
    set((state) => {
      const timestamp = state.combatState.timestamp + 1200;
      const result = tickActiveCombat({
        timestamp,
        commandQueue: [],
        canFight: state.combatCanFight,
        targetAvailable: state.combatState.enemyHp > 0,
        activeEnemyId: "slime_1",
        state: state.combatState,
        stats: {
          hpMax: 110,
          manaMax: 30,
          baseAttack: 12,
          critChance: 0.15,
          critMultiplier: 1.6
        },
        enemy: {
          id: "slime_1",
          name: "Green Slime",
          hp: 85,
          attack: 3
        },
        skills: demoSkills,
        autoResumeGraceMs: 2500
      });

      let account = state.account;
      let combatState = result.state;
      let rewardEvents: CombatEvent[] = [];
      if (result.enemyKilled) {
        const monster = sampleContent.monsters.slime_1;
        const rng = createDeterministicRng(result.state.simulationTick + timestamp);
        const coinReward = rollCoins(monster.coinsMin, monster.coinsMax, rng);
        const itemRewards = monster.dropTable
          ? rollDropTable(monster.dropTable, rng, 0)
          : rollDrops(monster.drops, 1, rng, 1);

        const [currentCharacter, ...rest] = account.characters;
        if (currentCharacter) {
          const characterProgress = applyCharacterXp(currentCharacter, 9);
          const combatSkillProgress = applySkillXp(characterProgress.character.skills, "Combat", 9);
          const nextInventory = [...characterProgress.character.inventory];
          for (const stack of itemRewards) {
            pushStack(nextInventory, stack.itemId, stack.qty);
          }

          const rewardedCharacter = {
            ...characterProgress.character,
            inventory: nextInventory,
            skills: combatSkillProgress.skills
          };

          account = {
            ...account,
            bank: {
              ...account.bank,
              coins: account.bank.coins + coinReward
            },
            characters: [rewardedCharacter, ...rest]
          };

          rewardEvents = [
            { kind: "SkillResolved", skillId: "reward_xp", amount: 9, reason: "combat_xp_award" },
            { kind: "SkillResolved", skillId: "reward_coins", amount: coinReward, reason: "combat_coin_award" },
            ...itemRewards.map((stack) => ({
              kind: "SkillResolved" as const,
              skillId: `reward_item:${stack.itemId}`,
              amount: stack.qty,
              reason: "combat_loot_award"
            }))
          ];
        }

        combatState = {
          ...combatState,
          enemyHp: 85,
          targetId: null
        };
      }

      return {
        account,
        combatState,
        combatLog: [...state.combatLog, ...result.events, ...rewardEvents].slice(-24)
      };
    });
  },
  selectCombatTarget: () => {
    set((state) => {
      const timestamp = state.combatState.timestamp + 1;
      const result = tickActiveCombat({
        timestamp,
        commandQueue: [{ kind: "SelectTarget", targetId: "slime_1" }],
        canFight: state.combatCanFight,
        targetAvailable: state.combatState.enemyHp > 0,
        activeEnemyId: "slime_1",
        state: state.combatState,
        stats: {
          hpMax: 110,
          manaMax: 30,
          baseAttack: 12,
          critChance: 0.15,
          critMultiplier: 1.6
        },
        enemy: {
          id: "slime_1",
          name: "Green Slime",
          hp: 85,
          attack: 3
        },
        skills: demoSkills,
        autoResumeGraceMs: 2500
      });

      return {
        combatState: result.state,
        combatLog: [...state.combatLog, ...result.events].slice(-24)
      };
    });
  },
  triggerCombatSkill: (skillId) => {
    set((state) => {
      const timestamp = state.combatState.timestamp + 1;
      const result = tickActiveCombat({
        timestamp,
        commandQueue: [{ kind: "TriggerSkill", skillId }],
        canFight: state.combatCanFight,
        targetAvailable: state.combatState.enemyHp > 0,
        activeEnemyId: "slime_1",
        state: state.combatState,
        stats: {
          hpMax: 110,
          manaMax: 30,
          baseAttack: 12,
          critChance: 0.15,
          critMultiplier: 1.6
        },
        enemy: {
          id: "slime_1",
          name: "Green Slime",
          hp: 85,
          attack: 3
        },
        skills: demoSkills,
        autoResumeGraceMs: 2500
      });

      return {
        combatState: result.state,
        combatLog: [...state.combatLog, ...result.events].slice(-24)
      };
    });
  },
  toggleCombatRange: () => {
    set((state) => ({
      combatCanFight: !state.combatCanFight
    }));
  },
  clearOfflineReport: () => set({ offlineReport: null })
}), {
  name: "idlecloud-web-save-v1",
  version: CURRENT_SAVE_SCHEMA_VERSION,
  migrate: (persistedState) => {
    const migrated = migratePersistedState(persistedState);
    if (!migrated) return persistedState as GameStore;

    const previous = persistedState as Partial<GameStore>;
    const fallbackCombat = createInitialActiveCombatState(Date.now(), 110, 30, 85);
    const persistedCombat = previous.combatState ?? fallbackCombat;
    const normalizedCombat: ActiveCombatState = {
      ...fallbackCombat,
      ...persistedCombat,
      skillNextReadyAt: { ...(persistedCombat.skillNextReadyAt ?? {}) },
      lastCommandSequenceId: persistedCombat.lastCommandSequenceId ?? 0,
      scheduledEffects: [...(persistedCombat.scheduledEffects ?? [])],
      lastScheduledEffectSequenceId: persistedCombat.lastScheduledEffectSequenceId ?? 0,
      lastActionSequenceId: persistedCombat.lastActionSequenceId ?? 0,
      activeModifiers: [...(persistedCombat.activeModifiers ?? [])],
      activeStatuses: [...(persistedCombat.activeStatuses ?? [])],
      lastAutoSkillId: persistedCombat.lastAutoSkillId ?? null,
      lastAutoSkillFallbackReason: persistedCombat.lastAutoSkillFallbackReason ?? null
    };

    return {
      ...(persistedState as GameStore),
      account: migrated.account,
      offlineReport: migrated.offlineReport,
      combatState: normalizedCombat,
      combatLog: previous.combatLog ?? [],
      combatCanFight: previous.combatCanFight ?? true,
      saveSchemaVersion: CURRENT_SAVE_SCHEMA_VERSION
    };
  },
  partialize: (state) => ({
    saveSchemaVersion: state.saveSchemaVersion,
    account: state.account,
    offlineReport: state.offlineReport,
    combatState: state.combatState,
    combatCanFight: state.combatCanFight,
    combatLog: state.combatLog
  })
}));
