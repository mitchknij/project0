using System.Collections;
using IdleCloud.Core;
using IdleCloud.Data;
using UnityEngine;

namespace IdleCloud.View
{
    public sealed class CombatTargetView : MonoBehaviour
    {
        [Header("Persistent Identity")]
        [Tooltip("Optional stable world-object ID. Leave blank to derive it from this object's scene hierarchy.")]
        [SerializeField] private string entityId;
        [Tooltip("Stable MonsterDef ID, for example slime.")]
        [SerializeField] private string monsterId = "world_slime";
        [Header("Respawn")]
        [Tooltip("0 = use MonsterDef.RespawnMs; > 0 overrides this placement.")]
        [SerializeField, Min(0f)] private float respawnSeconds = 0f;
        [Header("Combat Geometry")]
        [Tooltip("Authoritative circular ground footprint in Unity world units; independent from sprite/collider size.")]
        [SerializeField, Min(0f)] private float footprintRadius = 0.28f;
        [Header("Pointer Interaction")]
        [Tooltip("Click-only trigger radius. Created automatically when the authored object has no Collider2D.")]
        [SerializeField, Min(0.05f)] private float clickRadius = 0.36f;
        [SerializeField] private Vector2 clickOffset = new Vector2(0f, 0.08f);

        [Header("Health Indicator")]
        [SerializeField, Min(0.1f)] private float healthBarWidth = 0.64f;
        [SerializeField, Min(0.02f)] private float healthBarHeight = 0.08f;
        [SerializeField, Min(0f)] private float healthBarGap = 0.12f;

        private SpriteRenderer _renderer;
        private Collider2D[] _colliders;
        private EnemyController _enemy;
        private SpriteRenderer _healthBackground;
        private SpriteRenderer _healthFill;
        private float _healthBarY;
        private LineRenderer _selectionRing;

