# Offline Progression Policy

This document specifies the behavior currently implemented by the headless offline calculator. It is descriptive, not a source of new tuning or reward eligibility. Claim presentation and acknowledgement are separate from this calculation policy.

## Production tuning and elapsed time

The production `OfflineProgressionConfig` uses an offline rate of `0.4` (40% of the captured active rate), a 24-hour processing cap, and a one-minute minimum elapsed duration (`Assets/Resources/OfflineProgressionConfig.asset:8-11`). The asset converts hours and minutes to milliseconds before Core receives it (`Assets/Scripts/Managers/Content/OfflineProgressionConfigAsset.cs:23-30`); the same values also exist as the pure-data fallback (`Assets/Scripts/Data/OfflineBalanceRepo.cs:8-13`).

For an account with persisted `LastSeenAt` and a supplied `now`:

1. `elapsedMs = now - LastSeenAt` (`Assets/Scripts/Core/Offline.cs:42`). If `now < LastSeenAt`, the calculation throws `ArgumentOutOfRangeException`; negative elapsed time is never clamped or rewarded (`Assets/Scripts/Core/Offline.cs:29-35`).
2. If `elapsedMs < 60,000`, no rewards or report are produced, but the returned account advances `LastSeenAt` to `now` (`Assets/Scripts/Core/Offline.cs:43-48`). Exactly 60,000 ms is eligible.
3. Otherwise, `cappedMs = min(elapsedMs, 86,400,000)` and `hours = cappedMs / 3,600,000` (`Assets/Scripts/Core/Offline.cs:50-51`). The report retains both uncapped `ElapsedMs` and processed `CappedMs` (`Assets/Scripts/Core/Offline.cs:98-100`).
4. The calculator rejects balance data unless `0 <= Rate <= 1`, `CapMs > 0`, and `0 <= MinimumDurationMs <= CapMs` (`Assets/Scripts/Core/Offline.cs:36-40`).

## Captured rate and eligible snapshots

The persistent activity kinds are `Idle`, `Fighting`, `Mining`, `Chopping`, and `Gathering` (`Assets/Scripts/Data/State/GameTypes.cs:35`). Offline rewards are eligible only for a non-null snapshot that:

- matches the current content/configuration version;
- has non-negative actions, XP, and coins rates, positive map density, and survival factor in `[0, 1]`;
- matches the character's current character and activity revisions; and
- is non-idle with `ActionsPerHour > 0`.

These gates are implemented by `SnapshotValidation.IsUsable` and `Offline.SimulateCharacter` (`Assets/Scripts/Core/Offline/SnapshotValidation.cs:21-33`, `Assets/Scripts/Core/Offline.cs:133-141`). `Fighting` uses combat rewards; the other eligible kinds are the three canonical harvest activities mapped to Mining, Chopping, or Gathering skill XP (`Assets/Scripts/Core/Common/ActivitySkillMapping.cs:8-17`, `Assets/Scripts/Core/Offline.cs:146-179`). Idle, stale, zero-rate, and unusable snapshots produce zero actions and no account-level offline report unless another character earns actions (`Assets/Scripts/Core/Offline.cs:119-141`, `Assets/Scripts/Core/Offline.cs:89-100`).

The active snapshot captures the rate; offline processing does not recompute combat or harvesting. Combat capture uses:

`ActionsPerHour = (3,600,000 / (TimeToKillMs / max(0.1, SkillDamageRateMultiplier) + TravelOverheadMs)) * MapDensity * (1 + CombatBonusPct) * SurvivalFactor`

and captures monster XP plus average monster coins per action (`Assets/Scripts/Core/Activity.cs:40-75`). Harvest capture uses `HarvestsPerHour * passive efficiency * account activity efficiency`, captures node XP, and sets coins per action to zero (`Assets/Scripts/Core/Activity.cs:78-105`). Direct snapshot computation returns a zero-rate snapshot for an absent content target, while normal activity assignment rejects invalid targets before capture (`Assets/Scripts/Core/Activity.cs:35-38`, `Assets/Scripts/Core/Activity.cs:78-81`, `Assets/Scripts/Core/Activity.cs:139-170`).

