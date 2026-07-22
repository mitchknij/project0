# Architecture Documentation Rules

[ARCHI.md](ARCHI.md) documents the IdleCloud architecture. After each task (new feature, refactor, bug fix), determine if ARCHI.md needs updating.

**Division of labor (keep it):** `docs/guardrails/PROJECT.md` owns the normative rules — layering borders, style rules, and the three gameplay-flow contracts. ARCHI.md is the map of what exists and **defers to PROJECT.md**; never restate PROJECT.md content in ARCHI.md, and never edit PROJECT.md as part of an ARCHI update (it is a guardrails-kit file — verbatim discipline applies). Session-to-session working state belongs in `docs/STATE.md`, not here.

## When to Update

Update after ANY change that alters:

- Project structure (new directories, moved files, new/changed `.asmdef` — also update the §5 mermaid dependency graph)
- Technology stack (§3 — new packages in `Packages/manifest.json`, Unity version bumps)
- The subsystem sections: §8 Game Loop orchestration, §9 Isometric Sorting & Elevation, §10 Pathfinding & World Interaction, §11 Scene Architecture, §12 Offline Simulation, §13 UI Architecture, §15 Save System, §16 Editor Tooling
- Build/test workflow (§6, §14 — e.g. a working batch-mode pipeline, coverage tooling, a real build pipeline in §19)
- Configuration approach (§7 — new shared settings assets like `IsoSortSettings`)

## How to Update by Change Type

### Major Feature / Refactor

Review: §4 Project Structure, §5 Principles + dependency graph, and every subsystem section the change touches (§8–§16).

### Minor Feature / Enhancement

Update: only the one subsystem section involved (e.g. a new panel → §13; a new sort controller → §9; a new editor generator → §16).

### Bug Fix

Usually no update needed, unless it reveals/fixes an architectural flaw (e.g. an execution-order or asmdef-boundary issue worth recording as a gotcha).

### Dependency Changes

Update: §3 Technology Stack, and any affected subsystem sections.

## Guidelines

- Be precise and factual — reflect the actual codebase (verify class/file names before writing them)
- Be concise — enough detail to understand, not implementation specifics; the code is the reference
- Update the mermaid diagram when assembly dependencies change
- Reference actual file paths (`Assets/Scripts/...`, `Assets/Iso/Sorting/...`)
- After updating, check size with `bash .claude/skills/TRIP-compact/count-tokens.sh docs/ARCHI.md` (warn above ~20k tokens per TRIP-3-release Step 7)
