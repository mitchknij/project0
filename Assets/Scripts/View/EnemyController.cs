using System;
using System.Collections;
using System.Collections.Generic;
using IdleCloud.Data;
using UnityEngine;

namespace IdleCloud.View
{
    public enum MobCombatState
    {
        Idle, Detecting, Pursuing, MovingToAttackPosition, Attacking,
        WaitingForPosition, ReturningHome, Dead, Respawning,
    }

    public class EnemyController : MonoBehaviour
    {
        [Header("Animations")]
        public Sprite[] idleFrames;
        public Sprite[] walkFrames;

        [Header("Wander")]
        [Min(0f)] public float wanderRadius = 1.5f;
        [Min(0f)] public float moveSpeed = 0.8f;
        [Min(0f)] public float idleDuration = 2f;
        [Min(0f)] public float walkDuration = 1.5f;

        [Header("Encounter")]
        [Tooltip("Explicit encounter ownership. Child encounters are resolved automatically for legacy placed mobs.")]
        [SerializeField] private MobEncounter encounter;
        [SerializeField] private MobSpawnPoint spawnPoint;
        [SerializeField] private PlayerController player;

        [Header("Aggro")]
        [SerializeField, Min(0f)] private float aggroRadius = 3f;
        [SerializeField, Min(0f)] private float aggroDetectionDelay = 0.4f;
        [SerializeField, Min(0f)] private float returnLockSeconds = 1f;

        [Header("Attack Positioning")]
        [Tooltip("Melee uses adjacent skill-valid tiles. Ranged keeps its preferred distance.")]
        [SerializeField] private bool ranged;
        [SerializeField, Min(1)] private int meleeAttackRangeCells = 1;
        [SerializeField, Min(0)] private int rangedMinimumRangeCells = 2;
        [SerializeField, Min(1)] private int rangedPreferredRangeCells = 3;
        [SerializeField, Min(1)] private int rangedMaximumRangeCells = 5;
        [SerializeField, Min(0.05f)] private float positionRetrySeconds = 0.35f;
        [SerializeField, Range(0f, 0.2f)] private float positionRetryJitter = 0.2f;
        [SerializeField] private int retrySeed = 12345;

        [Header("Runtime Debug (read-only)")]
        [SerializeField] private MobCombatState debugState;
        [SerializeField] private float debugPlayerDistance;
        [SerializeField] private bool debugPlayerInsideLeash;
        [SerializeField] private Vector2Int debugMobCell;
        [SerializeField] private Vector2Int debugPlayerCell;

        [Header("Pathfinding")]
        public GridPathfinder pathfinder;
        [SerializeField] private Grid grid;

        [Header("Elevation")]
        [SerializeField] private int startHeight = 0;

        private SpriteRenderer _renderer;
        private SpriteSheetAnimator _animator;
        private Vector3 _origin;
        private Vector3 _home;
        private Vector3 _logical;
        private Vector2Int _occupiedCell;
        private bool _hasOccupancy;
        private bool _initialized;
        private Coroutine _movement;
        private MobCombatState _state;
        private float _detectUntil;
        private float _nextIdleActionAt;
        private float _nextPositionRetryAt;
        private float _returnLockedUntil;
        private Vector2Int _lastTargetCell;
        private int _retrySequence;
        private TextMesh _indicator;
        private Coroutine _indicatorRoutine;

        public Vector3 LogicalPosition => _logical;
        public bool HasLogicalPosition { get; private set; }
        public MobCombatState State => _state;
        public MobEncounter Encounter => encounter;
        public bool CanAttackPlayer => _state == MobCombatState.Attacking && player != null;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<SpriteSheetAnimator>();
        }

