using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace IdleCloud.View
{
    /// <summary>Runtime view for a physical ground-loot drop.</summary>
    public sealed class LootBagView : MonoBehaviour
    {
        [Header("Pointer Interaction")]
        [Tooltip("Click-only trigger radius. Created automatically when the authored object has no Collider2D.")]
        [SerializeField, Min(0.05f)] private float clickRadius = 0.36f;
        [SerializeField] private Vector2 clickOffset = new Vector2(0f, 0.08f);

        [Header("Despawn Warning")]
        [Tooltip("Seconds before despawn when the bag starts blinking. Visual only; timing remains GameManager-owned.")]
        [SerializeField, Min(0f)] private float despawnWarningSeconds = 3f;
        [SerializeField, Min(0.05f)] private float blinkIntervalSeconds = 0.18f;

        [Header("Spawn Bounce")]
        [SerializeField, Min(0.01f)] private float spawnBounceDuration = 0.24f;
        [SerializeField, Min(1f)] private float spawnBounceOvershoot = 1.15f;

        [Header("Vacuum Tween")]
        [SerializeField, Min(0.01f)] private float vacuumTweenDuration = 0.28f;
        [SerializeField, Range(0f, 1f)] private float vacuumEndScale = 0.35f;

        [Header("Events")]
        public UnityEvent OnSpawned;
        public UnityEvent OnPickedUp;

        private Collider2D[] _colliders;
        private SpriteRenderer[] _renderers;
        private Transform[] _visualTransforms = new Transform[0];
        private Vector3[] _visualBaseScales = new Vector3[0];
        private bool _scalesRoot;
        private CircleCollider2D[] _circleColliders = new CircleCollider2D[0];
        private float[] _circleBaseRadii = new float[0];
        private Vector2[] _circleBaseOffsets = new Vector2[0];
        private Vector3 _logicalPosition;
        private long _despawnAtMs;
        private bool _hasDespawnAt;
        private float _blinkElapsed;
        private bool _blinkVisible = true;
        private Coroutine _visualAnimation;

        public string DropId { get; private set; }
        public Vector3 LogicalPosition => _logicalPosition;
        public bool IsAvailable { get; private set; } = true;

        private void Awake()
        {
            _colliders = GetComponents<Collider2D>();
            if (_colliders.Length == 0)
            {
                var clickCollider = gameObject.AddComponent<CircleCollider2D>();
                clickCollider.isTrigger = true;
                clickCollider.radius = clickRadius > 0f ? clickRadius : 0.36f;
                clickCollider.offset = clickOffset;
                _colliders = new Collider2D[] { clickCollider };
            }
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            var visualTransforms = new List<Transform>();
            var visualBaseScales = new List<Vector3>();
            foreach (SpriteRenderer renderer in _renderers)
            {
                if (renderer == null || renderer.transform == transform) continue;
                visualTransforms.Add(renderer.transform);
                visualBaseScales.Add(renderer.transform.localScale);
            }
            // The authored LootBag prefab keeps its SpriteRenderer on the root object, so
            // fall back to scaling the root when no child visuals exist. Circle colliders
            // are counter-scaled in SetVisualScale so the world-space click area stays
            // constant while the visuals animate (vacuum flight disables colliders anyway).
            if (visualTransforms.Count == 0 && _renderers.Length > 0)
            {
                visualTransforms.Add(transform);
                visualBaseScales.Add(transform.localScale);
                _scalesRoot = true;
                var circles = new List<CircleCollider2D>();
                var radii = new List<float>();
                var offsets = new List<Vector2>();
                foreach (Collider2D collider in _colliders)
                {
                    if (!(collider is CircleCollider2D circle)) continue;
                    circles.Add(circle);
                    radii.Add(circle.radius);
                    offsets.Add(circle.offset);
                }
                _circleColliders = circles.ToArray();
                _circleBaseRadii = radii.ToArray();
                _circleBaseOffsets = offsets.ToArray();
            }
            _visualTransforms = visualTransforms.ToArray();
            _visualBaseScales = visualBaseScales.ToArray();
        }

        public void ConfigureRuntimeDrop(string dropId, Vector3 logicalPosition, long spawnedAtMs, long despawnMs)
        {
            if (_visualAnimation != null) StopCoroutine(_visualAnimation);
            RestoreVisualScales();
            DropId = dropId;
            _logicalPosition = logicalPosition;
            _despawnAtMs = spawnedAtMs + (despawnMs < 0L ? 0L : despawnMs);
            _hasDespawnAt = despawnMs > 0L;
            _blinkElapsed = 0f;
            _blinkVisible = true;
            IsAvailable = true;
            SetVisible(true);
            SetVisualScale(0f);
            _visualAnimation = StartCoroutine(AnimateSpawnBounce());
            OnSpawned?.Invoke();
        }

        public void FlyToWorldPosition(Vector3 targetPosition)
        {
            if (_visualAnimation != null) StopCoroutine(_visualAnimation);
            RestoreVisualScales();
            _visualAnimation = StartCoroutine(AnimateVacuumFlight(targetPosition));
        }

        public void NotifyPickedUp() => NotifyPickedUp(false);

        public void NotifyPickedUp(bool hasRemainingStacks)
        {
            OnPickedUp?.Invoke();
            if (hasRemainingStacks) return;
            if (!IsAvailable) return;
            IsAvailable = false;
            foreach (Collider2D collider in _colliders)
                if (collider != null) collider.enabled = false;
        }

        public void NotifyExpired()
        {
            IsAvailable = false;
            foreach (Collider2D collider in _colliders)
                if (collider != null) collider.enabled = false;
            SetVisible(false);
        }

        private void Update()
        {
            if (!IsAvailable || !_hasDespawnAt || despawnWarningSeconds <= 0f) return;

            long nowMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long warningMs = (long)(despawnWarningSeconds * 1000f);
            if (_despawnAtMs - nowMs > warningMs) return;

            _blinkElapsed += Time.deltaTime;
            if (_blinkElapsed < blinkIntervalSeconds) return;
            _blinkElapsed = 0f;
            _blinkVisible = !_blinkVisible;
            SetVisible(_blinkVisible);
        }

        private void SetVisible(bool visible)
        {
            if (_renderers == null) return;
            foreach (SpriteRenderer renderer in _renderers)
                if (renderer != null) renderer.enabled = visible;
        }

        private IEnumerator AnimateSpawnBounce()
        {
            float duration = Mathf.Max(0.01f, spawnBounceDuration);
            float elapsed = 0f;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float scale = progress < 0.65f
                    ? Mathf.Lerp(0f, spawnBounceOvershoot, progress / 0.65f)
                    : Mathf.Lerp(spawnBounceOvershoot, 1f, (progress - 0.65f) / 0.35f);
                SetVisualScale(scale);
                yield return null;
            }
            RestoreVisualScales();
            _visualAnimation = null;
        }

        private IEnumerator AnimateVacuumFlight(Vector3 targetPosition)
        {
            Vector3 startPosition = transform.position;
            float duration = Mathf.Max(0.01f, vacuumTweenDuration);
            float elapsed = 0f;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = progress * progress;
                transform.position = Vector3.Lerp(startPosition, targetPosition, easedProgress);
                SetVisualScale(Mathf.Lerp(1f, vacuumEndScale, easedProgress));
                yield return null;
            }
            if (this == null) yield break;
            transform.position = targetPosition;
            Destroy(gameObject);
        }

        private void SetVisualScale(float factor)
        {
            for (int index = 0; index < _visualTransforms.Length; index++)
            {
                Transform visual = _visualTransforms[index];
                if (visual != null) visual.localScale = _visualBaseScales[index] * factor;
            }
            if (!_scalesRoot) return;
            float divisor = Mathf.Max(factor, 0.001f);
            for (int index = 0; index < _circleColliders.Length; index++)
            {
                CircleCollider2D circle = _circleColliders[index];
                if (circle == null) continue;
                circle.radius = _circleBaseRadii[index] / divisor;
                circle.offset = _circleBaseOffsets[index] / divisor;
            }
        }

        private void RestoreVisualScales() => SetVisualScale(1f);
    }
}