        public string EntityId => entityId;
        public string MonsterId => monsterId;
        public bool IsAvailable { get; private set; } = true;
        public float FootprintRadius => footprintRadius;
        public Vector3 LogicalPosition => _enemy != null && _enemy.HasLogicalPosition
            ? _enemy.LogicalPosition
            : transform.position;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(entityId))
                entityId = gameObject.scene.path + ":" + transform.GetHierarchyPath();
            _renderer = GetComponent<SpriteRenderer>();
            _colliders = GetComponents<Collider2D>();
            if (_colliders.Length == 0)
            {
                var clickCollider = gameObject.AddComponent<CircleCollider2D>();
                clickCollider.isTrigger = true;
                clickCollider.radius = clickRadius > 0f ? clickRadius : 0.36f;
                clickCollider.offset = clickOffset;
                _colliders = new Collider2D[] { clickCollider };
            }
            _enemy = GetComponent<EnemyController>();
            CreateHealthBar();
            CreateSelectionRing();
        }

        public void ConfigureRuntimeIdentity(string stableEntityId, string definitionId)
        {
            if (!string.IsNullOrWhiteSpace(stableEntityId)) entityId = stableEntityId;
            if (!string.IsNullOrWhiteSpace(definitionId)) monsterId = definitionId;
        }

        public void Defeat()
        {
            if (!IsAvailable) return;
            SetSelected(false);
            _enemy?.HandleDefeated();
            StartCoroutine(Respawn());
        }

        /// <summary>Called by the shared combat-result consumer after the player actually damages this actor.</summary>
        public void NotifyAttacked() => _enemy?.NotifyAttacked();

        public void SetSelected(bool selected)
        {
            if (_selectionRing != null) _selectionRing.enabled = selected && IsAvailable;
        }

        private IEnumerator Respawn()
        {
            IsAvailable = false;
            if (_renderer != null) _renderer.enabled = false;
            SetHealthVisible(false);
            foreach (Collider2D collider in _colliders) collider.enabled = false;
            if (_enemy != null) _enemy.enabled = false;

            MonsterDef monster = RuntimeContent.Monsters.TryGetValue(monsterId, out var authoredMonster) ? authoredMonster : null;
            float delaySeconds = respawnSeconds > 0f
                ? respawnSeconds
                : (monster?.RespawnMs ?? 0) / 1000f;
            yield return new WaitForSeconds(delaySeconds);

            if (_enemy != null)
            {
                _enemy.enabled = true;
                while (!_enemy.TryRespawnAtSpawn())
                    yield return new WaitForSeconds(0.25f);
            }
            foreach (Collider2D collider in _colliders) collider.enabled = true;
            if (_renderer != null) _renderer.enabled = true;
            IsAvailable = true;
            ResetHealth();
        }

        public void SetHealth(int hp)
        {
            int maxHp = RuntimeContent.Monsters.TryGetValue(monsterId, out var monster) ? monster.Hp : 1;
            float fraction = Mathf.Clamp01(hp / (float)Mathf.Max(1, maxHp));
            float fillWidth = healthBarWidth * fraction;
            _healthFill.transform.localScale = new Vector3(fillWidth, healthBarHeight, 1f);
            _healthFill.transform.localPosition = new Vector3(
                -healthBarWidth * 0.5f + fillWidth * 0.5f,
                _healthBarY,
                0f);
            SetHealthVisible(true);
        }

        private void ResetHealth()
        {
            if (_healthFill == null) return;
            _healthFill.transform.localScale = new Vector3(healthBarWidth, healthBarHeight, 1f);
            _healthFill.transform.localPosition = new Vector3(0f, _healthBarY, 0f);
            SetHealthVisible(IsAvailable);
        }

        private void CreateHealthBar()
        {
            float spriteTop = _renderer != null && _renderer.sprite != null
                ? _renderer.sprite.bounds.max.y
                : 0.5f;
            _healthBarY = spriteTop + healthBarGap;

            var background = new GameObject("CombatHealthBackground");
            background.transform.SetParent(transform, false);
            background.transform.localPosition = new Vector3(0f, _healthBarY, 0f);
            background.transform.localScale = new Vector3(
                healthBarWidth + 0.04f,
                healthBarHeight + 0.04f,
                1f);
            _healthBackground = background.AddComponent<SpriteRenderer>();
            _healthBackground.sprite = PixelSprite;
            _healthBackground.color = new Color(0.08f, 0.04f, 0.04f, 0.95f);

            var fill = new GameObject("CombatHealthFill");
            fill.transform.SetParent(transform, false);
            _healthFill = fill.AddComponent<SpriteRenderer>();
            _healthFill.sprite = PixelSprite;
            _healthFill.color = new Color(0.9f, 0.12f, 0.1f, 1f);

            // Keep both parts together with the monster's sorting layer while making the fill
            // deterministic above the frame. This also works on targets without a SortingGroup.
            if (_renderer != null)
            {
                _healthBackground.sortingLayerID = _renderer.sortingLayerID;
                _healthFill.sortingLayerID = _renderer.sortingLayerID;
                _healthBackground.sortingOrder = _renderer.sortingOrder + 1;
                _healthFill.sortingOrder = _renderer.sortingOrder + 2;
            }
            ResetHealth();
        }

        private void CreateSelectionRing()
        {
            var ringObject = new GameObject("VFX_Placeholder_TargetMarker");
            ringObject.transform.SetParent(transform, false);
            _selectionRing = ringObject.AddComponent<LineRenderer>();
            const int segments = 24;
            _selectionRing.loop = true;
            _selectionRing.positionCount = segments;
            _selectionRing.useWorldSpace = false;
            _selectionRing.startWidth = 0.025f;
            _selectionRing.endWidth = 0.025f;
            _selectionRing.material = new Material(Shader.Find("Sprites/Default"));
            _selectionRing.startColor = new Color(1f, 0.72f, 0.12f, 0.95f);
            _selectionRing.endColor = _selectionRing.startColor;
            _selectionRing.sortingOrder = 998;
            float radius = Mathf.Max(0.2f, footprintRadius + 0.1f);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                _selectionRing.SetPosition(index,
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.55f, 0f));
            }
            _selectionRing.enabled = false;
        }

        private void SetHealthVisible(bool visible)
        {
            if (_healthBackground != null) _healthBackground.enabled = visible;
            if (_healthFill != null) _healthFill.enabled = visible;
        }

        private static Sprite PixelSprite
        {
            get
            {
                if (_pixel != null) return _pixel;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply(false, true);
                // One pixel per unit makes localScale map directly to world-space bar dimensions.
                // Sprite.Create's default is 100 PPU, which made the indicator effectively invisible.
                _pixel = Sprite.Create(
                    texture,
                    new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f),
                    1f);
                return _pixel;
            }
        }

        private static Sprite _pixel;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(monsterId) && !RuntimeContent.Monsters.ContainsKey(monsterId))
                Debug.LogWarning($"[CombatTargetView] Unknown monster ID: {monsterId}", this);
        }
#endif
    }

    internal static class TransformIdentityExtensions
    {
        public static string GetHierarchyPath(this Transform transform)
        {
            var path = transform.name + "[" + transform.GetSiblingIndex() + "]";
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "[" + transform.GetSiblingIndex() + "]/" + path;
            }
            return path;
        }
    }
}
