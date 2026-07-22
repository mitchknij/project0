# Testing Guidelines

## Test Framework

Unity Test Framework 1.7.0 (NUnit) on Unity 6000.5.1f1.

## Running Tests

Tests execute **only** via the Unity Editor Test Runner:

```
Window → General → Test Runner → EditMode (or PlayMode) → filter to the affected class → Run
```

The user runs this in the live Editor and reports results — the open Editor holds the project lock, so CLI batch mode is normally unavailable.

```bash
# Compile gate — what the CLI CAN verify (0 errors required;
# only the pre-existing CS0618 warning family is acceptable):
dotnet build IdleCloud.slnx

# DOES NOT WORK — restores only, executes 0 Unity tests:
# dotnet test

# Batch-mode fallback, ONLY if the Unity Editor is fully closed:
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit
```

## Test Organization

| Location | Assembly | References | Role |
|---|---|---|---|
| `Assets/Scripts/Tests/EditMode/` | `IdleCloud.Tests.EditMode` | Core, Data, Managers (**no UI/View**) | Main suite — fast, engine-free logic tests (10 files) |
| `Assets/Scripts/Tests/PlayMode/` | `IdleCloud.Tests.PlayMode` | Core, Data, Managers, View (**no UI**) | Gameplay-loop smoke tests only |

Naming: one file per area, `<Area>Tests.cs` (e.g. `ActiveSimTests.cs`, `SaveCompatibilityTests.cs`); NUnit `[Test]` methods.

## Writing Tests

- Default every new test to **EditMode** — `IdleCloud.Core`/`.Data` compile with `noEngineReferences: true`, so domain logic tests need no engine.
- Determinism first: drive randomness through seeded `IRandomSource`/`OfflineSeed` and assert **exact** expected values (see `ProgressionAndOfflineTests`, `DropAndActivityTests`).
- Test observable behavior (inputs → outputs/persisted state), never internal wiring.
- **Never add UI (or TMPro) references to a test asmdef** — attempted and reverted (recorded in `docs/STATE.md` Failed attempts). Resolve already-loaded components reflectively if a PlayMode test must touch them.
- Save-format changes always get a `SaveCompatibilityTests` case (round-trip + migration).
- Hard-to-cover paths: follow the seam ladder in `.claude/skills/TRIP-test/SKILL.md`; log deferred gaps in `docs/4-unit-tests/COVERAGE-DEBT.md` (`path | why hard | escape plan`).

## Coverage Requirements

Not defined — no coverage tooling installed (Unity Code Coverage package absent). The floor is behavioral: safety-critical behavior (persistence, deletion, reward/cost math) must keep at least one behavioral test.
