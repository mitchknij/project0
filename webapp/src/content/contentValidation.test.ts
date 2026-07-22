import { describe, expect, it } from "vitest";
import { assertValidContentBundle, validateContentBundle } from "./contentValidation";
import { sampleContent } from "./sampleContent";

describe("content validation", () => {
  it("accepts the sample content bundle", () => {
    expect(validateContentBundle(sampleContent)).toEqual([]);
    expect(assertValidContentBundle(sampleContent)).toBe(sampleContent);
  });

  it("rejects malformed weighted drop slots before simulation", () => {
    const invalid = structuredClone(sampleContent);
    invalid.monsters.slime_1.dropTable!.main!.slots[0] = { weight: 1, itemId: "gel", min: 2, max: 1 };

    expect(validateContentBundle(invalid)).toContain("monster_drop:slime_1:slot");
    expect(() => assertValidContentBundle(invalid)).toThrow("content_validation_failed");
  });
});
