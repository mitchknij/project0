using System.Collections;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using UnityEngine;

namespace IdleCloud.View
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class GatheringNodeView : MonoBehaviour
    {
        [Header("Persistent Identity")]
        [Tooltip("Optional stable world-object ID. Leave blank to derive it from this object's scene hierarchy.")]
        [SerializeField] private string entityId;
        [Tooltip("Stable ResourceNodeDef ID, for example oak_tree, copper_vein, or wildflower_patch.")]
        [SerializeField] private string nodeId;
        [Header("Progress Indicator")]
        [SerializeField, Min(0.1f)] private float progressBarWidth = 0.64f;
        [SerializeField, Min(0.02f)] private float progressBarHeight = 0.08f;
        [SerializeField, Min(0f)] private float progressBarGap = 0.12f;
        [SerializeField] private Color progressBarBackgroundColor = new Color(0.08f, 0.04f, 0.04f, 0.95f);
        [SerializeField] private Color progressBarFillColor = new Color(0.95f, 0.58f, 0.08f, 1f);
        [Header("Gathering Feedback")]
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.12f;
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.05f;
        [SerializeField, Min(1)] private int shakeOscillations = 3;
        [SerializeField, Min(0)] private int crumblePuffCount = 4;
        [SerializeField] private Color crumblePuffColor = new Color(0.72f, 0.48f, 0.2f, 0.9f);
        [SerializeField, Min(0.05f)] private float crumblePuffDuration = 0.45f;
        [SerializeField, Min(0f)] private float crumblePuffScatter = 0.18f;
        [SerializeField, Min(0f)] private float crumblePuffFallSpeed = 0.35f;
        [SerializeField, Min(0.01f)] private float crumblePuffScale = 0.08f;
        [SerializeField] private int crumblePuffSortingOrderOffset = 3;

        private SpriteRenderer _renderer;
        private SpriteRenderer _progressBackground;
        private SpriteRenderer _progressFill;
        private float _progressBarY;
        private Coroutine _shakeRoutine;
        private Vector3 _shakeOriginalLocalPosition;

        public string EntityId => entityId;
        public string NodeId => nodeId;
        public bool IsAvailable => gameObject.activeInHierarchy;

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (string.IsNullOrWhiteSpace(entityId))
                entityId = gameObject.scene.path + ":" + transform.GetHierarchyPath();
            CreateProgressBar();
        }

        public void Configure(string stableEntityId, string definitionId)
        {
            entityId = stableEntityId;
            nodeId = definitionId;
            RuntimeContent.Nodes.TryGetValue(nodeId, out ResourceNodeDef node);
            if (_renderer == null || node == null) return;
        }

        public void Select()
        {
            RuntimeContent.Nodes.TryGetValue(nodeId, out ResourceNodeDef node);
            if (node == null) return;
            GameManager.Instance?.Assign(ActivitySkillMapping.ToActivityKind(node.Skill), node.Id);
        }

        public void SetProgress(double progress01)
        {
            if (_progressFill == null) return;
            float fraction = Mathf.Clamp01((float)progress01);
            float fillWidth = progressBarWidth * fraction;
            _progressFill.transform.localScale = new Vector3(fillWidth, progressBarHeight, 1f);
            _progressFill.transform.localPosition = new Vector3(
                -progressBarWidth * 0.5f + fillWidth * 0.5f,
                _progressBarY,
                0f);
            SetProgressVisible(true);
        }

        public void HideProgress() => SetProgressVisible(false);

        public void PlayHitShake()
        {
            if (_renderer == null) return;
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _renderer.transform.localPosition = _shakeOriginalLocalPosition;
                _shakeRoutine = null;
            }
            _shakeOriginalLocalPosition = _renderer.transform.localPosition;
            _shakeRoutine = StartCoroutine(AnimateHitShake());
        }

        public void PlayCrumblePuff()
        {
            if (_renderer == null || crumblePuffCount <= 0) return;
            for (int index = 0; index < crumblePuffCount; index++)
            {
                var puff = new GameObject("VFX_Placeholder_GatheringPuff");
                puff.transform.position = _renderer.transform.position;
                puff.transform.localScale = Vector3.one * crumblePuffScale;
                var puffRenderer = puff.AddComponent<SpriteRenderer>();
                puffRenderer.sprite = ProgressPixelSprite;
                puffRenderer.color = crumblePuffColor;
                puffRenderer.sortingLayerID = _renderer.sortingLayerID;
                puffRenderer.sortingOrder = _renderer.sortingOrder + crumblePuffSortingOrderOffset;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(0.5f, 1f) * crumblePuffScatter;
                Vector3 displacement = new Vector3(
                    Mathf.Cos(angle) * distance,
                    Random.Range(0.5f, 1f) * crumblePuffScatter,
                    0f);
                StartCoroutine(AnimateCrumblePuff(puff, puffRenderer, displacement));
            }
        }

        private IEnumerator AnimateHitShake()
        {
            float duration = Mathf.Max(0.01f, shakeDuration);
            float elapsed = 0f;
            while (elapsed < duration && _renderer != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float envelope = 1f - progress;
                float oscillation = Mathf.Sin(
                    progress * Mathf.PI * 2f * Mathf.Max(1, shakeOscillations));
                _renderer.transform.localPosition = _shakeOriginalLocalPosition +
                    new Vector3(oscillation * shakeAmplitude * envelope, 0f, 0f);
                yield return null;
            }
            if (_renderer != null)
                _renderer.transform.localPosition = _shakeOriginalLocalPosition;
            _shakeRoutine = null;
        }

        private IEnumerator AnimateCrumblePuff(
            GameObject puff,
            SpriteRenderer puffRenderer,
            Vector3 displacement)
        {
            float duration = Mathf.Max(0.05f, crumblePuffDuration);
            float elapsed = 0f;
            Color color = puffRenderer.color;
            float baseAlpha = color.a;
            Vector3 start = puff.transform.position;
            while (elapsed < duration && puff != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                puff.transform.position = start + displacement * progress +
                    Vector3.down * (crumblePuffFallSpeed * progress * progress);
                color.a = baseAlpha * (1f - progress);
                puffRenderer.color = color;
                yield return null;
            }
            if (puff != null) Destroy(puff);
        }

        private void CreateProgressBar()
        {
            // World-space renderer bounds relative to this root: the renderer may sit on an
            // offset "Visual" child (Tree_Base profile), which sprite-local bounds would miss.
            float spriteTop = _renderer != null && _renderer.sprite != null
                ? _renderer.bounds.max.y - transform.position.y
                : 0.5f;
            _progressBarY = spriteTop + progressBarGap;

            var background = new GameObject("GatheringProgressBackground");
            background.transform.SetParent(transform, false);
            background.transform.localPosition = new Vector3(0f, _progressBarY, 0f);
            background.transform.localScale = new Vector3(
                progressBarWidth + 0.04f,
                progressBarHeight + 0.04f,
                1f);
            _progressBackground = background.AddComponent<SpriteRenderer>();
            _progressBackground.sprite = ProgressPixelSprite;
            _progressBackground.color = progressBarBackgroundColor;

            var fill = new GameObject("GatheringProgressFill");
            fill.transform.SetParent(transform, false);
            _progressFill = fill.AddComponent<SpriteRenderer>();
            _progressFill.sprite = ProgressPixelSprite;
            _progressFill.color = progressBarFillColor;

            if (_renderer != null)
            {
                _progressBackground.sortingLayerID = _renderer.sortingLayerID;
                _progressFill.sortingLayerID = _renderer.sortingLayerID;
                _progressBackground.sortingOrder = _renderer.sortingOrder + 1;
                _progressFill.sortingOrder = _renderer.sortingOrder + 2;
            }
            HideProgress();
        }

        private void SetProgressVisible(bool visible)
        {
            if (_progressBackground != null) _progressBackground.enabled = visible;
            if (_progressFill != null) _progressFill.enabled = visible;
        }

        private static Sprite ProgressPixelSprite
        {
            get
            {
                if (_progressPixel != null) return _progressPixel;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply(false, true);
                _progressPixel = Sprite.Create(
                    texture,
                    new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f),
                    1f);
                return _progressPixel;
            }
        }

        private static Sprite _progressPixel;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(nodeId) && !RuntimeContent.Nodes.ContainsKey(nodeId))
                Debug.LogWarning($"[GatheringNodeView] Unknown resource node ID: {nodeId}", this);
        }
#endif

    }
}
