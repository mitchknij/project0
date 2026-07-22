# IdleCloud Web Starter

This folder is the first migration step from Unity to browser.

## Goals

- Keep the same gameplay pillars: active loop, gathering loop, and deterministic offline gains.
- Preserve the downward architecture: content/data -> domain simulation -> app state -> UI.
- Enable rapid iteration of Core logic without Unity scene dependencies.

## Run

1. Install dependencies:
   npm install
2. Start dev server:
   npm run dev
3. Build production bundle:
   npm run build

## What is implemented

- TypeScript domain contracts inspired by `Assets/Scripts/Data/State/GameTypes.cs`.
- Pure offline simulation inspired by `Assets/Scripts/Core/Offline.cs` and `Assets/Scripts/Core/Activity.cs`.
- Active combat tick simulation inspired by `Assets/Scripts/Core/Combat/ActiveSim.cs` command/event style.
- Active combat now includes scheduled impact support and auto-skill fallback selection.
- Active combat now includes transient modifier and status tick lifecycles with scheduled runtime processing.
- Active combat events now carry command/action sequence metadata, and state tracks auto-selection diagnostics.
- Progression helpers aligned with `Assets/Scripts/Core/Progression.cs` xp curve and carry behavior.
- Drop-system helpers aligned with `Assets/Scripts/Core/DropSystem.cs` (weighted table, tertiary, and expected-value offline rolls).
- Zustand store for account/session-like state inspired by `Assets/Scripts/Managers/GameManager.cs`.
- React UI shell for activity assignment, offline simulation, and combat sandbox controls.
- Unit tests for active combat simulation via Vitest.
- Versioned local save persistence and migration scaffold inspired by `Assets/Scripts/Managers/SaveManager.cs`.
- Deterministic combat replay helper plus Unity PascalCase legacy-save migration, including character state.
- Unity-compatible tile-pattern resolver for deterministic area target selection.
- Content import validation and a Unity Editor JSON snapshot exporter.

## Final handoff

See [DEPLOYMENT.md](DEPLOYMENT.md) for GitHub Pages publication, the local release gate, and Unity content snapshot publication.

Every browser-source change runs the **Verify IdleCloud Web** GitHub Actions workflow. It must pass its Vitest suite and production Vite build before a release is eligible for publication. The Pages workflow is manual-only, so a validated commit is not published until an explicit deployment run is requested.

The simulation, save compatibility, content-boundary, deterministic replay, and static-hosting delivery path can proceed independently of Unity scenes.