        private void Start()
        {
            ResolveReferences();
            _origin = transform.position;
            _home = spawnPoint != null ? spawnPoint.HomePosition : _origin;
            int initialHeight = startHeight;
            if (pathfinder != null && pathfinder.TryGetHeightAt(transform.position, out int terrainHeight)) initialHeight = terrainHeight;
            _logical = new Vector3(transform.position.x, transform.position.y, initialHeight);
            HasLogicalPosition = true;
            _occupiedCell = WorldToCell(_logical);
            _hasOccupancy = TileOccupancy.TryOccupy(_occupiedCell, this);
            if (!_hasOccupancy) Debug.LogWarning("[EnemyController] Spawn tile is already occupied: " + _occupiedCell, this);
            encounter?.Register(this);
            _state = MobCombatState.Idle;
            _nextIdleActionAt = Time.time + idleDuration;
            if (_animator != null) _animator.SetFrames(idleFrames);
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized) return;
            ResolveReferences();
            _occupiedCell = WorldToCell(_logical);
            _hasOccupancy = TileOccupancy.TryOccupy(_occupiedCell, this);
            if (_state == MobCombatState.Dead) _state = MobCombatState.Respawning;
        }

        private void OnDisable()
        {
            StopMovement();
            TileOccupancy.ReleaseAll(this);
            _hasOccupancy = false;
            encounter?.Unregister(this);
        }

        private void Update()
        {
            if (!_initialized || _state == MobCombatState.Dead || _state == MobCombatState.Respawning) return;
            ResolveReferences();
            UpdateDebugFacts();
            if (player == null) return;

            switch (_state)
            {
                case MobCombatState.Idle:
                    UpdateIdle();
                    break;
                case MobCombatState.Detecting:
                    if (!IsPlayerInAggroRange()) { _state = MobCombatState.Idle; break; }
                    if (Time.time >= _detectUntil) AcquireAggro(false);
                    break;
                case MobCombatState.Pursuing:
                case MobCombatState.MovingToAttackPosition:
                case MobCombatState.Attacking:
                case MobCombatState.WaitingForPosition:
                    UpdateAggro();
                    break;
                case MobCombatState.ReturningHome:
                    if (_movement == null)
                    {
                        if (AtHome()) _state = MobCombatState.Idle;
                        else StartMove(_home, MobCombatState.ReturningHome);
                    }
                    break;
            }
        }

        private void UpdateIdle()
        {
            if (Time.time >= _returnLockedUntil && IsPlayerInAggroRange())
            {
                _state = MobCombatState.Detecting;
                _detectUntil = Time.time + aggroDetectionDelay;
                return;
            }
            if (_movement == null && Time.time >= _nextIdleActionAt)
            {
                Vector3 target = _origin + new Vector3(
                    UnityEngine.Random.Range(-wanderRadius, wanderRadius),
                    UnityEngine.Random.Range(-wanderRadius * 0.5f, wanderRadius * 0.5f), 0f);
                StartMove(target, MobCombatState.Idle);
                _nextIdleActionAt = Time.time + idleDuration + UnityEngine.Random.Range(-0.5f, 0.5f);
            }
        }

        private void UpdateAggro()
        {
            if (player == null || !IsWithinLeash(player.LogicalPosition)) { LoseAggro(); return; }
            Vector2Int playerCell = WorldToCell(player.LogicalPosition);
            if (playerCell != _lastTargetCell)
            {
                _lastTargetCell = playerCell;
                StopMovement();
                _state = MobCombatState.Pursuing;
                _nextPositionRetryAt = 0f;
            }

            int distance = CellDistance(_occupiedCell, playerCell);
            if (IsValidAttackDistance(distance))
            {
                StopMovement();
                _state = MobCombatState.Attacking;
                return;
            }

            if (_movement != null || Time.time < _nextPositionRetryAt) return;
            if (TryFindAttackPosition(playerCell, out Vector3 position, out bool anyFreePosition))
            {
                StartMove(position, MobCombatState.Pursuing);
                _state = MobCombatState.MovingToAttackPosition;
            }
            else
            {
                _state = MobCombatState.WaitingForPosition;
                _nextPositionRetryAt = Time.time + positionRetrySeconds + NextRetryJitter();
                // A blocked route or a contested attack tile is transient crowding, not a reason
                // to forget the player. Keep aggro and retry until an actual invalidation event
                // (leash exit, player death, or mob death) occurs.
            }
        }

        /// <summary>Called by CombatTargetView only for an actual player hit, not target selection.</summary>
        public void NotifyAttacked()
        {
            if (!_initialized || _state == MobCombatState.Dead || _state == MobCombatState.Respawning) return;
            ResolveReferences();
            AcquireAggro(true);
        }

        /// <summary>Encounter alerts are deliberately non-propagating.</summary>
        public void ReceiveEncounterAlert(PlayerController alertedPlayer)
        {
            if (alertedPlayer != null) player = alertedPlayer;
            AcquireAggro(false);
        }

        public void HandleDefeated()
        {
            StopMovement();
            TileOccupancy.ReleaseAll(this);
            _hasOccupancy = false;
            _state = MobCombatState.Dead;
        }

        /// <summary>CombatView calls this when the shared simulation reports player death.</summary>
        public void NotifyPlayerDefeated(PlayerController defeatedPlayer)
        {
            if (defeatedPlayer != null && player != defeatedPlayer) return;
            if (_state != MobCombatState.Pursuing && _state != MobCombatState.MovingToAttackPosition &&
                _state != MobCombatState.Attacking && _state != MobCombatState.WaitingForPosition &&
                _state != MobCombatState.Detecting) return;
            LoseAggro();
        }

        /// <summary>Used by CombatTargetView's respawn loop; returns false while the spawn tile is blocked.</summary>
        public bool TryRespawnAtSpawn()
        {
            if (!_initialized) return false;
            TileOccupancy.ReleaseAll(this);
            _logical = new Vector3(_origin.x, _origin.y, startHeight);
            if (pathfinder != null && pathfinder.TryGetHeightAt(_origin, out int height)) _logical.z = height;
            _occupiedCell = WorldToCell(_logical);
            if (!TileOccupancy.TryOccupy(_occupiedCell, this)) return false;
            _hasOccupancy = true;
            transform.position = new Vector3(_logical.x, _logical.y, transform.position.z);
            _state = MobCombatState.Idle;
            _returnLockedUntil = Time.time + returnLockSeconds;
            _nextIdleActionAt = Time.time + idleDuration;
            return true;
        }

        private void AcquireAggro(bool directlyAttacked)
        {
            if (player == null || Time.time < _returnLockedUntil || !IsWithinLeash(player.LogicalPosition)) return;
            bool newlyAggroed = _state != MobCombatState.Pursuing && _state != MobCombatState.MovingToAttackPosition &&
                _state != MobCombatState.Attacking && _state != MobCombatState.WaitingForPosition;
            StopMovement();
            _state = MobCombatState.Pursuing;
            _lastTargetCell = WorldToCell(player.LogicalPosition);
            _nextPositionRetryAt = 0f;
            if (newlyAggroed) ShowIndicator("!", Color.red);
            if (newlyAggroed)
                FindFirstObjectByType<CombatView>()?.BeginMobEngagement(GetComponent<CombatTargetView>());
            if (directlyAttacked) encounter?.AlertNearby(this, player);
        }

        private void LoseAggro()
        {
            StopMovement();
            ShowIndicator("?", new Color(1f, 0.82f, 0.2f));
            _returnLockedUntil = Time.time + returnLockSeconds;
            _state = AtHome() ? MobCombatState.Idle : MobCombatState.ReturningHome;
        }

        private bool TryFindAttackPosition(Vector2Int playerCell, out Vector3 bestPosition, out bool anyFreePosition)
        {
            bestPosition = default;
            anyFreePosition = false;
            int minimum = ranged ? rangedMinimumRangeCells : 1;
            int maximum = ranged ? Mathf.Max(rangedMinimumRangeCells, rangedMaximumRangeCells) : meleeAttackRangeCells;
            float bestScore = float.PositiveInfinity;
            for (int y = playerCell.y - maximum; y <= playerCell.y + maximum; y++)
            for (int x = playerCell.x - maximum; x <= playerCell.x + maximum; x++)
            {
                var cell = new Vector2Int(x, y);
                int range = CellDistance(cell, playerCell);
                if (range < minimum || range > maximum || TileOccupancy.IsBlocked(cell, this)) continue;
                Vector3 world = CellToWorld(cell);
                if (pathfinder != null && !pathfinder.IsWalkable(world)) continue;
                anyFreePosition = true;
                List<Vector3> path = pathfinder != null
                    ? pathfinder.FindPath(_logical, world, candidate => TileOccupancy.IsBlocked(candidate, this))
                    : null;
                if (pathfinder != null && (path == null || path.Count == 0)) continue;
                int preferred = ranged ? Mathf.Clamp(rangedPreferredRangeCells, minimum, maximum) : minimum;
                float score = Mathf.Abs(range - preferred) * 1000f + (path?.Count ?? 0) * 10f + x * 0.001f + y * 0.0001f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestPosition = world;
            }
            return !float.IsPositiveInfinity(bestScore);
        }

        private void StartMove(Vector3 destination, MobCombatState stateAfterArrival)
        {
            if (_movement != null || !_hasOccupancy) return;
            _movement = StartCoroutine(MoveRoutine(destination, stateAfterArrival));
        }

        private IEnumerator MoveRoutine(Vector3 destination, MobCombatState stateAfterArrival)
        {
            List<Vector3> path = pathfinder != null
                ? pathfinder.FindPath(_logical, destination, cell => TileOccupancy.IsBlocked(cell, this))
                : null;
            if (path == null || path.Count == 0) { _movement = null; yield break; }

            foreach (Vector3 waypoint in path)
            {
                Vector2Int nextCell = WorldToCell(waypoint);
                if (nextCell == _occupiedCell) continue;
                if (!TileOccupancy.TryReserve(nextCell, this)) break;
                if (_animator != null) _animator.SetFrames(walkFrames);
                float elapsed = 0f;
                while (elapsed < walkDuration && Vector2.Distance(transform.position, waypoint) > 0.05f)
                {
                    if (_state == MobCombatState.Dead || _state == MobCombatState.Respawning)
                    {
                        TileOccupancy.ReleaseReservation(nextCell, this);
                        _movement = null;
                        yield break;
                    }
                    Vector2 direction = (Vector2)waypoint - (Vector2)transform.position;
                    Vector2 next = Vector2.MoveTowards(transform.position, waypoint, moveSpeed * Time.deltaTime);
                    transform.position = new Vector3(next.x, next.y, transform.position.z);
                    if (_renderer != null && direction.sqrMagnitude > 0.0001f) _renderer.flipX = direction.x < 0f;
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (Vector2.Distance(transform.position, waypoint) > 0.05f)
                {
                    TileOccupancy.ReleaseReservation(nextCell, this);
                    break;
                }
                Vector2Int previous = _occupiedCell;
                if (!TileOccupancy.TryCommitReservation(previous, nextCell, this)) break;
                _occupiedCell = nextCell;
                _logical = new Vector3(waypoint.x, waypoint.y, waypoint.z);
            }
            if (_animator != null) _animator.SetFrames(idleFrames);
            _movement = null;
            if (_state != MobCombatState.Dead && _state != MobCombatState.Respawning) _state = stateAfterArrival;
        }

        private void StopMovement()
        {
            if (_movement != null) StopCoroutine(_movement);
            _movement = null;
            TileOccupancy.ReleaseAllReservationsFor(this);
            if (_animator != null) _animator.SetFrames(idleFrames);
        }

        private bool IsPlayerInAggroRange()
            => player != null && Vector2.Distance(_logical, player.LogicalPosition) <= aggroRadius && IsWithinLeash(player.LogicalPosition);

        private void UpdateDebugFacts()
        {
            debugState = _state;
            debugMobCell = _occupiedCell;
            if (player == null)
            {
                debugPlayerDistance = float.PositiveInfinity;
                debugPlayerInsideLeash = false;
                return;
            }
            debugPlayerDistance = Vector2.Distance(_logical, player.LogicalPosition);
            debugPlayerInsideLeash = IsWithinLeash(player.LogicalPosition);
            debugPlayerCell = WorldToCell(player.LogicalPosition);
        }

        private bool IsWithinLeash(Vector3 position)
            => encounter == null || encounter.Contains(position);

        private bool AtHome() => Vector2.Distance(_logical, _home) < 0.08f;
        private bool IsValidAttackDistance(int distance)
            => ranged ? distance >= rangedMinimumRangeCells && distance <= rangedMaximumRangeCells : distance <= meleeAttackRangeCells;
        private int CellDistance(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        private Vector2Int WorldToCell(Vector3 world) => grid != null ? (Vector2Int)grid.WorldToCell(world) : Vector2Int.RoundToInt(world);
        private Vector3 CellToWorld(Vector2Int cell)
        {
            Vector3 result = grid != null ? grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0)) : new Vector3(cell.x, cell.y, 0f);
            if (pathfinder != null && pathfinder.TryGetHeightAt(result, out int height)) result.z = height;
            return result;
        }

        private float NextRetryJitter()
        {
            unchecked { _retrySequence = _retrySequence * 1103515245 + retrySeed + 12345; }
            return ((_retrySequence & 0x7fffffff) / (float)int.MaxValue) * positionRetryJitter;
        }

        private void ResolveReferences()
        {
            if (pathfinder == null) pathfinder = FindFirstObjectByType<GridPathfinder>();
            if (grid == null) grid = FindFirstObjectByType<Grid>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (spawnPoint == null) spawnPoint = GetComponentInParent<MobSpawnPoint>();
            if (encounter == null) encounter = spawnPoint != null ? spawnPoint.Encounter : GetComponentInParent<MobEncounter>();
        }

        private void ShowIndicator(string text, Color color)
        {
            if (_indicatorRoutine != null) StopCoroutine(_indicatorRoutine);
            _indicatorRoutine = StartCoroutine(IndicatorRoutine(text, color));
        }

        private IEnumerator IndicatorRoutine(string text, Color color)
        {
            if (_indicator == null)
            {
                var indicatorObject = new GameObject("MobAggroIndicator");
                indicatorObject.transform.SetParent(transform, false);
                indicatorObject.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                _indicator = indicatorObject.AddComponent<TextMesh>();
                _indicator.anchor = TextAnchor.MiddleCenter;
                _indicator.alignment = TextAlignment.Center;
                _indicator.characterSize = 0.18f;
                _indicator.fontSize = 64;
            }
            _indicator.text = text;
            _indicator.color = color;
            _indicator.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.75f);
            if (_indicator != null) _indicator.gameObject.SetActive(false);
            _indicatorRoutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Application.isPlaying ? _origin : transform.position, wanderRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aggroRadius);
        }
    }
}
