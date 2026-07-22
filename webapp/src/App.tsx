import { useGameStore } from "./state/gameStore";

function formatDuration(ms: number): string {
  const totalMinutes = Math.floor(ms / 60000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${hours}h ${minutes}m`;
}

export function App() {
  const account = useGameStore((state) => state.account);
  const report = useGameStore((state) => state.offlineReport);
  const combatState = useGameStore((state) => state.combatState);
  const combatLog = useGameStore((state) => state.combatLog);
  const combatCanFight = useGameStore((state) => state.combatCanFight);
  const assignDemoActivity = useGameStore((state) => state.assignDemoActivity);
  const simulateOfflineWindow = useGameStore((state) => state.simulateOfflineWindow);
  const runCombatTick = useGameStore((state) => state.runCombatTick);
  const selectCombatTarget = useGameStore((state) => state.selectCombatTarget);
  const triggerCombatSkill = useGameStore((state) => state.triggerCombatSkill);
  const toggleCombatRange = useGameStore((state) => state.toggleCombatRange);
  const clearOfflineReport = useGameStore((state) => state.clearOfflineReport);

  const character = account.characters[0];

  return (
    <div className="page-shell">
      <div className="ambient ambient-a" />
      <div className="ambient ambient-b" />
      <main className="layout">
        <header className="hero card">
          <p className="kicker">IdleCloud migration track</p>
          <h1>Unity gameplay logic, web-native client shell</h1>
          <p>
            This starter keeps the existing design pillars intact: deterministic
            simulation, account-bank model, and snapshot offline progression.
          </p>
        </header>

        <section className="card split">
          <div>
            <h2>Account</h2>
            <p className="mono">{account.name}</p>
            <p>Bank coins: {account.bank.coins}</p>
            <p>Last seen: {new Date(account.lastSeenAt).toLocaleString()}</p>
          </div>
          <div>
            <h2>Character</h2>
            <p className="mono">{character.name} ({character.classId})</p>
            <p>Level {character.level} - XP {character.xp}</p>
            <p>
              Activity: {character.activity.kind}
              {character.activity.targetId ? ` (${character.activity.targetId})` : ""}
            </p>
            <p>
              Snapshot APH: {character.efficiency?.actionsPerHour.toFixed(1) ?? "none"}
            </p>
          </div>
        </section>

        <section className="card">
          <h2>Loop Controls</h2>
          <div className="button-row">
            <button onClick={() => assignDemoActivity(character.id, "Fighting")}>Assign Combat</button>
            <button onClick={() => assignDemoActivity(character.id, "Mining")}>Assign Mining</button>
            <button onClick={() => assignDemoActivity(character.id, "Idle")}>Go Idle</button>
            <button onClick={() => simulateOfflineWindow(4)}>Sim 4h Offline</button>
            <button onClick={() => simulateOfflineWindow(24)}>Sim 24h Offline</button>
          </div>
        </section>

        <section className="card">
          <h2>Active Combat Sandbox</h2>
          <p>
            Enemy HP {combatState.enemyHp} | Player HP {combatState.playerHp} | Mana {combatState.playerMana}
          </p>
          <p>
            Target: {combatState.targetId ?? "none"} | In Range: {combatCanFight ? "yes" : "no"}
          </p>
          <p>Scheduled effects: {combatState.scheduledEffects.length}</p>
          <p>Active modifiers: {combatState.activeModifiers.length} | Active statuses: {combatState.activeStatuses.length}</p>
          <p>
            Command seq: {combatState.lastCommandSequenceId} | Action seq: {combatState.lastActionSequenceId}
          </p>
          <p>
            Auto selected: {combatState.lastAutoSkillId ?? "none"} | Auto fallback: {combatState.lastAutoSkillFallbackReason ?? "none"}
          </p>
          <div className="button-row">
            <button onClick={selectCombatTarget}>Select Slime</button>
            <button onClick={() => triggerCombatSkill("power_strike")}>Power Strike</button>
            <button onClick={() => triggerCombatSkill("quick_slash")}>Quick Slash</button>
            <button onClick={() => triggerCombatSkill("delayed_bolt")}>Delayed Bolt</button>
            <button onClick={() => triggerCombatSkill("flame_burst")}>Flame Burst</button>
            <button onClick={runCombatTick}>Advance Tick</button>
            <button onClick={toggleCombatRange}>Toggle Range</button>
          </div>
          <div className="log-box">
            {combatLog.length === 0 && <p>No combat events yet.</p>}
            {combatLog.map((event, index) => (
              <p key={`${event.kind}-${index}`} className="mono">
                {event.kind}
                {event.skillId ? ` [${event.skillId}]` : ""}
                {event.amount ? ` amount=${event.amount}` : ""}
                {event.reason ? ` reason=${event.reason}` : ""}
              </p>
            ))}
          </div>
        </section>

        <section className="card">
          <h2>Offline Report</h2>
          {!report && <p>No report yet. Run an offline simulation window.</p>}
          {report && (
            <>
              <p>
                Elapsed: {formatDuration(report.elapsedMs)} | Capped: {formatDuration(report.cappedMs)}
              </p>
              {report.characters.map((entry) => (
                <article className="report-item" key={entry.characterId}>
                  <h3>{entry.characterName}</h3>
                  <p>
                    {entry.kind} {"→"} actions {entry.actions}, xp +{entry.xpGained}, levels +{entry.levelsGained}, coins +{entry.coinsGained}
                  </p>
                  <p>
                    Loot: {entry.loot.length === 0 ? "none" : entry.loot.map((loot) => `${loot.itemId} x${loot.qty}`).join(", ")}
                  </p>
                </article>
              ))}
              <button className="ghost" onClick={clearOfflineReport}>Clear Report</button>
            </>
          )}
        </section>
      </main>
    </div>
  );
}
