using UnityEngine;

namespace Iso.Sorting
{
    /// Reproduces JohnBrx's projected sprite-shadow baseline: copies the owner's current sprite frame
    /// into a child renderer, tints it dark/translucent, tilts it 45 degrees on X, and orients/scales it
    /// away from a rotating 2D "sun" (SunOrbitController.ShadowDirection), matching the transcript.
    ///
    /// This is a visual child only. It does not participate in pathfinding, walkability, tile occupancy,
    /// floor selection, or world occlusion, and it never recomputes the isometric sort key: sorting is
    /// derived from the owner's already-resolved SpriteRenderer.sortingOrder (see PlayerSortController /
    /// IsoTerrainSortCalculator), offset by a small, configurable, owner-relative amount that stays well
    /// under IsoSortSettings.sortScale so it can never cross into a neighbouring cell's band.
    ///
    /// Uses a single shared, cached, non-instanced material (no per-frame or per-instance Material
    /// creation) and colours each instance via SpriteRenderer.color, which is per-renderer state, not a
    /// material property — no MaterialPropertyBlock needed for this simple case.
    [DisallowMultipleComponent]
    public sealed class ProjectedSpriteShadow : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The owner's visible SpriteRenderer (e.g. the Player's \"Visual\" child). Auto-filled from a child on Reset/Awake if left empty.")]
        [SerializeField] private SpriteRenderer source;

        [Header("Light direction")]
        [Tooltip("Drives the shadow's projection direction via ShadowDirection (points away from the light), matching JohnBrx's rotating daytime Sprite Light. Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private SunOrbitController sun;

        [Tooltip("IdleCloud extension, not part of JohnBrx's literal method: when true (or when no sun is assigned), use fixedDirection instead of the sun's live ShadowDirection.")]
        [SerializeField] private bool useFixedDirection = false;

        [Tooltip("Direction used when useFixedDirection is true or no sun is assigned. Does not need to be normalized.")]
        [SerializeField] private Vector2 fixedDirection = Vector2.down;

        [Header("Sorting")]
        [Tooltip("Added to source.sortingOrder to get the shadow's sortingOrder. Keep well under IsoSortSettings.sortScale (10 in this project) so it never crosses into a neighbouring cell's band. Negative draws the shadow behind the owner.")]
        [SerializeField] private int sortingOrderOffset = -1;

        [Header("Appearance")]
        [Tooltip("Shadow tint and opacity (alpha). Per the plan's recommended default: dark, ~25-40% opaque.")]
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.35f);

        [Tooltip("Rotation of the shadow sprite around the X axis, in degrees. The transcript specifies 45.")]
        [SerializeField] private float tiltAngleX = 45f;

        [Tooltip("Shadow length multiplier along the projection direction.")]
        [SerializeField] private float length = 1f;

        [Tooltip("Horizontal shear applied toward the projection direction, in local units.")]
        [SerializeField] private float skew = 0.5f;

        [Tooltip("Vertical scale multiplier (compression < 1, extension > 1) applied to the shadow sprite.")]
        [SerializeField] private float verticalCompression = 0.6f;

        [Tooltip("Local offset from the owner's origin to the ground contact point. Player sprites are sliced bottom-center (pivot 0.5, 0), so the feet already sit at local (0, 0) and this can stay zero.")]
        [SerializeField] private Vector2 footAnchorOffset = Vector2.zero;

        [Header("Debug")]
        [Tooltip("Editor-only: draw the foot anchor and projection direction as gizmos, and expose live diagnostic fields below. Compiled out of builds.")]
        [SerializeField] private bool debug = false;

#if UNITY_EDITOR
        [SerializeField] private string debugSourceSpriteName;
        [SerializeField] private int debugSourceSortingOrder;
        [SerializeField] private int debugShadowSortingOrder;
        [SerializeField] private Vector2 debugDirection;
        [SerializeField] private bool debugSpritesMatch;
#endif

        // Single shared material for every instance in the project: created once, never per-frame,
        // never per-instance. Sprites/Default is an unlit URP-compatible sprite shader that samples the
        // sprite's own alpha, which is exactly what the transcript's silhouette shadow needs.
        private static Material _sharedShadowMaterial;

        private SpriteRenderer _shadowRenderer;
        private Transform _shadowTransform;

        private static Material SharedShadowMaterial
        {
            get
            {
                if (_sharedShadowMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    _sharedShadowMaterial = new Material(shader) { name = "ProjectedSpriteShadow (Shared)" };
                }
                return _sharedShadowMaterial;
            }
        }

        private void Reset()
        {
            if (source == null) source = GetComponentInChildren<SpriteRenderer>();
            if (sun == null) sun = FindFirstObjectByType<SunOrbitController>();
        }

        private void Awake()
        {
            if (source == null) source = GetComponentInChildren<SpriteRenderer>();
            if (sun == null) sun = FindFirstObjectByType<SunOrbitController>();
            EnsureShadowRenderer();
        }

        private void EnsureShadowRenderer()
        {
            if (_shadowRenderer != null) return;

            Transform existing = transform.Find("ProjectedShadow");
            GameObject shadowGo = existing != null ? existing.gameObject : new GameObject("ProjectedShadow");
            shadowGo.transform.SetParent(transform, worldPositionStays: false);

            _shadowTransform = shadowGo.transform;
            _shadowRenderer = shadowGo.GetComponent<SpriteRenderer>();
            if (_shadowRenderer == null) _shadowRenderer = shadowGo.AddComponent<SpriteRenderer>();

            _shadowRenderer.sharedMaterial = SharedShadowMaterial;
        }

        private void LateUpdate()
        {
            if (source == null || source.sprite == null)
            {
                if (_shadowRenderer != null) _shadowRenderer.enabled = false;
                return;
            }

            EnsureShadowRenderer();
            _shadowRenderer.enabled = true;

            // Mirror the owner's current frame and flip state.
            _shadowRenderer.sprite = source.sprite;
            _shadowRenderer.flipX = source.flipX;
            _shadowRenderer.flipY = source.flipY;
            _shadowRenderer.color = shadowColor;

            // Owner-relative sorting: never recompute the iso sort key, just ride the owner's resolved
            // order. See PlayerSortController / IsoTerrainSortCalculator for how source.sortingOrder
            // (or, for a SortingGroup owner, the group order the source inherits) was derived.
            _shadowRenderer.sortingLayerID = source.sortingLayerID;
            int shadowOrder = source.sortingOrder + sortingOrderOffset;
            _shadowRenderer.sortingOrder = shadowOrder;

            Vector2 direction = ResolveDirection();

            // Position at the ground contact point.
            _shadowTransform.localPosition = new Vector3(footAnchorOffset.x, footAnchorOffset.y, 0f);

            // Orient toward the projection direction, then tilt 45 degrees on X per the transcript.
            float yawDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _shadowTransform.localRotation = Quaternion.Euler(tiltAngleX, 0f, yawDegrees);

            // Shape via transform only: shear toward the direction, scale for length/compression.
            _shadowTransform.localScale = new Vector3(1f + skew * direction.magnitude, verticalCompression, 1f) * length;

#if UNITY_EDITOR
            if (debug)
            {
                debugSourceSpriteName = source.sprite.name;
                debugSourceSortingOrder = source.sortingOrder;
                debugShadowSortingOrder = shadowOrder;
                debugDirection = direction;
                debugSpritesMatch = _shadowRenderer.sprite == source.sprite;
            }
#endif
        }

        private Vector2 ResolveDirection()
        {
            if (!useFixedDirection && sun != null) return sun.ShadowDirection;
            return fixedDirection.sqrMagnitude > 0.0001f ? fixedDirection.normalized : Vector2.down;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!debug) return;

            Vector3 anchor = transform.position + new Vector3(footAnchorOffset.x, footAnchorOffset.y, 0f);
            Gizmos.color = new Color(0f, 0f, 0f, 0.8f);
            Gizmos.DrawWireSphere(anchor, 0.05f);

            Vector2 direction = ResolveDirection();
            Gizmos.color = new Color(0.6f, 0.2f, 0.8f, 0.9f);
            Gizmos.DrawLine(anchor, anchor + (Vector3)(direction * length));
        }
#endif
    }
}
