import { describe, expect, it } from "vitest";
import { importContentBundle } from "./contentImport";
import { sampleContent } from "./sampleContent";

describe("content import", () => {
  it("accepts the generated snapshot envelope", () => {
    expect(importContentBundle({
      schemaVersion: 1,
      contentVersion: "test-v1",
      generatedAtUtc: "2026-07-22T00:00:00.000Z",
      bundle: sampleContent
    })).toEqual(sampleContent);
  });

  it("rejects incomplete external bundles", () => {
    expect(() => importContentBundle({ monsters: {} })).toThrow("content_import_missing_bundle");
  });
});
