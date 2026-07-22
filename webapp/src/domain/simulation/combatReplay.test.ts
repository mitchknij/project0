import { describe, expect, it } from "vitest";
import { createInitialActiveCombatState } from "./activeCombat";
import { replayCombat } from "./combatReplay";

describe("replayCombat", () => {
  it("produces the same annotated event stream for the same fixed script", () => {
    const initialState = createInitialActiveCombatState(1_000, 100, 30, 60);
    const steps = [
      { timestamp: 1_100, commands: [{ kind: "SelectTarget" as const, targetId: "slime_1" }] },
      { timestamp: 1_200, commands: [{ kind: "TriggerSkill" as const, skillId: "delayed_bolt" }] },
      { timestamp: 2_400, commands: [] },
      { timestamp: 3_000, commands: [] }
    ];

    const createInput = (state: typeof initialState, step: (typeof steps)[number]) => ({
      timestamp: step.timestamp,
      commandQueue: step.commands,
      canFight: true,
      targetAvailable: state.enemyHp > 0,
      activeEnemyId: "slime_1",
      state,
      stats: { hpMax: 100, manaMax: 30, baseAttack: 10, critChance: 0.15, critMultiplier: 1.6 },
      enemy: { id: "slime_1", name: "Slime", hp: 60, attack: 0 },
      skills: [{
        id: "delayed_bolt",
        name: "Delayed Bolt",
        cooldownMs: 3_000,
        damageMultiplier: 2,
        manaCost: 5,
        range: 1,
        autoEnabled: false,
        timing: "ScheduledImpact" as const,
        impactDelayTicks: 2
      }],
      autoResumeGraceMs: 2_000
    });

    const first = replayCombat(createInput, initialState, steps);
    const second = replayCombat(createInput, initialState, steps);

    expect(second.eventSignature).toEqual(first.eventSignature);
    expect(first.eventSignature).toContain("TargetSelected");
    expect(first.eventSignature).toContain("CombatEffectScheduled:delayed_bolt:2");
    expect(first.eventSignature.some((entry) => entry.startsWith("SkillResolved:delayed_bolt"))).toBe(true);
    expect(first.finalState.lastCommandSequenceId).toBe(2);
  });
});
