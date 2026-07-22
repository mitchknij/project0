import type {
  Account,
  ContentBundle,
  ItemStack,
  OfflineBalanceConfig,
  OfflineCharacterReport,
  OfflineReport
} from "../types";
import { expectedDropTable, rollDrops } from "./dropSystem";
import { applyCharacterXp } from "./progression";

function hashSeed(parts: Array<string | number>): number {
  const raw = parts.join("|");
  let hash = 2166136261;
  for (let i = 0; i < raw.length; i += 1) {
    hash ^= raw.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

function createRng(seed: number): () => number {
  let state = seed || 1;
  return () => {
    state = (Math.imul(1664525, state) + 1013904223) >>> 0;
    return state / 4294967296;
  };
}

function pushStack(target: ItemStack[], itemId: string, qty: number): void {
  if (qty <= 0) return;
  const existing = target.find((entry) => entry.itemId === itemId);
  if (existing) {
    existing.qty += qty;
    return;
  }
  target.push({ itemId, qty });
}

export function simulateOffline(
  account: Account,
  nowMs: number,
  content: ContentBundle,
  balance: OfflineBalanceConfig
): { account: Account; report: OfflineReport | null } {
  if (nowMs < account.lastSeenAt) {
    throw new Error("Time moved backwards");
  }

  const elapsedMs = nowMs - account.lastSeenAt;
  if (elapsedMs < balance.minimumDurationMs) {
    return {
      account: { ...account, lastSeenAt: nowMs },
      report: null
    };
  }

  const cappedMs = Math.min(elapsedMs, balance.capMs);
  const hours = cappedMs / 3_600_000;
  const rng = createRng(hashSeed([account.id, account.lastSeenAt, nowMs]));

  const reports: OfflineCharacterReport[] = [];
  const nextCharacters = account.characters.map((character) => {
    const efficiency = character.efficiency;
    const baseReport: OfflineCharacterReport = {
      characterId: character.id,
      characterName: character.name,
      kind: character.activity.kind,
      targetId: character.activity.targetId,
      actions: 0,
      xpGained: 0,
      levelsGained: 0,
      coinsGained: 0,
      loot: []
    };

    if (!efficiency || efficiency.kind === "Idle" || efficiency.actionsPerHour <= 0) {
      reports.push(baseReport);
      return {
        ...character,
        activity: { ...character.activity, startedAt: nowMs }
      };
    }

    const actions = Math.floor(efficiency.actionsPerHour * balance.rate * hours);
    if (actions <= 0) {
      reports.push(baseReport);
      return {
        ...character,
        activity: { ...character.activity, startedAt: nowMs }
      };
    }

    const xpGained = Math.floor(actions * efficiency.xpPerAction);
    const coinsGained = Math.floor(actions * efficiency.coinsPerAction);
    let loot: ItemStack[] = [];

    if (efficiency.kind === "Fighting" && efficiency.targetId) {
      const monster = content.monsters[efficiency.targetId];
      if (monster?.dropTable) {
        loot = expectedDropTable(monster.dropTable, actions, rng, 0);
      } else {
        loot = rollDrops(monster?.drops ?? [], actions, rng, 1);
      }
    } else if (efficiency.targetId) {
      const node = content.nodes[efficiency.targetId];
      loot = rollDrops(node?.drops ?? [], actions, rng, 1);
    }

    const progression = applyCharacterXp(character, xpGained);
    const nextCharacter = {
      ...progression.character,
      inventory: [...character.inventory],
      activity: { ...character.activity, startedAt: nowMs }
    };

    for (const stack of loot) {
      pushStack(nextCharacter.inventory, stack.itemId, stack.qty);
    }

    reports.push({
      ...baseReport,
      actions,
      xpGained,
      levelsGained: progression.levelsGained,
      coinsGained,
      loot
    });

    return nextCharacter;
  });

  const totalCoins = reports.reduce((sum, entry) => sum + entry.coinsGained, 0);
  const didAnything = reports.some((entry) => entry.actions > 0);

  const nextAccount: Account = {
    ...account,
    lastSeenAt: nowMs,
    characters: nextCharacters,
    bank: {
      ...account.bank,
      coins: account.bank.coins + totalCoins
    }
  };

  if (!didAnything) {
    return { account: nextAccount, report: null };
  }

  return {
    account: nextAccount,
    report: {
      elapsedMs,
      cappedMs,
      characters: reports
    }
  };
}
