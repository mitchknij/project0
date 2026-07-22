import { describe, expect, it } from "vitest";
import { createInitialActiveCombatState, tickActiveCombat } from "./activeCombat";

describe("tickActiveCombat", () => {
  it("rejects backward timestamps", () => {
    const baseState = createInitialActiveCombatState(1000, 100, 30, 40);
    const result = tickActiveCombat({
      timestamp: 900,
      commandQueue: [],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: baseState,
      enemy: { id: "slime_1", name: "Slime", hp: 40, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [],
      autoResumeGraceMs: 2000
    });

    expect(result.events.some((event) => event.reason === "timestamp_moved_backwards")).toBe(true);
  });

  it("selects target and resolves basic attack", () => {
    const baseState = createInitialActiveCombatState(1000, 100, 30, 40);
    const result = tickActiveCombat({
      timestamp: 2200,
      commandQueue: [{ kind: "SelectTarget", targetId: "slime_1" }],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: baseState,
      enemy: { id: "slime_1", name: "Slime", hp: 40, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [],
      autoResumeGraceMs: 2000
    });

    expect(result.events.some((event) => event.kind === "TargetSelected")).toBe(true);
    expect(result.events.some((event) => event.kind === "AttackResolved")).toBe(true);
    expect(result.state.enemyHp).toBeLessThan(40);
  });

  it("resolves manual skill and applies cooldown", () => {
    const baseState = {
      ...createInitialActiveCombatState(1000, 100, 30, 40),
      targetId: "slime_1"
    };

    const result = tickActiveCombat({
      timestamp: 1500,
      commandQueue: [{ kind: "TriggerSkill", skillId: "power_strike" }],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: baseState,
      enemy: { id: "slime_1", name: "Slime", hp: 40, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "power_strike",
          name: "Power Strike",
          cooldownMs: 3000,
          damageMultiplier: 1.8,
          manaCost: 8,
          range: 1,
          autoEnabled: true
        }
      ],
      autoResumeGraceMs: 2000
    });

    expect(result.events.some((event) => event.kind === "SkillResolved")).toBe(true);
    expect(result.state.skillNextReadyAt.power_strike).toBe(4500);
    expect(result.state.playerMana).toBe(22);
  });

  it("queues and resolves scheduled impact skills", () => {
    const baseState = {
      ...createInitialActiveCombatState(1000, 100, 30, 40),
      targetId: "slime_1"
    };

    const castResult = tickActiveCombat({
      timestamp: 1500,
      commandQueue: [{ kind: "TriggerSkill", skillId: "delayed_bolt" }],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: baseState,
      enemy: { id: "slime_1", name: "Slime", hp: 40, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "delayed_bolt",
          name: "Delayed Bolt",
          cooldownMs: 3000,
          damageMultiplier: 2,
          manaCost: 5,
          range: 1,
          autoEnabled: true,
          timing: "ScheduledImpact",
          impactDelayTicks: 2
        }
      ],
      autoResumeGraceMs: 2000
    });

    expect(castResult.events.some((event) => event.kind === "CombatEffectScheduled")).toBe(true);
    expect(castResult.state.scheduledEffects.length).toBe(1);

    const resolveResult = tickActiveCombat({
      timestamp: 2600,
      commandQueue: [],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: castResult.state,
      enemy: { id: "slime_1", name: "Slime", hp: 40, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "delayed_bolt",
          name: "Delayed Bolt",
          cooldownMs: 3000,
          damageMultiplier: 2,
          manaCost: 5,
          range: 1,
          autoEnabled: true,
          timing: "ScheduledImpact",
          impactDelayTicks: 2
        }
      ],
      autoResumeGraceMs: 2000
    });

    expect(resolveResult.events.some((event) => event.kind === "SkillResolved" && event.skillId === "delayed_bolt")).toBe(true);
    expect(resolveResult.state.scheduledEffects.length).toBe(0);
  });

  it("auto-selects first eligible auto skill when no manual command", () => {
    const baseState = {
      ...createInitialActiveCombatState(1000, 100, 30, 60),
      targetId: "slime_1"
    };

    const result = tickActiveCombat({
      timestamp: 2200,
      commandQueue: [],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: baseState,
      enemy: { id: "slime_1", name: "Slime", hp: 60, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "auto_disabled",
          name: "Disabled",
          cooldownMs: 1000,
          damageMultiplier: 2,
          manaCost: 5,
          range: 1,
          autoEnabled: false
        },
        {
          id: "auto_ready",
          name: "Auto Ready",
          cooldownMs: 1000,
          damageMultiplier: 1.5,
          manaCost: 4,
          range: 1,
          autoEnabled: true
        }
      ],
      autoResumeGraceMs: 2000
    });

    expect(result.events.some((event) => event.kind === "SkillResolved" && event.skillId === "auto_ready")).toBe(true);
  });

  it("applies guard modifier and expires it at authoritative tick", () => {
    const baseState = {
      ...createInitialActiveCombatState(1000, 100, 30, 60),
      targetId: "slime_1"
    };

    const applied = tickActiveCombat({
      timestamp: 1100,
      commandQueue: [{ kind: "TriggerSkill", skillId: "guard" }],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: baseState,
      enemy: { id: "slime_1", name: "Slime", hp: 60, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "guard",
          name: "Guard",
          cooldownMs: 3000,
          damageMultiplier: 0,
          manaCost: 4,
          range: 0,
          autoEnabled: true,
          modifier: {
            property: "Defense",
            operation: "FlatAdd",
            magnitude: 8,
            durationTicks: 2
          }
        }
      ],
      autoResumeGraceMs: 2000
    });

    expect(applied.state.activeModifiers.length).toBe(1);
    expect(applied.events.some((event) => event.kind === "TransientModifierApplied")).toBe(true);

    const waiting = tickActiveCombat({
      timestamp: 1200,
      commandQueue: [],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: applied.state,
      enemy: { id: "slime_1", name: "Slime", hp: 60, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "guard",
          name: "Guard",
          cooldownMs: 3000,
          damageMultiplier: 0,
          manaCost: 4,
          range: 0,
          autoEnabled: true,
          modifier: {
            property: "Defense",
            operation: "FlatAdd",
            magnitude: 8,
            durationTicks: 2
          }
        }
      ],
      autoResumeGraceMs: 2000
    });
    expect(waiting.state.activeModifiers.length).toBe(1);

    const expired = tickActiveCombat({
      timestamp: 1300,
      commandQueue: [],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: waiting.state,
      enemy: { id: "slime_1", name: "Slime", hp: 60, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "guard",
          name: "Guard",
          cooldownMs: 3000,
          damageMultiplier: 0,
          manaCost: 4,
          range: 0,
          autoEnabled: true,
          modifier: {
            property: "Defense",
            operation: "FlatAdd",
            magnitude: 8,
            durationTicks: 2
          }
        }
      ],
      autoResumeGraceMs: 2000
    });

    expect(expired.state.activeModifiers.length).toBe(0);
    expect(expired.events.some((event) => event.kind === "TransientModifierExpired" && event.skillId === "guard")).toBe(true);
  });

  it("applies burn status, ticks, and refreshes without stacking duplicate instances", () => {
    const statusSkill = {
      id: "flame_burst",
      name: "Flame Burst",
      cooldownMs: 3000,
      damageMultiplier: 1,
      manaCost: 5,
      range: 1,
      autoEnabled: true,
      inflicts: [
        {
          kind: "Burn" as const,
          magnitude: 0.2,
          durationTicks: 3,
          intervalTicks: 1
        }
      ]
    };

    const first = tickActiveCombat({
      timestamp: 1000,
      commandQueue: [{ kind: "TriggerSkill", skillId: "flame_burst" }],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: { ...createInitialActiveCombatState(900, 100, 30, 80), targetId: "slime_1" },
      enemy: { id: "slime_1", name: "Slime", hp: 80, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [statusSkill],
      autoResumeGraceMs: 2000
    });

    expect(first.state.activeStatuses.length).toBe(1);
    expect(first.events.some((event) => event.kind === "StatusApplied")).toBe(true);

    const ticked = tickActiveCombat({
      timestamp: 1100,
      commandQueue: [],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: first.state,
      enemy: { id: "slime_1", name: "Slime", hp: 80, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [statusSkill],
      autoResumeGraceMs: 2000
    });

    expect(ticked.events.some((event) => event.kind === "StatusTicked")).toBe(true);

    const refreshed = tickActiveCombat({
      timestamp: 1200,
      commandQueue: [{ kind: "TriggerSkill", skillId: "flame_burst" }],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: {
        ...ticked.state,
        skillNextReadyAt: {}
      },
      enemy: { id: "slime_1", name: "Slime", hp: 80, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [statusSkill],
      autoResumeGraceMs: 2000
    });

    expect(refreshed.state.activeStatuses.length).toBe(1);
    expect(refreshed.events.some((event) => event.kind === "StatusApplied" && event.reason === "refreshed")).toBe(true);
  });

  it("emits command and action sequence metadata for manual skill usage", () => {
    const result = tickActiveCombat({
      timestamp: 1500,
      commandQueue: [{ kind: "TriggerSkill", skillId: "power_strike" }],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: { ...createInitialActiveCombatState(1000, 100, 30, 40), targetId: "slime_1" },
      enemy: { id: "slime_1", name: "Slime", hp: 40, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "power_strike",
          name: "Power Strike",
          cooldownMs: 3000,
          damageMultiplier: 1.8,
          manaCost: 8,
          range: 1,
          autoEnabled: true
        }
      ],
      autoResumeGraceMs: 2000
    });

    const resolved = result.events.find((event) => event.kind === "SkillResolved" && event.skillId === "power_strike");
    expect(resolved?.commandSequenceId).toBe(1);
    expect(typeof resolved?.actionSequenceId).toBe("number");
    expect(result.state.lastCommandSequenceId).toBe(1);
  });

  it("records auto fallback reason when no auto skill is eligible", () => {
    const result = tickActiveCombat({
      timestamp: 2200,
      commandQueue: [],
      canFight: true,
      targetAvailable: true,
      activeEnemyId: "slime_1",
      state: { ...createInitialActiveCombatState(1000, 100, 1, 60), targetId: "slime_1" },
      enemy: { id: "slime_1", name: "Slime", hp: 60, attack: 2 },
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      skills: [
        {
          id: "expensive_skill",
          name: "Expensive",
          cooldownMs: 1000,
          damageMultiplier: 2,
          manaCost: 50,
          range: 1,
          autoEnabled: true
        }
      ],
      autoResumeGraceMs: 2000
    });

    expect(result.state.lastAutoSkillId).toBeNull();
    expect(result.state.lastAutoSkillFallbackReason).toBe("no_eligible_slotted_skill");
  });
});