## Bulk calculation

For each eligible character:

`actions = floor(ActionsPerHour * OfflineRate * cappedMs / 3,600,000)`

with saturation at `int.MaxValue`; zero rounded actions earn nothing (`Assets/Scripts/Core/Offline.cs:137-141`). XP and coins are each bulk-calculated as `floor(actions * per-action value)` (`Assets/Scripts/Core/Offline.cs:143-144`). Fighting XP is applied to both character and Combat progression; harvest XP is applied to its mapped skill (`Assets/Scripts/Core/Offline.cs:168-181`). Production orchestration supplies zero account bonuses to the offline call (`Assets/Scripts/Managers/GameManager.cs:787-801`).

Fighting loot uses `ExpectedDropTable`: always drops, weighted-main expected quantities, and tertiary expected quantities are accumulated per item, then each item quantity is rounded by `floor(expected)` plus one seeded fractional-remainder draw (`Assets/Scripts/Core/DropSystem.cs:88-144`). Harvest loot independently calculates each entry as `actions * chance * drop multiplier * average(min,max)`, using the same floor-plus-fractional-remainder rule (`Assets/Scripts/Core/DropSystem.cs:21-47`). Coins go to the shared bank; loot is added to the character inventory and excess is reported as overflow (`Assets/Scripts/Core/Offline.cs:62-85`).

This is explicitly a no-tick, no-individual-kill calculation. Core multiplies the captured rate once per character, and the fighting bulk resolver iterates loot-table entries rather than `actions` kills (`Assets/Scripts/Core/Offline.cs:137-163`, `Assets/Scripts/Core/DropSystem.cs:102-143`).

## Determinism

Production derives one 32-bit seed from the account stable ID, the persisted `LastSeenAt`, and the supplied `now`, in that order (`Assets/Scripts/Core/Common/OfflineSeed.cs:9-17`). `GameManager` constructs a seeded `SystemRandomSource` and passes it into the single account calculation (`Assets/Scripts/Managers/GameManager.cs:791-801`, `Assets/Scripts/Core/Common/IRandomSource.cs:16-30`). Therefore the same complete account snapshot, content/configuration, elapsed window (`LastSeenAt` and `now`), character order, and seed-driven iteration order produce the same rewards. The calculator never reads Unity time or creates its own random source.

## Implemented boundaries and known gaps

- Backward-clock abuse is rejected, not recovered: Core throws before mutation, and `GameManager.ProcessOffline` has no local recovery path around that call (`Assets/Scripts/Core/Offline.cs:29-35`, `Assets/Scripts/Managers/GameManager.cs:791-803`). A controlled clock-rollback recovery policy remains unimplemented.
- Snapshot validity does not inspect `SnapshotAt`, `TravelOverheadMs`, or target existence. Normal assignment validates authored targets, but a manually malformed persisted snapshot can still award its captured XP/coins while missing target content suppresses loot (`Assets/Scripts/Core/Offline/SnapshotValidation.cs:21-33`, `Assets/Scripts/Core/Offline.cs:143-163`).
- Rewards are calculated into a new account and saved before the live session commit, but there is no persisted pending-claim/acknowledgement record; the displayed report is memory-only and acknowledgement only clears it (`Assets/Scripts/Managers/GameManager.cs:803-823`). Interrupted-claim recovery and idempotent acknowledgement belong to P0-11, not this calculation contract.
- Existing tests cover configured-rate XP, inventory overflow, stale character-revision rejection, config-unit conversion, and bulk-drop fractional rounding (`Assets/Scripts/Tests/EditMode/ProgressionAndOfflineTests.cs:23-165`, `Assets/Scripts/Tests/EditMode/ContentValidationTests.cs:65-93`, `Assets/Scripts/Tests/EditMode/DropAndActivityTests.cs:136-175`). There are no dedicated tests for the elapsed minimum, cap application, backward clock, `OfflineSeed` repeatability, or complete same-input offline-result repeatability.
