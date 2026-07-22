using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using UnityEngine;

namespace IdleCloud.View
{
    public sealed class CombatView : MonoBehaviour
    {
        [Header("Grid Combat")]
        public GridPathfinder pathfinder;
        [Min(1)] public int attackRangeCells = 1;
        [Min(0)] public int maxAttackHeightDelta = 1;
        [Min(1)] public int maxCatchUpTicksPerFrame = 5;
        [Min(0f)] public float playerFootprintRadius = 0.28f;
        [SerializeField] private PlayerController player;
        [SerializeField] private WorldMapContext mapContext;
        [SerializeField] private LootBagView lootBagPrefab;

        private PlayerController _player;
        private readonly List<CombatTargetView> _targets = new List<CombatTargetView>();
        private readonly Dictionary<string, LootBagView> _lootBags = new Dictionary<string, LootBagView>();
        private CombatTargetView _target;
        private bool _combatStarted;
        private bool _targetWasManuallySelected;
        private bool _chasePathOwned;
        private bool _manualMovementSuspended;
        // An encounter-driven target must not make the player path toward the mob: the mob owns
        // approach positioning, while ActiveSim remains the sole source of combat damage/timing.
        private bool _mobEngagement;
        private bool _autoToggleStateInitialized;
        private bool _lastAutoCombatEnabled;
        private double _tickAccumulator;
        private long _simTimeMs;
        private bool _simClockInitialized;
        private string _warnedStartFailureMonsterId;
        private bool _lootIntentActive;
        private bool _lootPrefabWarningShown;
        private bool _lootSpawnWarningShown;
        private GameManager _lootEventManager;

        public CombatTargetView CurrentTarget => _target;

        public void Configure(PlayerController player, IEnumerable<CombatTargetView> targets)
        {
            if (_player != null)
            {
                _player.ManualMoveRequested -= HandleManualMove;
                _player.CombatTargetSelected -= HandleTargetSelected;
                _player.LootTargetSelected -= HandleLootTargetSelected;
                _player.LootTargetReached -= HandleLootTargetReached;
                _player.LootTargetCancelled -= HandleLootTargetCancelled;
            }
            _player = player;
            if (_player != null)
            {
                _player.ManualMoveRequested += HandleManualMove;
                _player.CombatTargetSelected += HandleTargetSelected;
                _player.LootTargetSelected += HandleLootTargetSelected;
                _player.LootTargetReached += HandleLootTargetReached;
                _player.LootTargetCancelled += HandleLootTargetCancelled;
            }
            _targets.Clear();
            if (targets != null) _targets.AddRange(targets);
        }

