import type {
  ActiveCombatStatus,
  ActiveCombatInput,
  ActiveCombatResult,
  CombatEvent,
  CombatSkillDef,
  ScheduledCombatEffect,
  TransientCombatModifier
} from "../types";

function buildEvent(event: CombatEvent): CombatEvent {
  return event;
}

function findSkill(skills: CombatSkillDef[], skillId: string | undefined): CombatSkillDef | undefined {
  if (!skillId) return undefined;
  return skills.find((skill) => skill.id === skillId);
}

function random01(seed: number): number {
  let state = seed >>> 0;
  state = (Math.imul(1664525, state) + 1013904223) >>> 0;
  return state / 4294967296;
}

function resolveDamage(baseAttack: number, multiplier: number, tick: number): { amount: number; critical: boolean } {
  const roll = random01(tick + baseAttack * 17);
  const critical = roll < 0.15;
  const critMultiplier = critical ? 1.6 : 1.0;
  const amount = Math.max(1, Math.floor(baseAttack * multiplier * critMultiplier));
  return { amount, critical };
}

function requestMovement(events: CombatEvent[], reason: string, targetId: string | null): void {
  events.push(buildEvent({ kind: "MovementRequested", reason, targetId: targetId ?? undefined }));
}

function reject(
  events: CombatEvent[],
  reason: string,
  skillId?: string,
  targetId?: string,
  commandSequenceId?: number,
  actionSequenceId?: number
): void {
  events.push(buildEvent({ kind: "ActionRejected", reason, skillId, targetId, commandSequenceId, actionSequenceId }));
}

function nextCommandSequenceId(state: { lastCommandSequenceId: number }): number {
  state.lastCommandSequenceId += 1;
  return state.lastCommandSequenceId;
}

