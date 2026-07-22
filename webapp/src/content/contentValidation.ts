import type { ContentBundle, DropEntry, DropTable, WeightedSlot } from "../domain/types";

function validDrop(drop: DropEntry): boolean {
  return Boolean(
    drop &&
    drop.itemId &&
    Number.isFinite(drop.chance) &&
    drop.chance >= 0 &&
    drop.chance <= 1 &&
    Number.isInteger(drop.min) &&
    Number.isInteger(drop.max) &&
    drop.min >= 0 &&
    drop.max >= drop.min
  );
}

function validateWeightedSlot(slot: WeightedSlot): boolean {
  return Boolean(
    slot &&
    Number.isFinite(slot.weight) &&
    slot.weight >= 0 &&
    (slot.nothing
      ? !slot.itemId
      : Boolean(slot.itemId) && Number.isInteger(slot.min) && Number.isInteger(slot.max) &&
        (slot.min ?? -1) >= 0 && (slot.max ?? -1) >= (slot.min ?? 0))
  );
}

function validateDropTable(prefix: string, table: DropTable, issues: string[]): void {
  for (const drop of table.always) {
    if (!drop.itemId || !Number.isInteger(drop.min) || !Number.isInteger(drop.max) || drop.min < 0 || drop.max < drop.min) {
      issues.push(`${prefix}:always`);
    }
  }
  for (const drop of table.tertiary) if (!validDrop(drop)) issues.push(`${prefix}:tertiary`);
  if (!table.main) return;
  if (!Number.isInteger(table.main.rolls) || table.main.rolls < 0) issues.push(`${prefix}:rolls`);
  const totalWeight = table.main.slots.reduce((total, slot) => total + (slot?.weight ?? 0), 0);
  for (const slot of table.main.slots) if (!validateWeightedSlot(slot)) issues.push(`${prefix}:slot`);
  if (table.main.rolls > 0 && totalWeight <= 0) issues.push(`${prefix}:total_weight`);
}

/** Validates web content at the import boundary before simulations consume it. */
export function validateContentBundle(content: ContentBundle): string[] {
  const issues: string[] = [];
  for (const [id, monster] of Object.entries(content.monsters ?? {})) {
    if (!monster || monster.id !== id || !monster.name || monster.xp < 0 || monster.coinsMin < 0 || monster.coinsMax < monster.coinsMin) {
      issues.push(`monster_invalid:${id}`);
      continue;
    }
    for (const drop of monster.drops ?? []) if (!validDrop(drop)) issues.push(`monster_drop_invalid:${id}`);
    if (monster.dropTable) validateDropTable(`monster_drop:${id}`, monster.dropTable, issues);
  }
  for (const [id, node] of Object.entries(content.nodes ?? {})) {
    if (!node || node.id !== id || !node.name || node.xp < 0) {
      issues.push(`node_invalid:${id}`);
      continue;
    }
    for (const drop of node.drops ?? []) if (!validDrop(drop)) issues.push(`node_drop_invalid:${id}`);
  }
  return issues;
}

/** Rejects malformed external content instead of letting it enter deterministic simulation. */
export function assertValidContentBundle(content: ContentBundle): ContentBundle {
  const issues = validateContentBundle(content);
  if (issues.length > 0) throw new Error(`content_validation_failed:${issues.join(";")}`);
  return content;
}