        private void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null || mapContext == null)
            {
                Debug.LogWarning("[CombatView] Assign Player and World Map Context in the Inspector.", this);
                return;
            }
            Configure(player, mapContext.CombatTargets);
            EnsureLootSubscriptions(GameManager.Instance);
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.ManualMoveRequested -= HandleManualMove;
                _player.CombatTargetSelected -= HandleTargetSelected;
                _player.LootTargetSelected -= HandleLootTargetSelected;
                _player.LootTargetReached -= HandleLootTargetReached;
                _player.LootTargetCancelled -= HandleLootTargetCancelled;
            }
            RemoveLootSubscriptions();
            foreach (LootBagView bag in _lootBags.Values)
                if (bag != null) Destroy(bag.gameObject);
            _lootBags.Clear();
        }

        private void Update()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || _player == null) return;
            EnsureLootSubscriptions(manager);
            if (pathfinder == null) pathfinder = FindFirstObjectByType<GridPathfinder>();
            if (_lootIntentActive) return;

            bool autoCombatEnabled = manager.AutoCombatEnabled;
            if (!_autoToggleStateInitialized)
            {
                _autoToggleStateInitialized = true;
                _lastAutoCombatEnabled = autoCombatEnabled;
            }
            else
            {
                if (autoCombatEnabled && !_lastAutoCombatEnabled)
                    _manualMovementSuspended = false;
                _lastAutoCombatEnabled = autoCombatEnabled;
            }

            if (_target == null || !_target.IsAvailable)
            {
                _target?.SetSelected(false);
                _chasePathOwned = false;
                _target = autoCombatEnabled && !_manualMovementSuspended ? SelectAutoTarget() : null;
                _target?.SetSelected(true);
                _targetWasManuallySelected = false;
                _mobEngagement = false;
            }
            if (!_targetWasManuallySelected && !autoCombatEnabled)
            {
                _target?.SetSelected(false);
                _target = null;
                _combatStarted = false;
                _chasePathOwned = false;
                return;
            }
            if (_target == null) return;

            if (!_combatStarted)
            {
                _combatStarted = manager.StartActiveCombat(_target.MonsterId);
                if (!_combatStarted)
                {
                    _chasePathOwned = false;
                    WarnCombatStartFailedOnce(manager, _target.MonsterId);
                    return;
                }
                _warnedStartFailureMonsterId = null;
            }

            long tickMs = CombatTimeContract.DefaultStepMilliseconds;
            int maxTicks = Math.Max(1, maxCatchUpTicksPerFrame);
            long wallClockMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!_simClockInitialized)
            {
                _simTimeMs = wallClockMs;
                _simClockInitialized = true;
            }
            else if (wallClockMs > _simTimeMs && wallClockMs - _simTimeMs > maxTicks * tickMs)
            {
                _simTimeMs = wallClockMs;
                _tickAccumulator = 0d;
                return;
            }

            double tickIntervalSeconds = tickMs / 1000.0;
            _tickAccumulator += Time.deltaTime;
            int ticksThisFrame = 0;
            bool terminalTick = false;
            while (_tickAccumulator >= tickIntervalSeconds && ticksThisFrame < maxTicks)
            {
                _tickAccumulator -= tickIntervalSeconds;
                _simTimeMs += tickMs;
                ticksThisFrame++;

                Vector3 from = _player.LogicalPosition;
                Vector3 to = _target.transform.position;
                double distance = Vector2.Distance(from, to);
                bool inRange = true;
                bool lineOfSight = true;
                int cellDistance = pathfinder != null ? pathfinder.CellDistance(from, to) : int.MaxValue;
                // int.MaxValue = no usable grid (sentinel): keep the permissive gridless facts so
                // combat still functions in scenes without terrain, same as pathfinder == null.
                if (cellDistance != int.MaxValue)
                {
                    distance = cellDistance;
                    inRange = cellDistance <= attackRangeCells;

                    bool hasPlayerHeight = pathfinder.TryGetHeightAt(from, out int playerHeight);
                    bool hasTargetHeight = pathfinder.TryGetHeightAt(to, out int targetHeight);
                    if (hasPlayerHeight && hasTargetHeight)
                        inRange &= Mathf.Abs(playerHeight - targetHeight) <= maxAttackHeightDelta;

                    lineOfSight = pathfinder.HasLineOfSight(from, to);
                }

                if (_chasePathOwned && inRange)
                {
                    _player.CancelPath();
                    _chasePathOwned = false;
                }

                ActiveCombatTickResult result = manager.TickActiveCombat(
                    _target.EntityId,
                    _target.MonsterId,
                    new CombatWorldFacts
                    {
                        TargetAvailable = _target.IsAvailable,
                        TargetInRange = inRange,
                        LineOfSight = lineOfSight,
                        Distance = distance,
                        PrimaryTargetActorId = _target.EntityId,
                        Spatial = CombatSpatialAdapter.Capture(
                            manager.GetSelectedCharacter()?.Id,
                            _player,
                            _targets,
                            pathfinder,
                            playerFootprintRadius),
                        HostileAttackers = CaptureHostileAttackers(),
                    },
                    null,
                    _simTimeMs);

                // A rejected tick (player_recovering, activity mismatch, ...) means the sim did not
                // run — drop chase ownership; the next successful tick re-establishes it if needed.
                // Per-command CombatEventKind.CommandRejected (e.g. a skill on cooldown) deliberately
                // does NOT clear it: the chase itself is still valid.
                if (!string.IsNullOrEmpty(result.RejectionReason))
                    _chasePathOwned = false;

                if (result.Simulation?.Events != null)
                {
                    foreach (CombatEvent combatEvent in result.Simulation.Events)
                    {
                        if (combatEvent.Kind == CombatEventKind.DamageApplied && combatEvent.Amount > 0 &&
                            combatEvent.TargetId == _target.EntityId)
                            _target.NotifyAttacked();
                        if (!_mobEngagement && combatEvent.Kind == CombatEventKind.MovementRequested && combatEvent.Reason == "target_out_of_range")
                        {
                            _player.RequestMoveToWorld(_target.transform.position);
                            _chasePathOwned = true;
                        }
                    }
                }

                if (result.Reward != null)
                {
                    SpawnLootForReward(manager, result.Reward);
                    bool currentTargetDefeated = result.Simulation?.DefeatedActorIds != null &&
                        result.Simulation.DefeatedActorIds.Contains(_target.EntityId);
                    if (currentTargetDefeated)
                    {
                        _target.SetSelected(false);
                        _target.Defeat();
                        _target = null;
                        _combatStarted = false;
                        _targetWasManuallySelected = false;
                        _mobEngagement = false;
                    }
                    _chasePathOwned = false;
                    terminalTick = true;
                    break;
                }

                if (result.Simulation?.PlayerDefeated == true)
                {
                    foreach (CombatTargetView combatTarget in _targets)
                        combatTarget?.GetComponent<EnemyController>()?.NotifyPlayerDefeated(_player);
                    if (mapContext?.PlayerSpawnPoint != null)
                        _player.TeleportTo(mapContext.PlayerSpawnPoint.position, mapContext.PlayerSpawnPoint.rotation);
                    _target?.SetSelected(false);
                    _target = null;
                    _combatStarted = false;
                    _targetWasManuallySelected = false;
                    _mobEngagement = false;
                    _chasePathOwned = false;
                    terminalTick = true;
                    break;
                }
            }

            if (terminalTick) _tickAccumulator = 0d;
        }

        private void EnsureLootSubscriptions(GameManager manager)
        {
            if (_lootEventManager == manager) return;
            RemoveLootSubscriptions();
            if (manager == null) return;
            _lootEventManager = manager;
            _lootEventManager.LootSpawned += HandleLootSpawned;
            _lootEventManager.LootPickedUp += HandleLootPickedUp;
            _lootEventManager.LootExpired += HandleLootExpired;
            _lootEventManager.LootCleared += HandleLootCleared;
        }

        private void RemoveLootSubscriptions()
        {
            if (_lootEventManager == null) return;
            _lootEventManager.LootSpawned -= HandleLootSpawned;
            _lootEventManager.LootPickedUp -= HandleLootPickedUp;
            _lootEventManager.LootExpired -= HandleLootExpired;
            _lootEventManager.LootCleared -= HandleLootCleared;
            _lootEventManager = null;
        }

        private void SpawnLootForReward(GameManager manager, CombatReward reward)
        {
            if (manager == null || reward?.KillLoot == null) return;
            string mapId = manager.GetSelectedCharacter()?.MapId;
            if (string.IsNullOrWhiteSpace(mapId)) return;

            foreach (KillLootRecord killLoot in reward.KillLoot)
            {
                if (killLoot == null) continue;
                CombatTargetView actor = null;
                foreach (CombatTargetView candidate in _targets)
                {
                    if (candidate != null && candidate.EntityId == killLoot.ActorEntityId)
                    {
                        actor = candidate;
                        break;
                    }
                }
                // BeginMobEngagement accepts targets outside _targets; never discard their loot.
                if (actor == null && _target != null && _target.EntityId == killLoot.ActorEntityId)
                    actor = _target;
                if (pathfinder == null) pathfinder = FindFirstObjectByType<GridPathfinder>();

                // Last-resort anchor is the player's own tile — rolled loot must not vanish.
                Vector3 logical = actor != null ? actor.LogicalPosition : _player.LogicalPosition;
                if (pathfinder == null || !pathfinder.TryGetCell(logical, out Vector2Int cell))
                {
                    if (!_lootSpawnWarningShown)
                    {
                        _lootSpawnWarningShown = true;
                        Debug.LogWarning("[CombatView] Could not resolve a tile for rolled loot; drop skipped.", this);
                    }
                    continue;
                }
                manager.SpawnLoot(
                    mapId,
                    new CombatTileCoordinate(cell.x, cell.y),
                    Mathf.RoundToInt(logical.z),
                    killLoot.Stacks ?? new List<ItemStack>());
            }
        }

        private void HandleLootSpawned(LootSpawnedEvent payload)
        {
            if (payload == null || payload.Record == null || string.IsNullOrWhiteSpace(payload.DropId)) return;
            if (lootBagPrefab == null)
            {
                if (!_lootPrefabWarningShown)
                {
                    _lootPrefabWarningShown = true;
                    Debug.LogWarning("[CombatView] Loot spawned but no LootBagView prefab is assigned.", this);
                }
                return;
            }

            if (_lootBags.TryGetValue(payload.DropId, out LootBagView prior) && prior != null)
                Destroy(prior.gameObject);

            GroundLootRecord record = payload.Record;
            Vector3 world = new Vector3(record.Tile.X, record.Tile.Y, 0f);
            if (pathfinder != null && pathfinder.TryGetTileWorldPosition(record.Tile, record.Floor, out Vector3 tileWorld))
                world = new Vector3(tileWorld.x, tileWorld.y, 0f);

            LootBagView bag = Instantiate(lootBagPrefab, world, Quaternion.identity);
            bag.ConfigureRuntimeDrop(
                payload.DropId,
                new Vector3(world.x, world.y, record.Floor),
                record.SpawnedAtMs,
                _lootEventManager != null ? _lootEventManager.LootDespawnMs : 0L);
            _lootBags[payload.DropId] = bag;
        }

        private void HandleLootPickedUp(LootPickedUpEvent payload)
        {
            if (payload == null || payload.PickedStacks == null || payload.PickedStacks.Count == 0) return;
            if (!_lootBags.TryGetValue(payload.DropId, out LootBagView bag) || bag == null) return;
            bool hasRemaining = payload.RemainingStacks != null && payload.RemainingStacks.Count > 0;
            bag.NotifyPickedUp(hasRemaining);
            if (hasRemaining) return;
            if (payload.Vacuum && _player != null)
            {
                _lootBags.Remove(payload.DropId);
                bag.FlyToWorldPosition(_player.LogicalPosition);
                return;
            }
            DestroyLootBag(payload.DropId);
        }

        private void HandleLootExpired(LootExpiredEvent payload)
        {
            if (payload == null || !_lootBags.TryGetValue(payload.DropId, out LootBagView bag)) return;
            bag?.NotifyExpired();
            DestroyLootBag(payload.DropId);
        }

        private void HandleLootCleared(string _)
        {
            foreach (string dropId in new List<string>(_lootBags.Keys))
                DestroyLootBag(dropId);
        }

        private void DestroyLootBag(string dropId)
        {
            if (!_lootBags.TryGetValue(dropId, out LootBagView bag)) return;
            _lootBags.Remove(dropId);
            if (bag != null) Destroy(bag.gameObject);
        }

        private void HandleLootTargetSelected(LootBagView lootBag)
        {
            if (lootBag == null) return;
            _lootIntentActive = true;
            _manualMovementSuspended = true;
            _chasePathOwned = false;
            _target?.SetSelected(false);
            _target = null;
            _combatStarted = false;
            _targetWasManuallySelected = false;
            _mobEngagement = false;
            GameManager.Instance?.Stop();
        }

        private void HandleLootTargetReached(LootBagView lootBag)
        {
            // ReferenceEquals: a destroyed bag must still release the intent gate;
            // its DropId stays readable and the pickup is a harmless no-op then.
            if (ReferenceEquals(lootBag, null)) return;
            _lootIntentActive = false;
            GameManager.Instance?.TryPickupLoot(lootBag.DropId);
        }

        private void HandleLootTargetCancelled(LootBagView _)
        {
            _lootIntentActive = false;
        }

        // GameManager.Assign swallows activity-validation failures silently (mirrors store.ts),
        // so a refused combat start would otherwise be invisible: auto-combat toggles fine but
        // nothing ever attacks. Warn once per monster id, with the map facts that are the usual
        // cause (character's data-level MapId must equal the monster's MapId).
        private void WarnCombatStartFailedOnce(GameManager manager, string monsterId)
        {
            if (_warnedStartFailureMonsterId == monsterId) return;
            _warnedStartFailureMonsterId = monsterId;
            string characterMap = manager.GetSelectedCharacter()?.MapId ?? "<no character>";
            string monsterMap = RuntimeContent.Monsters.TryGetValue(monsterId, out var monster) ? monster.MapId : "<unknown monster>";
            Debug.LogWarning(
                $"[CombatView] Combat with '{monsterId}' did not start — character is on map '{characterMap}', " +
                $"monster belongs to map '{monsterMap}'. Travel to the monster's map first.", this);
        }

        private void HandleManualMove(Vector3 _)
            => CancelCombatForManualMovement();

        public void CancelCombatForManualMovement()
        {
            _player?.CancelLootIntent();
            _lootIntentActive = false;
            _manualMovementSuspended = true;
            _chasePathOwned = false;
            _target?.SetSelected(false);
            _target = null;
            _combatStarted = false;
            _targetWasManuallySelected = false;
            _mobEngagement = false;
            GameManager.Instance?.Stop();
        }

        private void HandleTargetSelected(CombatTargetView target)
        {
            if (target == null || !_targets.Contains(target) || !target.IsAvailable) return;
            if (_target == target && _combatStarted) return;

            _lootIntentActive = false;
            _manualMovementSuspended = false;
            _target?.SetSelected(false);
            _target = target;
            _target.SetSelected(true);
            _combatStarted = false;
            _targetWasManuallySelected = true;
            _mobEngagement = false;
            _chasePathOwned = false;
            GameManager.Instance?.EnqueueCombatCommand(new CombatCommand
            {
                Kind = CombatCommandKind.SelectTarget,
                TargetId = target.EntityId,
            });
        }

        /// <summary>
        /// Starts the existing active-combat simulation for an approaching mob without taking
        /// over player movement. The selected mob remains the single authoritative target the
        /// current ActiveSim contract supports.
        /// </summary>
        public void BeginMobEngagement(CombatTargetView target)
        {
            // Encounter ownership is scene-authored and can include targets omitted from an older
            // WorldMapContext explicit list. Accept any live target in this loaded scene.
            if (target == null || !target.IsAvailable) return;
            if (_target == target && _combatStarted) return;

            _player?.CancelLootIntent();
            _lootIntentActive = false;
            _manualMovementSuspended = false;
            _target?.SetSelected(false);
            _target = target;
            _target.SetSelected(true);
            _combatStarted = false;
            _targetWasManuallySelected = true;
            _mobEngagement = true;
            _chasePathOwned = false;
            GameManager.Instance?.EnqueueCombatCommand(new CombatCommand
            {
                Kind = CombatCommandKind.SelectTarget,
                TargetId = target.EntityId,
            });
        }

        private CombatTargetView SelectAutoTarget()
        {
            var candidates = new List<CombatCandidate>();
            foreach (CombatTargetView candidate in _targets)
            {
                if (candidate == null) continue;
                Vector3 playerPosition = _player.LogicalPosition;
                double distance = Vector2.Distance(candidate.transform.position, playerPosition);
                if (pathfinder != null &&
                    pathfinder.TryGetCell(playerPosition, out Vector2Int playerCell) &&
                    pathfinder.TryGetCell(candidate.transform.position, out Vector2Int candidateCell))
                    distance = pathfinder.CellDistance(playerPosition, candidate.transform.position);

                candidates.Add(new CombatCandidate
                {
                    EntityId = candidate.EntityId,
                    MonsterId = candidate.MonsterId,
                    Available = candidate.IsAvailable,
                    Distance = distance,
                });
            }
            CombatCandidate selected = AutoCombatPolicy.SelectTarget(candidates);
            if (selected == null) return null;
            foreach (CombatTargetView candidate in _targets)
                if (candidate != null && candidate.EntityId == selected.EntityId) return candidate;
            return null;
        }

        private List<HostileAttackerFacts> CaptureHostileAttackers()
        {
            var attackers = new List<HostileAttackerFacts>();
            foreach (CombatTargetView target in FindObjectsByType<CombatTargetView>(FindObjectsSortMode.None))
            {
                if (target == null || !target.IsAvailable) continue;
                EnemyController enemy = target.GetComponent<EnemyController>();
                if (enemy == null || !enemy.CanAttackPlayer) continue;
                attackers.Add(new HostileAttackerFacts
                {
                    ActorId = target.EntityId,
                    MonsterId = target.MonsterId,
                    CanAttack = true,
                });
            }
            return attackers;
        }
    }
}
