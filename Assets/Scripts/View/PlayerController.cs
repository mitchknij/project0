using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IdleCloud.View
{
    // We eisen nu een Rigidbody2D omdat we voor 'echte' physics gaan.
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Ground movement speed in Unity units per second.")]
        [Min(0f)]
        public float speed = 3f;

        [Header("Pathfinding")]
        [Tooltip("Grid pathfinder to route clicks through. Auto-resolved via FindFirstObjectByType if left unassigned.")]
        public GridPathfinder pathfinder;

        [Header("Animations")]
        [Tooltip("Frames shown while standing still.")]
        public Sprite[] idleFrames;
        [Tooltip("Frames shown while walking.")]
        public Sprite[] walkFrames;
        [Tooltip("Idle animation speed, in frames per second.")]
        [Min(0f)]
        public float idleFps = 2f;
        [Tooltip("Walk animation speed, in frames per second.")]
        [Min(0f)]
        public float walkFps = 4f;

        [Header("Elevation")]
        [Tooltip("Fallback initial height (floor-level units) used only when no terrain height data is available yet for the player's starting cell. Otherwise Start() overrides this from the live terrain lookup, so the player never spawns embedded under a taller stacked block.")]
        [SerializeField] private int startHeight = 0;

        [Header("Path Following")]
        [Tooltip("Distance (Unity units) from a waypoint at which it counts as reached and the player advances to the next one along the path.")]
        [Min(0f)]
        [SerializeField] private float arrivalThreshold = 0.08f;

        private Rigidbody2D _rb;
        private SpriteRenderer _renderer;
        private SpriteSheetAnimator _animator;
        private bool _isMoving;

        // Single source of truth for movement/collision/rendering/sorting. XY = world ground
        // position, Z = height in floor-level units. Everything else derives from this vector.
        private Vector3 _logical;

        /// <summary>Read-only 3D logical position (XY = ground, Z = height). Sorting reads this directly.</summary>
        public Vector3 LogicalPosition => _logical;

        /// <summary>Moves the persistent player between map scenes and resets all scene-local movement state.</summary>
        public void TeleportTo(Vector3 worldPosition, Quaternion worldRotation)
        {
            CancelLootIntent();
            ClearPath();
            TileOccupancy.ReleaseAll(this);
            _hasOccupancy = false;

            int height = startHeight;
            if (pathfinder != null && pathfinder.TryGetHeightAt(worldPosition, out int terrainHeight))
                height = terrainHeight;
            _logical = new Vector3(worldPosition.x, worldPosition.y, height);

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            if (_rb != null) _rb.position = new Vector2(_logical.x, _logical.y);
            if (pathfinder != null && pathfinder.TryGetCell(_logical, out _occupiedCell))
                _hasOccupancy = TileOccupancy.TryOccupy(_occupiedCell, this);
        }

        // Click-to-move path state
        private List<Vector3> _path;
        private int _pathIndex;
        private Vector2Int _occupiedCell;
        private bool _hasOccupancy;
        private LootBagView _lootIntent;

        public event Action<Vector3> ManualMoveRequested;
        public event Action<CombatTargetView> CombatTargetSelected;
        public event Action<GatheringNodeView> GatheringNodeSelected;
        public event Action<LootBagView> LootTargetSelected;
        public event Action<LootBagView> LootTargetReached;
        public event Action<LootBagView> LootTargetCancelled;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            // Kinematic: _logical drives the body exclusively via MovePosition. No velocity,
            // AddForce, or gravity — those would create a second, competing source of truth.
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Visuals live on the root after flattening; fall back to a child while migrating.
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            _animator = GetComponent<SpriteSheetAnimator>();
            if (_animator == null) _animator = GetComponentInChildren<SpriteSheetAnimator>();
        }

        void Start()
        {
            if (pathfinder == null) pathfinder = FindFirstObjectByType<GridPathfinder>();

            int initialHeight = startHeight;
            if (pathfinder != null && pathfinder.TryGetHeightAt(transform.position, out int terrainHeight))
                initialHeight = terrainHeight;

            _logical = new Vector3(transform.position.x, transform.position.y, initialHeight);
            if (pathfinder != null && pathfinder.TryGetCell(_logical, out _occupiedCell))
                _hasOccupancy = TileOccupancy.TryOccupy(_occupiedCell, this);
            if (_animator != null) _animator.SetFrames(idleFrames, idleFps);
        }

        private void OnDisable()
        {
            CancelLootIntent();
            TileOccupancy.ReleaseAll(this);
            _hasOccupancy = false;
        }

        void Update()
        {
            // ── Input: Click-to-move ─────────────────────────────────────────────
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                // Directe vertaling van klik naar wereldpositie.
                // Geen "Iso" picks meer nodig, de physics engine doet het werk.
                Camera camera = Camera.main;
                if (camera == null) return;
                Vector3 mouseWorld = camera.ScreenToWorldPoint(mouse.position.ReadValue());
                // Project Auto Sync Transforms is disabled; enemies wander by changing their
                // transforms, so synchronize once at click time before point queries.
                Physics2D.SyncTransforms();
                CombatTargetView target = FindCombatTargetAt(mouseWorld);
                if (target != null)
                {
                    SelectCombatTarget(target);
                    return;
                }

                LootBagView lootBag = FindLootBagAt(mouseWorld);
                if (lootBag != null)
                {
                    SelectLootTarget(lootBag);
                    return;
                }

                GatheringNodeView node = FindGatheringNodeAt(mouseWorld);
                if (node != null)
                {
                    SelectGatheringNode(node);
                    return;
                }
                RequestMoveTo(mouseWorld);
                ManualMoveRequested?.Invoke(mouseWorld);
            }
        }

        void FixedUpdate()
        {
            // ── Pathfinding & Beweging ───────────────────────────────────────────
            // ReferenceEquals bypasses Unity's destroyed-object null: a bag destroyed by
            // vacuum/expiry must still resolve the pending intent or combat stays paused.
            if (!ReferenceEquals(_lootIntent, null) && (_lootIntent == null || !_lootIntent.IsAvailable))
                CancelLootIntent();

            Vector2 moveDir = FollowPath();

            Vector3 candidateLogical = _logical + (Vector3)(moveDir * speed * Time.fixedDeltaTime);
            if (moveDir.sqrMagnitude > 0.01f && !TryUpdateOccupancy(candidateLogical))
                moveDir = Vector2.zero;

            // _logical is authoritative; step it, then move the kinematic body to match.
            _logical += (Vector3)(moveDir * speed * Time.fixedDeltaTime);
            _rb.MovePosition(new Vector2(_logical.x, _logical.y));

            // ── Animatie & Flipping ──────────────────────────────────────────────
            bool moving = moveDir.sqrMagnitude > 0.01f;
            if (moving != _isMoving)
            {
                _isMoving = moving;
                if (_animator != null) _animator.SetFrames(_isMoving ? walkFrames : idleFrames, _isMoving ? walkFps : idleFps);
            }

            if (moveDir.x != 0f && _renderer != null)
                _renderer.flipX = moveDir.x < 0f;
        }

        private void RequestMoveTo(Vector3 worldPos)
        {
            CancelLootIntent();
            if (pathfinder == null) return;

            if (_path != null && _pathIndex < _path.Count &&
                Vector2.Distance(_path[_path.Count - 1], worldPos) < arrivalThreshold)
                return;

            // FindPath snaps an off-floor target to the nearest reachable tile; it only returns
            // empty when nothing is reachable at all. Keep following the current path in that case
            // instead of clearing it, so a click into a wall/gap can't stop an in-progress walk.
            var path = pathfinder.FindPath(_logical, worldPos);
            if (path.Count == 0) return;

            _path = path;
            _pathIndex = 0;
        }

        public void RequestMoveToWorld(Vector3 worldPos) => RequestMoveTo(worldPos);

        public void CancelPath()
        {
            CancelLootIntent();
            ClearPath();
        }

        public void SelectCombatTarget(CombatTargetView target)
        {
            if (target == null || !target.IsAvailable) return;
            CancelLootIntent();
            ClearPath();
            CombatTargetSelected?.Invoke(target);
        }

        public void SelectGatheringNode(GatheringNodeView node)
        {
            CancelLootIntent();
            ClearPath();
            GatheringNodeSelected?.Invoke(node);
        }

        public void SelectLootTarget(LootBagView lootBag)
        {
            CancelLootIntent();
            ClearPath();
            if (lootBag == null || !lootBag.IsAvailable) return;
            if (pathfinder == null)
            {
                LootTargetCancelled?.Invoke(lootBag);
                return;
            }

            List<Vector3> path = pathfinder.FindPath(_logical, lootBag.LogicalPosition);
            if (path == null || path.Count == 0)
            {
                LootTargetCancelled?.Invoke(lootBag);
                return;
            }

            _lootIntent = lootBag;
            _path = path;
            _pathIndex = 0;
            LootTargetSelected?.Invoke(lootBag);
        }

        public void CancelLootIntent()
        {
            if (ReferenceEquals(_lootIntent, null)) return;
            LootBagView cancelled = _lootIntent;
            _lootIntent = null;
            LootTargetCancelled?.Invoke(cancelled);
        }

        public static CombatTargetView FindCombatTargetAt(Vector2 worldPoint)
        {
            CombatTargetView selected = null;
            float selectedDistance = float.PositiveInfinity;
            foreach (Collider2D collider in Physics2D.OverlapPointAll(worldPoint))
            {
                CombatTargetView candidate = collider != null
                    ? collider.GetComponentInParent<CombatTargetView>()
                    : null;
                if (candidate == null || !candidate.IsAvailable) continue;
                float distance = ((Vector2)candidate.LogicalPosition - worldPoint).sqrMagnitude;
                if (selected == null || distance < selectedDistance ||
                    (Mathf.Approximately(distance, selectedDistance) &&
                     string.CompareOrdinal(candidate.EntityId, selected.EntityId) < 0))
                {
                    selected = candidate;
                    selectedDistance = distance;
                }
            }
            return selected;
        }

        public static LootBagView FindLootBagAt(Vector2 worldPoint)
        {
            LootBagView selected = null;
            float selectedDistance = float.PositiveInfinity;
            foreach (Collider2D collider in Physics2D.OverlapPointAll(worldPoint))
            {
                LootBagView candidate = collider != null
                    ? collider.GetComponentInParent<LootBagView>()
                    : null;
                if (candidate == null || !candidate.IsAvailable) continue;
                float distance = ((Vector2)candidate.LogicalPosition - worldPoint).sqrMagnitude;
                if (selected == null || distance < selectedDistance ||
                    (Mathf.Approximately(distance, selectedDistance) &&
                     string.CompareOrdinal(candidate.DropId, selected.DropId) < 0))
                {
                    selected = candidate;
                    selectedDistance = distance;
                }
            }
            return selected;
        }

        private static GatheringNodeView FindGatheringNodeAt(Vector2 worldPoint)
        {
            foreach (Collider2D collider in Physics2D.OverlapPointAll(worldPoint))
            {
                GatheringNodeView node = collider != null
                    ? collider.GetComponentInParent<GatheringNodeView>()
                    : null;
                if (node != null) return node;
            }
            return null;
        }

        private Vector2 FollowPath()
        {
            if (_path == null || _pathIndex >= _path.Count)
                return Vector2.zero;

            var waypoint = _path[_pathIndex];
            var delta = (Vector2)waypoint - (Vector2)_logical;

            if (delta.magnitude < arrivalThreshold)
            {
                _logical.z = waypoint.z;
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    LootBagView reached = _lootIntent;
                    _lootIntent = null;
                    ClearPath();
                    if (!ReferenceEquals(reached, null)) LootTargetReached?.Invoke(reached);
                    return Vector2.zero;
                }
                delta = (Vector2)_path[_pathIndex] - (Vector2)_logical;
            }

            return delta.normalized;
        }

        private void ClearPath()
        {
            _path = null;
            _pathIndex = 0;
        }

        private bool TryUpdateOccupancy(Vector3 candidatePosition)
        {
            if (pathfinder == null || !pathfinder.TryGetCell(candidatePosition, out Vector2Int candidateCell)) return true;
            if (!_hasOccupancy)
            {
                _hasOccupancy = TileOccupancy.TryOccupy(candidateCell, this);
                if (_hasOccupancy) _occupiedCell = candidateCell;
                return _hasOccupancy;
            }
            if (candidateCell == _occupiedCell) return true;
            if (!TileOccupancy.TryOccupy(candidateCell, this)) return false;
            TileOccupancy.ReleaseOccupancy(_occupiedCell, this);
            _occupiedCell = candidateCell;
            return true;
        }
    }
}
