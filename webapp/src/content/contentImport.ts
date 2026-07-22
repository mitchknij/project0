import type { ContentBundle } from "../domain/types";
import { assertValidContentBundle } from "./contentValidation";

export interface ExportedContentSnapshot {
  schemaVersion: 1;
  contentVersion: string;
  generatedAtUtc: string;
  bundle: ContentBundle;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

/**
 * Parses a generated Unity content snapshot or a raw web content bundle. Both
 * routes validate before returning any data to deterministic simulations.
 */
export function importContentBundle(raw: unknown): ContentBundle {
  if (!isRecord(raw)) throw new Error("content_import_invalid_root");
  const bundle = isRecord(raw.bundle) ? raw.bundle : raw;
  if (!isRecord(bundle.monsters) || !isRecord(bundle.nodes)) {
    throw new Error("content_import_missing_bundle");
  }
  return assertValidContentBundle(bundle as unknown as ContentBundle);
}
