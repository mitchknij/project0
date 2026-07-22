import type { ActiveCombatInput, CombatCommand, CombatEvent } from "../types";
import { tickActiveCombat } from "./activeCombat";

export interface CombatReplayStep {
  timestamp: number;
  commands: CombatCommand[];
}

export interface CombatReplayResult {
  events: CombatEvent[];
  eventSignature: string[];
  finalState: ReturnType<typeof tickActiveCombat>["state"];
}

/**
 * Runs a fixed command/timestamp script against the pure combat reducer.
 * The result is intentionally serializable so parity fixtures can compare
 * the web event stream against Unity Core output for the same inputs.
 */
export function replayCombat(
  createInput: (state: ActiveCombatInput["state"], step: CombatReplayStep) => ActiveCombatInput,
  initialState: ActiveCombatInput["state"],
  steps: CombatReplayStep[]
): CombatReplayResult {
  let state = initialState;
  const events: CombatEvent[] = [];

  for (const step of steps) {
    const result = tickActiveCombat(createInput(state, step));
    state = result.state;
    events.push(...result.events);
  }

  return {
    events,
    eventSignature: events.map((event) => {
      const amount = event.amount === undefined ? "" : `:${event.amount}`;
      const skill = event.skillId ? `:${event.skillId}` : "";
      const reason = event.reason ? `:${event.reason}` : "";
      return `${event.kind}${skill}${amount}${reason}`;
    }),
    finalState: state
  };
}