function nextScheduledEffectSequenceId(state: {
  lastScheduledEffectSequenceId: number;
}): number {
  state.lastScheduledEffectSequenceId += 1;
  return state.lastScheduledEffectSequenceId;
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function composeModifierValue(
  persistentValue: number,
  property: TransientCombatModifier["property"],
  actorId: string,
  simulationTick: number,
  modifiers: TransientCombatModifier[],
  minimum: number,
  maximum: number
): number {
  const ordered = modifiers
    .filter((modifier) =>
      modifier &&
      modifier.property === property &&
      modifier.targetActorId === actorId &&
      modifier.startTick <= simulationTick &&
      simulationTick < modifier.endTick
    )
    .sort((left, right) => {
      if (left.definitionId !== right.definitionId) {
        return left.definitionId.localeCompare(right.definitionId);
      }
      return left.applicationSequenceId - right.applicationSequenceId;
    });

  let value = persistentValue;
  for (const modifier of ordered) {
    if (modifier.operation === "FlatAdd") value += modifier.magnitude;
  }

  let additivePercent = 0;
  for (const modifier of ordered) {
    if (modifier.operation === "AdditivePercent") additivePercent += modifier.magnitude;
  }
  value *= 1 + additivePercent;

  for (const modifier of ordered) {
    if (modifier.operation === "MultiplicativePercent") value *= 1 + modifier.magnitude;
  }

  for (const modifier of ordered) {
    if (modifier.operation === "Override") value = modifier.magnitude;
  }

  return clamp(value, minimum, maximum);
}

function selectAutoSkill(
  skills: CombatSkillDef[],
  timestamp: number,
  canFight: boolean,
  state: {
    skillNextReadyAt: Record<string, number>;
    playerMana: number;
    targetId: string | null;
  }
): { skill?: CombatSkillDef; fallbackReason?: string } {
  if (!state.targetId) return { fallbackReason: "no_target" };
  if (!canFight) return { fallbackReason: "target_out_of_range" };
  for (const skill of skills) {
    if (!skill || skill.autoEnabled === false) continue;
    if (skill.damageMultiplier <= 0) continue;
    if (state.playerMana < skill.manaCost) continue;
    const readyAt = state.skillNextReadyAt[skill.id] ?? 0;
    if (timestamp < readyAt) continue;
    return { skill };
  }
  return { fallbackReason: "no_eligible_slotted_skill" };
}

function resolveSkillImpactDamage(
  state: {
    enemyHp: number;
    playerMana: number;
    skillNextReadyAt: Record<string, number>;
    simulationTick: number;
    activeModifiers: TransientCombatModifier[];
  },
  events: CombatEvent[],
  skill: CombatSkillDef,
  baseAttack: number,
  simulationTick: number,
  timestamp: number,
  targetId: string,
  commandSequenceId?: number,
  actionSequenceId?: number
): void {
  const damageMultiplier = composeModifierValue(
    skill.damageMultiplier,
    "Damage",
    "player",
    state.simulationTick,
    state.activeModifiers,
    0,
    100
  );
  const skillDamage = resolveDamage(baseAttack, damageMultiplier, simulationTick);
  state.playerMana = Math.max(0, state.playerMana - skill.manaCost);
  state.enemyHp = Math.max(0, state.enemyHp - skillDamage.amount);
  state.skillNextReadyAt[skill.id] = timestamp + skill.cooldownMs;
  events.push(buildEvent({
    kind: "SkillResolved",
    amount: skillDamage.amount,
    skillId: skill.id,
    critical: skillDamage.critical,
    targetId,
    commandSequenceId,
    actionSequenceId
  }));
  events.push(buildEvent({
    kind: "CooldownStarted",
    skillId: skill.id,
    targetId,
    commandSequenceId,
    actionSequenceId
  }));
}

function scheduleEffect(
  state: {
    simulationTick: number;
    scheduledEffects: ScheduledCombatEffect[];
    lastScheduledEffectSequenceId: number;
  },
  effect: Omit<ScheduledCombatEffect, "sequenceId" | "executeTick"> & { delayTicks: number }
): void {
  const sequenceId = nextScheduledEffectSequenceId(state);
  state.scheduledEffects.push({
    sequenceId,
    executeTick: state.simulationTick + Math.max(1, effect.delayTicks),
    kind: effect.kind,
    referenceId: effect.referenceId,
    sourceActorId: effect.sourceActorId,
    targetActorId: effect.targetActorId,
    skillId: effect.skillId,
    commandSequenceId: effect.commandSequenceId,
    actionSequenceId: effect.actionSequenceId
  });
}

function applyModifierFromSkill(
  state: {
    simulationTick: number;
    activeModifiers: TransientCombatModifier[];
    lastActionSequenceId: number;
    scheduledEffects: ScheduledCombatEffect[];
    lastScheduledEffectSequenceId: number;
  },
  events: CombatEvent[],
  skill: CombatSkillDef,
  targetId: string,
  commandSequenceId?: number,
  actionSequenceId?: number
): void {
  if (!skill.modifier) return;

  const resolvedActionSequenceId = actionSequenceId ?? state.lastActionSequenceId + 1;
  state.lastActionSequenceId = Math.max(state.lastActionSequenceId, resolvedActionSequenceId);
  const instanceId = `modifier:${resolvedActionSequenceId}:${skill.id}`;

  state.activeModifiers = state.activeModifiers.filter(
    (modifier) => !(modifier.definitionId === skill.id && modifier.targetActorId === targetId)
  );

  const modifier: TransientCombatModifier = {
    instanceId,
    definitionId: skill.id,
    sourceActorId: "player",
    targetActorId: targetId,
    property: skill.modifier.property,
    operation: skill.modifier.operation,
    magnitude: skill.modifier.magnitude,
    startTick: state.simulationTick,
    endTick: state.simulationTick + Math.max(1, skill.modifier.durationTicks),
    applicationSequenceId: resolvedActionSequenceId
  };

  state.activeModifiers.push(modifier);
  scheduleEffect(state, {
    delayTicks: Math.max(1, skill.modifier.durationTicks),
    kind: "ExpireModifier",
    referenceId: modifier.instanceId,
    sourceActorId: modifier.sourceActorId,
    targetActorId: modifier.targetActorId,
    skillId: skill.id,
    commandSequenceId,
    actionSequenceId: resolvedActionSequenceId
  });

  events.push(buildEvent({
    kind: "TransientModifierApplied",
    targetId,
    skillId: skill.id,
    amount: Math.round(skill.modifier.magnitude),
    commandSequenceId,
    actionSequenceId: resolvedActionSequenceId
  }));
}

function applyStatusesFromSkill(
  state: {
    simulationTick: number;
    activeStatuses: ActiveCombatStatus[];
    lastActionSequenceId: number;
    scheduledEffects: ScheduledCombatEffect[];
    lastScheduledEffectSequenceId: number;
  },
  events: CombatEvent[],
  skill: CombatSkillDef,
  targetId: string,
  commandSequenceId?: number,
  actionSequenceId?: number
): void {
  for (const statusDef of skill.inflicts ?? []) {
    const resolvedActionSequenceId = actionSequenceId ?? state.lastActionSequenceId + 1;
    state.lastActionSequenceId = Math.max(state.lastActionSequenceId, resolvedActionSequenceId);
    const instanceId = `status:${resolvedActionSequenceId}:${skill.id}:${statusDef.kind}`;
    const existing = state.activeStatuses.find(
      (status) =>
        status.definitionId === skill.id &&
        status.targetActorId === targetId &&
        status.kind === statusDef.kind
    );
    const interval = Math.max(1, statusDef.intervalTicks);
    if (existing) {
      existing.magnitude = statusDef.magnitude;
      existing.endTick = state.simulationTick + Math.max(interval, statusDef.durationTicks);
      existing.nextTick = state.simulationTick + interval;
      existing.intervalTicks = interval;
      existing.applicationSequenceId = resolvedActionSequenceId;
      state.scheduledEffects = state.scheduledEffects.filter(
        (effect) => !(effect.kind === "TickStatus" && effect.referenceId === existing.instanceId)
      );
      scheduleEffect(state, {
        delayTicks: interval,
        kind: "TickStatus",
        referenceId: existing.instanceId,
        sourceActorId: existing.sourceActorId,
        targetActorId: existing.targetActorId,
        skillId: existing.definitionId,
        commandSequenceId,
        actionSequenceId: resolvedActionSequenceId
      });
      events.push(buildEvent({
        kind: "StatusApplied",
        targetId,
        skillId: skill.id,
        reason: "refreshed",
        commandSequenceId,
        actionSequenceId: resolvedActionSequenceId
      }));
      continue;
    }

    const status: ActiveCombatStatus = {
      instanceId,
      definitionId: skill.id,
      sourceActorId: "player",
      targetActorId: targetId,
      kind: statusDef.kind,
      magnitude: statusDef.magnitude,
      startTick: state.simulationTick,
      endTick: state.simulationTick + Math.max(interval, statusDef.durationTicks),
      nextTick: state.simulationTick + interval,
      intervalTicks: interval,
      applicationSequenceId: resolvedActionSequenceId
    };

    state.activeStatuses.push(status);
    scheduleEffect(state, {
      delayTicks: interval,
      kind: "TickStatus",
      referenceId: status.instanceId,
      sourceActorId: status.sourceActorId,
      targetActorId: status.targetActorId,
      skillId: status.definitionId,
      commandSequenceId,
      actionSequenceId: resolvedActionSequenceId
    });
    events.push(buildEvent({
      kind: "StatusApplied",
      targetId,
      skillId: skill.id,
      reason: "applied",
      commandSequenceId,
      actionSequenceId: resolvedActionSequenceId
    }));
  }
}

function queueScheduledImpact(
  state: {
    simulationTick: number;
    scheduledEffects: ScheduledCombatEffect[];
    lastScheduledEffectSequenceId: number;
    skillNextReadyAt: Record<string, number>;
  },
  events: CombatEvent[],
  skill: CombatSkillDef,
  timestamp: number,
  targetId: string,
  commandSequenceId?: number,
  actionSequenceId?: number
): void {
  const delayTicks = Math.max(1, skill.impactDelayTicks ?? 1);
  const sequenceId = nextScheduledEffectSequenceId(state);
  state.scheduledEffects.push({
    sequenceId,
    executeTick: state.simulationTick + delayTicks,
    kind: "ResolveSkillImpact",
    sourceActorId: "player",
    targetActorId: targetId,
    skillId: skill.id,
    commandSequenceId,
    actionSequenceId
  });
  state.skillNextReadyAt[skill.id] = timestamp + skill.cooldownMs;
  events.push(buildEvent({
    kind: "SkillCastStarted",
    skillId: skill.id,
    targetId,
    durationTicks: delayTicks,
    commandSequenceId,
    actionSequenceId
  }));
  events.push(buildEvent({
    kind: "CombatEffectScheduled",
    skillId: skill.id,
    targetId,
    amount: delayTicks,
    durationTicks: delayTicks,
    commandSequenceId,
    actionSequenceId
  }));
  events.push(buildEvent({
    kind: "CooldownStarted",
    skillId: skill.id,
    targetId,
    commandSequenceId,
    actionSequenceId
  }));
}

function resolveDueScheduledEffects(
  state: {
    simulationTick: number;
    scheduledEffects: ScheduledCombatEffect[];
    enemyHp: number;
    activeModifiers: TransientCombatModifier[];
    activeStatuses: ActiveCombatStatus[];
  },
  events: CombatEvent[],
  skills: CombatSkillDef[],
  baseAttack: number
): void {
  state.scheduledEffects.sort((left, right) => {
    if (left.executeTick !== right.executeTick) return left.executeTick - right.executeTick;
    return left.sequenceId - right.sequenceId;
  });

  while (state.scheduledEffects.length > 0 && state.scheduledEffects[0].executeTick <= state.simulationTick) {
    const effect = state.scheduledEffects.shift()!;
    const skill = findSkill(skills, effect.skillId);
    if (effect.kind === "ExpireModifier") {
      const modifier = state.activeModifiers.find((item) => item.instanceId === effect.referenceId);
      if (!modifier) continue;
      state.activeModifiers = state.activeModifiers.filter((item) => item.instanceId !== modifier.instanceId);
      events.push(buildEvent({
        kind: "TransientModifierExpired",
        targetId: modifier.targetActorId,
        skillId: modifier.definitionId,
        actionSequenceId: modifier.applicationSequenceId
      }));
      continue;
    }

    if (effect.kind === "TickStatus") {
      const status = state.activeStatuses.find((item) => item.instanceId === effect.referenceId);
      if (!status) continue;
      if (state.enemyHp <= 0) {
        state.activeStatuses = state.activeStatuses.filter((item) => item.instanceId !== status.instanceId);
        events.push(buildEvent({
          kind: "StatusExpired",
          targetId: status.targetActorId,
          skillId: status.definitionId,
          reason: "target_already_defeated",
          actionSequenceId: status.applicationSequenceId
        }));
        continue;
      }

      const tickDamage = Math.max(1, Math.floor(baseAttack * status.magnitude));
      state.enemyHp = Math.max(0, state.enemyHp - tickDamage);
      events.push(buildEvent({
        kind: "StatusTicked",
        targetId: status.targetActorId,
        skillId: status.definitionId,
        amount: tickDamage,
        actionSequenceId: status.applicationSequenceId
      }));

      status.nextTick = status.nextTick + status.intervalTicks;
      if (status.nextTick <= status.endTick && state.enemyHp > 0) {
        const queueState = state as {
          simulationTick: number;
          scheduledEffects: ScheduledCombatEffect[];
          lastScheduledEffectSequenceId: number;
        };
        scheduleEffect(queueState, {
          delayTicks: status.intervalTicks,
          kind: "TickStatus",
          referenceId: status.instanceId,
          sourceActorId: status.sourceActorId,
          targetActorId: status.targetActorId,
          skillId: status.definitionId,
          actionSequenceId: status.applicationSequenceId
        });
      } else {
        state.activeStatuses = state.activeStatuses.filter((item) => item.instanceId !== status.instanceId);
        events.push(buildEvent({
          kind: "StatusExpired",
          targetId: status.targetActorId,
          skillId: status.definitionId,
          reason: "duration_complete",
          actionSequenceId: status.applicationSequenceId
        }));
      }
      continue;
    }

    if (!skill || !effect.targetActorId) {
      events.push(buildEvent({
        kind: "CombatEffectCancelled",
        skillId: effect.skillId,
        targetId: effect.targetActorId ?? undefined,
        reason: "invalid_target",
        commandSequenceId: effect.commandSequenceId,
        actionSequenceId: effect.actionSequenceId
      }));
      continue;
    }
    if (state.enemyHp <= 0) {
      events.push(buildEvent({
        kind: "CombatEffectCancelled",
        skillId: effect.skillId,
        targetId: effect.targetActorId,
        reason: "target_already_defeated",
        commandSequenceId: effect.commandSequenceId,
        actionSequenceId: effect.actionSequenceId
      }));
      continue;
    }

    const resolved = resolveDamage(baseAttack, skill.damageMultiplier, state.simulationTick + effect.sequenceId);
    state.enemyHp = Math.max(0, state.enemyHp - resolved.amount);
    events.push(buildEvent({
      kind: "SkillResolved",
      amount: resolved.amount,
      skillId: skill.id,
      critical: resolved.critical,
      targetId: effect.targetActorId,
      commandSequenceId: effect.commandSequenceId,
      actionSequenceId: effect.actionSequenceId
    }));
  }
}

export function createInitialActiveCombatState(nowMs: number, playerHp: number, playerMana: number, enemyHp: number) {
  return {
    timestamp: nowMs,
    simulationTick: 0,
    playerHp,
    playerMana,
    enemyHp,
    targetId: null,
    autoCombatStopped: false,
    autoCombatStoppedAt: nowMs,
    playerNextAttackAt: nowMs,
    skillNextReadyAt: {},
    scheduledEffects: [],
    lastCommandSequenceId: 0,
    lastScheduledEffectSequenceId: 0,
    lastActionSequenceId: 0,
    activeModifiers: [],
    activeStatuses: [],
    lastAutoSkillId: null,
    lastAutoSkillFallbackReason: null
  };
}

export function tickActiveCombat(input: ActiveCombatInput): ActiveCombatResult {
  const events: CombatEvent[] = [];
  const state = {
    ...input.state,
    simulationTick: input.state.simulationTick + 1,
    skillNextReadyAt: { ...input.state.skillNextReadyAt },
    scheduledEffects: [...(input.state.scheduledEffects ?? [])],
    activeModifiers: [...(input.state.activeModifiers ?? [])],
    activeStatuses: [...(input.state.activeStatuses ?? [])],
    lastAutoSkillId: input.state.lastAutoSkillId ?? null,
    lastAutoSkillFallbackReason: input.state.lastAutoSkillFallbackReason ?? null
  };

  if (input.timestamp < state.timestamp) {
    reject(events, "timestamp_moved_backwards");
    return { state, events, enemyKilled: state.enemyHp <= 0, playerDefeated: state.playerHp <= 0 };
  }

  resolveDueScheduledEffects(state, events, input.skills, input.stats.baseAttack);

  for (const command of input.commandQueue) {
    const commandSequenceId = nextCommandSequenceId(state);
    if (command.kind === "MoveIntent") {
      state.autoCombatStopped = true;
      state.autoCombatStoppedAt = input.timestamp;
      events.push(buildEvent({
        kind: "MovementRequested",
        reason: "manual_movement",
        targetId: command.targetId ?? undefined,
        commandSequenceId
      }));
      continue;
    }

    if (command.kind === "SelectTarget") {
      if (!input.targetAvailable || command.targetId !== input.activeEnemyId) {
        reject(events, "invalid_target", undefined, command.targetId, commandSequenceId);
        continue;
      }
      state.targetId = command.targetId;
      state.autoCombatStopped = false;
      events.push(buildEvent({ kind: "TargetSelected", targetId: command.targetId, commandSequenceId }));
      continue;
    }

    if (command.kind === "TriggerSkill") {
      const skill = findSkill(input.skills, command.skillId);
      if (!skill) {
        reject(events, "unknown_skill", command.skillId, undefined, commandSequenceId);
        continue;
      }
      if (!state.targetId) {
        reject(events, "no_valid_target", skill.id, undefined, commandSequenceId);
        continue;
      }
      if (!input.canFight) {
        events.push(buildEvent({
          kind: "MovementRequested",
          reason: "target_out_of_range",
          targetId: state.targetId,
          commandSequenceId
        }));
        continue;
      }
      if (state.playerMana < skill.manaCost) {
        reject(events, "insufficient_mana", skill.id, state.targetId, commandSequenceId);
        continue;
      }
      const readyAt = state.skillNextReadyAt[skill.id] ?? 0;
      if (input.timestamp < readyAt) {
        reject(events, "skill_on_cooldown", skill.id, state.targetId, commandSequenceId);
        continue;
      }

      const actionSequenceId = state.lastActionSequenceId + 1;
      state.lastActionSequenceId = actionSequenceId;

      if ((skill.timing ?? "Immediate") === "ScheduledImpact") {
        queueScheduledImpact(state, events, skill, input.timestamp, state.targetId, commandSequenceId, actionSequenceId);
        applyModifierFromSkill(state, events, skill, "player", commandSequenceId, actionSequenceId);
        applyStatusesFromSkill(state, events, skill, state.targetId, commandSequenceId, actionSequenceId);
        continue;
      }

      resolveSkillImpactDamage(
        state,
        events,
        skill,
        input.stats.baseAttack,
        state.simulationTick,
        input.timestamp,
        state.targetId,
        commandSequenceId,
        actionSequenceId
      );
      applyModifierFromSkill(state, events, skill, "player", commandSequenceId, actionSequenceId);
      applyStatusesFromSkill(state, events, skill, state.targetId, commandSequenceId, actionSequenceId);
    }
  }

  if (state.autoCombatStopped && input.timestamp >= state.autoCombatStoppedAt + input.autoResumeGraceMs) {
    state.autoCombatStopped = false;
    events.push(buildEvent({ kind: "AutoCombatResumed", targetId: state.targetId ?? undefined }));
  }

  const autoSelection = selectAutoSkill(input.skills, input.timestamp, input.canFight, state);
  state.lastAutoSkillId = autoSelection.skill?.id ?? null;
  state.lastAutoSkillFallbackReason = autoSelection.fallbackReason ?? null;

  if (autoSelection.skill && !state.autoCombatStopped && state.enemyHp > 0) {
    const autoSkill = autoSelection.skill;
    const actionSequenceId = state.lastActionSequenceId + 1;
    state.lastActionSequenceId = actionSequenceId;
    if ((autoSkill.timing ?? "Immediate") === "ScheduledImpact") {
      queueScheduledImpact(state, events, autoSkill, input.timestamp, state.targetId!, undefined, actionSequenceId);
      applyModifierFromSkill(state, events, autoSkill, "player", undefined, actionSequenceId);
      applyStatusesFromSkill(state, events, autoSkill, state.targetId!, undefined, actionSequenceId);
    } else {
      resolveSkillImpactDamage(
        state,
        events,
        autoSkill,
        input.stats.baseAttack,
        state.simulationTick + 13,
        input.timestamp,
        state.targetId!,
        undefined,
        actionSequenceId
      );
      applyModifierFromSkill(state, events, autoSkill, "player", undefined, actionSequenceId);
      applyStatusesFromSkill(state, events, autoSkill, state.targetId!, undefined, actionSequenceId);
    }
  }

  const canAutoAttack =
    !state.autoCombatStopped &&
    !!state.targetId &&
    input.canFight &&
    state.enemyHp > 0 &&
    input.timestamp >= state.playerNextAttackAt;

  if (canAutoAttack) {
    const attack = resolveDamage(input.stats.baseAttack, 1, state.simulationTick + 101);
    state.enemyHp = Math.max(0, state.enemyHp - attack.amount);
    state.playerNextAttackAt = input.timestamp + 1200;
    events.push(buildEvent({
      kind: "AttackResolved",
      amount: attack.amount,
      critical: attack.critical,
      targetId: state.targetId ?? undefined
    }));
  } else if (!input.canFight && state.targetId && state.enemyHp > 0) {
    requestMovement(events, "target_out_of_range", state.targetId);
  }

  if (state.enemyHp > 0 && input.canFight && state.targetId) {
    state.playerHp = Math.max(0, state.playerHp - input.enemy.attack);
  }

  if (state.enemyHp <= 0) {
    events.push(buildEvent({ kind: "EnemyDefeated", targetId: input.activeEnemyId }));
  }

  if (state.playerHp <= 0) {
    events.push(buildEvent({ kind: "PlayerDefeated", targetId: input.activeEnemyId }));
  }

  state.timestamp = input.timestamp;
  return {
    state,
    events,
    enemyKilled: state.enemyHp <= 0,
    playerDefeated: state.playerHp <= 0
  };
}
