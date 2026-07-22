import type { DropEntry, DropTable, ItemStack, WeightedSlot } from "../types";

const RARE_ACCESS_FACTOR = 0.2;
const NOTHING_FLOOR = 0.5;

export type RngDouble = () => number;

function average(min: number, max: number): number {
  return (min + max) / 2;
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

function validateRange(min: number, max: number): void {
  if (!Number.isFinite(min) || !Number.isFinite(max) || min < 0 || max < min) {
    throw new Error("invalid_range");
  }
}

function validateDropEntry(entry: DropEntry): void {
  if (!entry.itemId || !entry.itemId.trim()) throw new Error("invalid_drop_item");
  if (entry.chance < 0 || entry.chance > 1) throw new Error("invalid_drop_chance");
  validateRange(entry.min, entry.max);
}

function validateDropTable(table: DropTable): void {
  for (const always of table.always) {
    if (!always.itemId || !always.itemId.trim()) throw new Error("invalid_always_item");
    validateRange(always.min, always.max);
  }

  for (const tertiary of table.tertiary) {
    validateDropEntry(tertiary);
  }

  if (!table.main) return;
  if (table.main.rolls < 0) throw new Error("invalid_roll_count");

  for (const slot of table.main.slots) {
    if (slot.weight < 0) throw new Error("invalid_slot_weight");
    if (slot.nothing) continue;
    if (!slot.itemId || !slot.itemId.trim()) throw new Error("invalid_slot_item");
    validateRange(slot.min ?? 0, slot.max ?? -1);
  }
}

function uniformInt(min: number, max: number, rng: RngDouble): number {
  validateRange(min, max);
  const roll = rng();
  return min + Math.floor(roll * (max - min + 1));
}

function isItemSlot(slot: WeightedSlot): boolean {
  return !slot.nothing;
}

function effectiveTertiaryChance(entry: DropEntry, dropBonus: number): number {
  return Math.min(1, entry.chance * (1 + dropBonus));
}

function effectiveWeights(slots: WeightedSlot[], dropBonus: number): number[] {
  const nothingScale = Math.max(NOTHING_FLOOR, 1 - dropBonus * RARE_ACCESS_FACTOR);
  return slots.map((slot) => (isItemSlot(slot) ? slot.weight : slot.weight * nothingScale));
}

function pickWeightedSlot(slots: WeightedSlot[], rng: RngDouble, dropBonus: number): WeightedSlot | null {
  const weights = effectiveWeights(slots, dropBonus);
  const total = weights.reduce((sum, value) => sum + value, 0);
  if (total <= 0) return null;

  let cursor = rng() * total;
  for (let i = 0; i < slots.length; i += 1) {
    cursor -= weights[i];
    if (cursor < 0) {
      return isItemSlot(slots[i]) ? slots[i] : null;
    }
  }

  return null;
}

export function rollDrops(drops: DropEntry[], actions: number, rng: RngDouble, dropMult = 1): ItemStack[] {
  if (actions < 0) throw new Error("invalid_actions");
  if (dropMult < 0) throw new Error("invalid_drop_mult");

  const result: ItemStack[] = [];
  for (const entry of drops) {
    validateDropEntry(entry);
    const expected = actions * entry.chance * dropMult * average(entry.min, entry.max);
    const floor = Math.floor(expected);
    const qty = floor + (rng() < expected - floor ? 1 : 0);
    pushStack(result, entry.itemId, qty);
  }

  return result;
}

export function rollDropTable(table: DropTable, rng: RngDouble, dropBonus = 0): ItemStack[] {
  validateDropTable(table);
  const result: ItemStack[] = [];

  for (const always of table.always) {
    pushStack(result, always.itemId, uniformInt(always.min, always.max, rng));
  }

  if (table.main) {
    for (let roll = 0; roll < table.main.rolls; roll += 1) {
      const picked = pickWeightedSlot(table.main.slots, rng, dropBonus);
      if (picked && picked.itemId) {
        pushStack(result, picked.itemId, uniformInt(picked.min ?? 1, picked.max ?? 1, rng));
      }
    }
  }

  for (const entry of table.tertiary) {
    if (rng() < effectiveTertiaryChance(entry, dropBonus)) {
      pushStack(result, entry.itemId, uniformInt(entry.min, entry.max, rng));
    }
  }

  return result;
}

export function expectedDropTable(table: DropTable, kills: number, rng: RngDouble, dropBonus = 0): ItemStack[] {
  if (kills < 0) throw new Error("invalid_kills");
  validateDropTable(table);
  const expectedByItem = new Map<string, number>();

  function add(itemId: string, qty: number): void {
    expectedByItem.set(itemId, (expectedByItem.get(itemId) ?? 0) + qty);
  }

  for (const always of table.always) {
    add(always.itemId, kills * average(always.min, always.max));
  }

  if (table.main) {
    const weights = effectiveWeights(table.main.slots, dropBonus);
    const total = weights.reduce((sum, value) => sum + value, 0);
    if (total > 0) {
      for (let i = 0; i < table.main.slots.length; i += 1) {
        const slot = table.main.slots[i];
        if (!isItemSlot(slot) || !slot.itemId) continue;
        const p = weights[i] / total;
        add(slot.itemId, kills * table.main.rolls * p * average(slot.min ?? 1, slot.max ?? 1));
      }
    }
  }

  for (const entry of table.tertiary) {
    add(entry.itemId, kills * effectiveTertiaryChance(entry, dropBonus) * average(entry.min, entry.max));
  }

  const result: ItemStack[] = [];
  for (const [itemId, expected] of expectedByItem.entries()) {
    const floor = Math.floor(expected);
    const qty = floor + (rng() < expected - floor ? 1 : 0);
    pushStack(result, itemId, qty);
  }

  return result;
}

export function rollCoins(min: number, max: number, rng: RngDouble): number {
  return uniformInt(min, max, rng);
}
