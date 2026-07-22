import { useState } from "react";
import { useGameStore } from "./state/gameStore";

type GamePanel = "adventure" | "inventory" | "journal";

function formatDuration(ms: number): string {
  const totalMinutes = Math.floor(ms / 60_000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${hours}h ${minutes}m`;
}

function healthPercent(value: number, maximum: number): number {
  return Math.max(0, Math.min(100, Math.round((value / maximum) * 100)));
}

export function App() {
  const [panel, setPanel] = useState<GamePanel>("adventure");
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
  const playerHealth = healthPercent(combatState.playerHp, 110);
  const enemyHealth = healthPercent(combatState.enemyHp, 85);
  const isTargeted = combatState.targetId === "slime_1";

  return (
    <main className="rpg-shell">
      <header className="rpg-header">
        <div className="logo"><span>☁</span><b>Idle<em>Cloud</em></b><small>ADVENTURES</small></div>
        <nav><button className={panel === "adventure" ? "selected" : ""} onClick={() => setPanel("adventure")}>Adventure</button><button className={panel === "inventory" ? "selected" : ""} onClick={() => setPanel("inventory")}>Inventory</button><button className={panel === "journal" ? "selected" : ""} onClick={() => setPanel("journal")}>Journal</button></nav>
        <div className="coins">◈ {account.bank.coins.toLocaleString()}</div>
      </header>

      <section className="character-hud">
        <div className="portrait">⚔</div><div><small>LEVEL {character.level} {character.classId.toUpperCase()}</small><h1>{character.name}</h1><div className="xp"><i style={{ width: `${Math.min(100, Math.round(character.xp / 4.11))}%` }} /></div><p><b />{character.activity.kind === "Fighting" ? "Hunting Green Slimes" : character.activity.kind === "Mining" ? "Mining Copper Vein" : "Resting at camp"}</p></div>
        <div className="hud-stats"><span>POWER<b>12</b></span><span>COMBAT<b>{character.skills.Combat.level}</b></span><span>OFFLINE<b>{character.efficiency ? `${character.efficiency.actionsPerHour.toFixed(0)}/h` : "Ready"}</b></span></div>
      </section>

      {panel === "adventure" && <section className="adventure-layout">
        <aside className="side-card quest-card"><h2>✦ Quest Board</h2><article><span>☠</span><div><small>ACTIVE QUEST</small><h3>The Green Menace</h3><p>Clear slimes from Verdant Way.</p><div className="mini-progress"><i style={{ width: enemyHealth === 0 ? "100%" : "55%" }} /></div><em>{enemyHealth === 0 ? "1 / 1" : "0 / 1"} slime defeated</em></div></article><h3 className="side-label">IDLE ACTIVITY</h3><button className={character.activity.kind === "Fighting" ? "activity active" : "activity"} onClick={() => assignDemoActivity(character.id, "Fighting")}>⚔ <span>Hunt slimes<small>Combat XP & loot</small></span></button><button className={character.activity.kind === "Mining" ? "activity active" : "activity"} onClick={() => assignDemoActivity(character.id, "Mining")}>⛏ <span>Mine copper<small>Mining XP & ore</small></span></button><button className={character.activity.kind === "Idle" ? "activity active" : "activity"} onClick={() => assignDemoActivity(character.id, "Idle")}>⌂ <span>Rest at camp<small>Pause activity</small></span></button><div className="idle-buttons"><button onClick={() => simulateOfflineWindow(4)}>Claim 4h</button><button onClick={() => simulateOfflineWindow(24)}>Claim 24h</button></div></aside>
        <section className="world-card"><div className="zone-title"><span>VERDANT WAY · Meadow Path</span><b>{combatCanFight ? "IN RANGE" : "OUT OF RANGE"}</b></div><div className="world"><i className="cloud one" /><i className="cloud two" /><i className="mountains" /><i className="trees" /><i className="grass" /><div className="hero-unit"><label>{character.name}<small>Lv. {character.level}</small></label><div className="hp"><i style={{ width: `${playerHealth}%` }} /></div><span className="hero-sprite">⚔</span></div><div className={`slime-unit ${isTargeted ? "targeted" : ""}`}><label>Green Slime<small>Lv. 2</small></label><div className="hp enemy-hp"><i style={{ width: `${enemyHealth}%` }} /></div><span className="slime-sprite"><i /><i /><b /></span></div>{!combatCanFight && <p className="scene-callout">Move closer to engage</p>}{combatState.scheduledEffects.length > 0 && <p className="scene-callout cast">Casting spell...</p>}</div><div className="world-actions"><button onClick={selectCombatTarget}>{isTargeted ? "Target Locked" : "Target Slime"}</button><button onClick={toggleCombatRange}>{combatCanFight ? "Step Back" : "Move In"}</button><button className="battle-button" onClick={runCombatTick}>Advance Battle <kbd>SPACE</kbd></button></div><div className="skills"><button onClick={() => triggerCombatSkill("power_strike")}><b>1</b><span>✦</span><small>Power Strike</small></button><button onClick={() => triggerCombatSkill("quick_slash")}><b>2</b><span>〰</span><small>Quick Slash</small></button><button onClick={() => triggerCombatSkill("delayed_bolt")}><b>3</b><span>ϟ</span><small>Delayed Bolt</small></button><button onClick={() => triggerCombatSkill("flame_burst")}><b>4</b><span>♨</span><small>Flame Burst</small></button></div></section>
        <aside className="side-card feed-card"><h2>⌁ Battle Feed</h2><div className="combat-status"><span>ENCOUNTER<b>{isTargeted ? "Engaged" : "Scouting"}</b></span><span>AUTO SKILL<b>{combatState.lastAutoSkillId?.replaceAll("_", " ") ?? "Waiting"}</b></span></div><div className="feed">{combatLog.length === 0 ? <p>The meadow is quiet. Select a target to begin.</p> : combatLog.slice(-6).reverse().map((event, index) => <p key={`${event.kind}-${index}`}><i>{event.kind === "SkillResolved" ? "✦" : "•"}</i><b>{event.skillId?.replaceAll("_", " ") ?? event.kind.replaceAll(/([A-Z])/g, " $1")}</b>{event.amount ? ` ${event.amount}` : ""}</p>)}</div><footer>Effects <b>{combatState.scheduledEffects.length}</b><span>Modifiers <b>{combatState.activeModifiers.length}</b></span><span>Status <b>{combatState.activeStatuses.length}</b></span></footer></aside>
      </section>}

      {panel === "inventory" && <section className="large-card"><h2>▣ Pack &amp; Bank</h2><p>{character.inventory.length} / {character.maxInventorySlots} pack slots used</p><div className="items">{character.inventory.length === 0 ? <p>Your pack is empty. Hunt slimes or mine copper to collect resources.</p> : character.inventory.map((item) => <article key={item.itemId}>◆<b>{item.itemId.replaceAll("_", " ")}</b><small>× {item.qty}</small></article>)}</div><div className="bank">◈ <span><small>CLOUD COINS</small><b>{account.bank.coins.toLocaleString()}</b></span><em>Shared across all characters</em></div></section>}
      {panel === "journal" && <section className="large-card"><h2>☷ Expedition Journal</h2>{!report ? <div className="empty-journal">☁<h3>No expedition report yet</h3><p>Choose an idle session from Adventure to generate a report.</p></div> : <><div className="report-head">Elapsed {formatDuration(report.elapsedMs)} · Simulation cap {formatDuration(report.cappedMs)}<button onClick={clearOfflineReport}>Clear report</button></div>{report.characters.map((entry) => <article className="report" key={entry.characterId}>✦<div><h3>{entry.characterName}</h3><p>{entry.kind} · {entry.actions.toLocaleString()} actions · +{entry.xpGained.toLocaleString()} XP · +{entry.coinsGained.toLocaleString()} coins</p><small>Loot: {entry.loot.length ? entry.loot.map((loot) => `${loot.itemId.replaceAll("_", " ")} ×${loot.qty}`).join(", ") : "none"}</small></div></article>)}</>}</section>}
      <footer className="rpg-footer"><span>IdleCloud Web</span><span>Deterministic simulation online</span><span>Last save {new Date(account.lastSeenAt).toLocaleTimeString()}</span></footer>
    </main>
  );
}
