import type { Account, Character, ItemStack, OfflineReport, SkillProgress } from "../domain/types";

export const CURRENT_SAVE_SCHEMA_VERSION = 1;

export interface PersistedGameStateV1 {
  saveSchemaVersion: 1;
  account: Account;
  offlineReport: OfflineReport | null;
}

export interface LegacyPersistedGameStateV0 {
  account: Account;
  offlineReport?: OfflineReport | null;
}

interface UnityLegacyAccount {
  Id?: string;
  Name?: string;
  Characters?: UnityLegacyCharacter[];
  Bank?: {
    Coins?: number;
    Slots?: Array<{ ItemId?: string; Qty?: number }>;
    MaxSlots?: number;
  };
  LastSeenAt?: number;
  AutoLoot?: boolean;
  AutoCombatDisabled?: boolean;
}

interface UnityLegacyCharacter {
  Id?: string;
  Name?: string;
  ClassId?: string | number;
  Level?: number;
  Xp?: number;
  MapId?: string;
  Inventory?: Array<{ ItemId?: string; Qty?: number }>;
  MaxInventorySlots?: number;
  Activity?: { Kind?: string | number; TargetId?: string | null; StartedAt?: number };
  Skills?: Record<string, { Level?: number; Xp?: number }>;
  FreeStatPoints?: number;
}

function normalizeAccount(account: Account): Account {
  return {
    ...account,
    characters: (account.characters ?? []).map((character) => ({
      ...character,
      freeStatPoints: character.freeStatPoints ?? 0,
      skills: {
        Combat: character.skills?.Combat ?? { level: 1, xp: 0 },
        Mining: character.skills?.Mining ?? { level: 1, xp: 0 },
        Chopping: character.skills?.Chopping ?? { level: 1, xp: 0 },
        Gathering: character.skills?.Gathering ?? { level: 1, xp: 0 }
      },
      activity: character.activity ?? { kind: "Idle", targetId: null, startedAt: Date.now() },
      inventory: character.inventory ?? []
    })),
    bank: {
      coins: account.bank?.coins ?? 0,
      slots: account.bank?.slots ?? [],
      maxSlots: account.bank?.maxSlots ?? 48
    }
  };
}

function isUnityLegacyAccount(value: unknown): value is UnityLegacyAccount {
  if (!value || typeof value !== "object") return false;
  const candidate = value as UnityLegacyAccount;
  return typeof candidate.Id === "string" && typeof candidate.Name === "string";
}

function translateUnityStack(source: { ItemId?: string; Qty?: number }): ItemStack | null {
  if (!source || typeof source.ItemId !== "string" || !Number.isFinite(source.Qty) || source.Qty! <= 0) return null;
  return { itemId: source.ItemId, qty: Math.floor(source.Qty!) };
}

function translateUnityClassId(value: UnityLegacyCharacter["ClassId"]): Character["classId"] {
  if (typeof value === "string" && ["Beginner", "Warrior", "Archer", "Mage"].includes(value)) {
    return value as Character["classId"];
  }
  return (["Beginner", "Warrior", "Archer", "Mage"][typeof value === "number" ? value : 0] ?? "Beginner") as Character["classId"];
}

function translateUnityActivityKind(value: string | number | undefined): Character["activity"]["kind"] {
  if (typeof value === "string" && ["Idle", "Fighting", "Mining", "Chopping", "Gathering"].includes(value)) {
    return value as Character["activity"]["kind"];
  }
  return (["Idle", "Fighting", "Mining", "Chopping", "Gathering"][typeof value === "number" ? value : 0] ?? "Idle") as Character["activity"]["kind"];
}

function translateUnitySkill(source: { Level?: number; Xp?: number } | undefined): SkillProgress {
  return {
    level: Math.max(1, Math.floor(source?.Level ?? 1)),
    xp: Math.max(0, Math.floor(source?.Xp ?? 0))
  };
}

function translateUnityCharacter(source: UnityLegacyCharacter, lastSeenAt: number): Character | null {
  if (!source || typeof source.Id !== "string" || typeof source.Name !== "string") return null;
  const activity = source.Activity;
  const inventory = (source.Inventory ?? []).map(translateUnityStack).filter((stack): stack is ItemStack => stack !== null);
  return {
    id: source.Id,
    name: source.Name,
    classId: translateUnityClassId(source.ClassId),
    level: Math.max(1, Math.floor(source.Level ?? 1)),
    xp: Math.max(0, Math.floor(source.Xp ?? 0)),
    mapId: source.MapId ?? "",
    inventory,
    maxInventorySlots: Math.max(0, Math.floor(source.MaxInventorySlots ?? 24)),
    activity: {
      kind: translateUnityActivityKind(activity?.Kind),
      targetId: activity?.TargetId ?? null,
      startedAt: activity?.StartedAt ?? lastSeenAt
    },
    efficiency: null,
    freeStatPoints: Math.max(0, Math.floor(source.FreeStatPoints ?? 0)),
    skills: {
      Combat: translateUnitySkill(source.Skills?.Combat),
      Mining: translateUnitySkill(source.Skills?.Mining),
      Chopping: translateUnitySkill(source.Skills?.Chopping),
      Gathering: translateUnitySkill(source.Skills?.Gathering)
    }
  };
}

function translateUnityLegacyAccount(source: UnityLegacyAccount): Account {
  const lastSeenAt = source.LastSeenAt ?? 0;
  return normalizeAccount({
    id: source.Id ?? "legacy-account",
    name: source.Name ?? "Legacy Account",
    lastSeenAt,
    autoLoot: source.AutoLoot ?? false,
    autoCombatDisabled: source.AutoCombatDisabled ?? false,
    characters: (source.Characters ?? [])
      .map((character) => translateUnityCharacter(character, lastSeenAt))
      .filter((character): character is Character => character !== null),
    bank: {
      coins: source.Bank?.Coins ?? 0,
      slots: (source.Bank?.Slots ?? [])
        .map(translateUnityStack)
        .filter((stack): stack is ItemStack => stack !== null),
      maxSlots: source.Bank?.MaxSlots ?? 48
    }
  });
}

export function migratePersistedState(raw: unknown): PersistedGameStateV1 | null {
  if (!raw || typeof raw !== "object") return null;

  const candidate = raw as { state?: unknown };
  const unwrapped = (candidate.state && typeof candidate.state === "object") ? candidate.state : raw;
  if (!unwrapped || typeof unwrapped !== "object") return null;

  const data = unwrapped as Partial<PersistedGameStateV1 & LegacyPersistedGameStateV0>;
  if (isUnityLegacyAccount(unwrapped)) {
    return {
      saveSchemaVersion: 1,
      account: translateUnityLegacyAccount(unwrapped),
      offlineReport: null
    };
  }
  if (!data.account) return null;

  const account = normalizeAccount(data.account);
  if (data.saveSchemaVersion === 1) {
    return {
      saveSchemaVersion: 1,
      account,
      offlineReport: data.offlineReport ?? null
    };
  }

  return {
    saveSchemaVersion: 1,
    account,
    offlineReport: data.offlineReport ?? null
  };
}
