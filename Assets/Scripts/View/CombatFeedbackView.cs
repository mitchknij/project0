using System.Collections;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using TMPro;
using UnityEngine;

namespace IdleCloud.View
{
    public sealed class CombatFeedbackView : MonoBehaviour
    {
        [SerializeField] private WorldMapContext mapContext;
        [SerializeField] private PlayerController player;
        [Header("Tile Area Placeholder")]
        [Tooltip("Authoritative conversion seam used by CombatSpatialAdapter. Assign the same GridPathfinder used by CombatView.")]
        [SerializeField] private GridPathfinder pathfinder;
        [Tooltip("Base tint for the short-lived tile-resolution placeholder.")]
        [SerializeField] private Color tileOverlayTint = new Color(0.18f, 0.72f, 1f, 0.8f);
        [Tooltip("How long each tile-resolution placeholder remains visible, in seconds.")]
        [SerializeField, Min(0f)] private float tileOverlayDuration = 0.35f;
        [Tooltip("Use the optional element color instead of the base tile tint.")]
        [SerializeField] private bool useElementColor;
        [Tooltip("Optional element-specific placeholder color.")]
        [SerializeField] private Color elementColor = new Color(1f, 0.45f, 0.16f, 0.8f);
        [Tooltip("World-space offset applied after GridPathfinder converts the logical tile.")]
        [SerializeField] private Vector3 tileOverlayRenderOffset;
        [Tooltip("World-space width and height of the placeholder diamond.")]
        [SerializeField] private Vector2 tileOverlaySize = new Vector2(0.64f, 0.32f);
        [Tooltip("Line width used by the placeholder diamond.")]
        [SerializeField, Min(0f)] private float tileOverlayLineWidth = 0.035f;
        [Tooltip("Sorting order for the placeholder overlay.")]
        [SerializeField] private int tileOverlaySortingOrder = 997;
        [Header("Loot Popup")]
        [SerializeField] private Vector3 lootPopupOffset = new Vector3(0f, 0.85f, 0f);
        [SerializeField, Min(0.05f)] private float lootPopupDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float lootPopupFontSize = 2.4f;
        [SerializeField] private Color lootPopupColor = new Color(0.72f, 0.92f, 0.72f, 0.9f);
        [SerializeField, Min(0f)] private float lootPopupRiseSpeed = 0.3f;
        [Header("Miss Popup (combat & gathering)")]
        [SerializeField] private Color missPopupColor = new Color(0.95f, 0.5f, 0.5f, 0.75f);
        [SerializeField, Min(0.1f)] private float missPopupFontSize = 2f;
        [Header("Coin Popup")]
        [SerializeField] private Color coinPopupColor = new Color(1f, 0.82f, 0.2f, 1f);
        [SerializeField, Min(0.1f)] private float coinPopupFontSize = 2.6f;
        [Header("Critical Hit Popup")]
        [SerializeField] private Color critPopupColor = new Color(1f, 0.45f, 0.08f, 1f);
        [SerializeField, Min(0.1f)] private float critPopupFontSize = 5.2f;
        [SerializeField, Min(1f)] private float critPunchScale = 1.35f;
        [SerializeField, Min(0.01f)] private float critPunchDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float combatPopupDuration = 0.7f;
        [SerializeField] private float combatPopupRiseSpeed = 0.45f;
        private readonly Dictionary<string, CombatTargetView> _targets = new Dictionary<string, CombatTargetView>();
        private readonly Dictionary<long, GameObject> _projectiles = new Dictionary<long, GameObject>();
        private readonly Dictionary<string, GameObject> _statusIndicators = new Dictionary<string, GameObject>();
        private readonly List<GameObject> _tileOverlays = new List<GameObject>();
        private Material _tileOverlayMaterial;
        private bool _subscribed;
        private bool _lootSubscribed;

        public void Configure(IEnumerable<CombatTargetView> targets)
        {
            _targets.Clear();
            foreach (CombatTargetView target in targets)
                if (target != null) _targets[target.EntityId] = target;
        }

        private void Start()
        {
            if (mapContext == null)
            {
                Debug.LogWarning("[CombatFeedbackView] Assign World Map Context in the Inspector.", this);
                return;
            }
            Configure(mapContext.CombatTargets);
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (pathfinder == null) pathfinder = FindFirstObjectByType<GridPathfinder>();
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (!_subscribed)
            {
                GameManager.Instance.ActiveCombatResolved += HandleCombatResult;
                _subscribed = true;
            }
            if (!_lootSubscribed)
            {
                GameManager.Instance.LootPickedUp += HandleLootPickedUp;
                GameManager.Instance.ActiveGatheringResolved += HandleGatheringResolved;
                GameManager.Instance.CraftCompleted += HandleCraftCompleted;
                _lootSubscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && GameManager.Instance != null)
                GameManager.Instance.ActiveCombatResolved -= HandleCombatResult;
            if (_lootSubscribed && GameManager.Instance != null)
            {
                GameManager.Instance.LootPickedUp -= HandleLootPickedUp;
                GameManager.Instance.ActiveGatheringResolved -= HandleGatheringResolved;
                GameManager.Instance.CraftCompleted -= HandleCraftCompleted;
            }
            foreach (GameObject projectile in _projectiles.Values)
                if (projectile != null) Destroy(projectile);
            _projectiles.Clear();
            foreach (GameObject indicator in _statusIndicators.Values)
                if (indicator != null) Destroy(indicator);
            _statusIndicators.Clear();
            foreach (GameObject overlay in _tileOverlays)
                if (overlay != null) Destroy(overlay);
            _tileOverlays.Clear();
            if (_tileOverlayMaterial != null) Destroy(_tileOverlayMaterial);
        }

        private void HandleLootPickedUp(LootPickedUpEvent payload)
        {
            if (player == null || payload?.PickedStacks == null) return;
            foreach (ItemStack stack in payload.PickedStacks)
            {
                if (stack == null || stack.Qty <= 0) continue;
                string itemName = stack.ItemId;
                if (RuntimeContent.Items.TryGetValue(stack.ItemId, out ItemDef item) && item != null &&
                    !string.IsNullOrWhiteSpace(item.Name))
                    itemName = item.Name;
                SpawnLootPopup(player.LogicalPosition, $"+{stack.Qty} {itemName}");
            }
        }

        private void HandleCraftCompleted(ItemStack output)
        {
            if (player == null || output == null || output.Qty <= 0) return;
            string itemName = output.ItemId;
            if (RuntimeContent.Items.TryGetValue(output.ItemId, out ItemDef item) && item != null &&
                !string.IsNullOrWhiteSpace(item.Name))
                itemName = item.Name;
            SpawnLootPopup(player.LogicalPosition, $"+{output.Qty} {itemName}");
        }

        private void HandleGatheringResolved(ActiveGatheringTickResult result)
        {
            if (player == null || result?.Simulation == null) return;
            foreach (ItemStack stack in result.Simulation.Loot ?? new List<ItemStack>())
            {
                if (stack == null || stack.Qty <= 0) continue;
                string itemName = stack.ItemId;
                if (RuntimeContent.Items.TryGetValue(stack.ItemId, out ItemDef item) && item != null &&
                    !string.IsNullOrWhiteSpace(item.Name))
                    itemName = item.Name;
                SpawnLootPopup(player.LogicalPosition, $"+{stack.Qty} {itemName}");
            }

            int misses = 0;
            foreach (GatheringEvent gatherEvent in result.Simulation.Events ?? new List<GatheringEvent>())
                if (gatherEvent != null && gatherEvent.Kind == GatheringEventKind.AttemptResolved && !gatherEvent.Success)
                    misses++;
            // One popup per tick even for catch-up batches — stacked identical popups just smear.
            if (misses > 0)
                SpawnFloatingText(player.LogicalPosition, misses == 1 ? "Miss" : $"Miss x{misses}",
                    missPopupColor, missPopupFontSize);
        }

        private void HandleCombatResult(ActiveCombatTickResult result)
        {
            if (result == null) return;
            if (result.Reward?.KillLoot != null)
            {
                foreach (KillLootRecord killLoot in result.Reward.KillLoot)
                {
                    if (killLoot == null || killLoot.Coins <= 0) continue;
                    Vector3 popupPosition = player != null ? player.LogicalPosition : Vector3.zero;
                    if (_targets.TryGetValue(killLoot.ActorEntityId, out CombatTargetView defeatedTarget) &&
                        defeatedTarget != null)
                        popupPosition = defeatedTarget.transform.position;
                    SpawnFloatingText(popupPosition, $"+{killLoot.Coins} coins",
                        coinPopupColor, coinPopupFontSize);
                }
            }
            if (result.Simulation?.Events == null) return;
            foreach (CombatEvent combatEvent in result.Simulation.Events)
            {
                if (combatEvent.Kind == CombatEventKind.CombatEffectScheduled)
                    SpawnScheduledProjectile(combatEvent);
                if (combatEvent.Kind == CombatEventKind.SkillExecuted ||
                    combatEvent.Kind == CombatEventKind.CombatEffectCancelled)
                    CompleteScheduledProjectile(combatEvent.ActionSequenceId);
                if (combatEvent.Kind == CombatEventKind.StatusApplied)
                    ShowStatusIndicator(combatEvent.TargetId, combatEvent.SkillId);
                if (combatEvent.Kind == CombatEventKind.StatusTicked &&
                    _targets.TryGetValue(combatEvent.TargetId, out CombatTargetView statusTarget))
                    SpawnImpactPulse(statusTarget.transform.position, combatEvent.SkillId + "_tick");
                if (combatEvent.Kind == CombatEventKind.StatusExpired)
                    HideStatusIndicator(combatEvent.TargetId, combatEvent.SkillId);
                if ((combatEvent.Kind == CombatEventKind.TransientModifierApplied ||
                     combatEvent.Kind == CombatEventKind.TransientModifierExpired) && player != null)
                    SpawnImpactPulse(player.LogicalPosition, combatEvent.SkillId);
                if (combatEvent.Kind == CombatEventKind.AreaResolved && combatEvent.Radius > 0.0)
                    SpawnGroundRing(
                        new Vector3((float)combatEvent.PositionX, (float)combatEvent.PositionY, 0f),
                        (float)combatEvent.Radius);
                if (combatEvent.Kind == CombatEventKind.TileAreaResolved)
                    SpawnTileOverlays(combatEvent);
                if (combatEvent.Kind == CombatEventKind.EnemyKilled &&
                    _targets.TryGetValue(combatEvent.TargetId, out CombatTargetView defeatedTarget))
                    defeatedTarget.Defeat();
                if (combatEvent.Kind == CombatEventKind.SkillExecuted &&
                    _targets.TryGetValue(combatEvent.TargetId, out CombatTargetView skillTarget))
                    SpawnImpactPulse(skillTarget.transform.position, combatEvent.SkillId);
                if ((combatEvent.Kind == CombatEventKind.AttackResolved ||
                     combatEvent.Kind == CombatEventKind.SkillExecuted) && !combatEvent.Hit)
                {
                    string missPlayerId = GameManager.Instance?.GetSelectedCharacter()?.Id;
                    if (player != null && combatEvent.TargetId == missPlayerId)
                        SpawnFloatingText(player.LogicalPosition, "Miss", missPopupColor, missPopupFontSize);
                    else if (_targets.TryGetValue(combatEvent.TargetId, out CombatTargetView missTarget))
                        SpawnFloatingText(missTarget.transform.position, "Miss", missPopupColor, missPopupFontSize);
                }
                if (combatEvent.Kind != CombatEventKind.DamageApplied || combatEvent.Amount <= 0) continue;
                string playerActorId = GameManager.Instance?.GetSelectedCharacter()?.Id;
                if (player != null && combatEvent.TargetId == playerActorId)
                {
                    SpawnImpactPulse(player.LogicalPosition, "mob_attack");
                    SpawnPopup(player.LogicalPosition, combatEvent.Amount, false);
                    continue;
                }
                if (_targets.TryGetValue(combatEvent.TargetId, out CombatTargetView target))
                {
                    int hp = result.Simulation.State.EnemyHpByActorId != null &&
                        result.Simulation.State.EnemyHpByActorId.TryGetValue(combatEvent.TargetId, out int actorHp)
                        ? actorHp
                        : result.Simulation.State.EnemyHp;
                    target.SetHealth(hp);
                    SpawnPopup(target.transform.position, combatEvent.Amount, combatEvent.Critical);
                }
            }
        }

        private void SpawnTileOverlays(CombatEvent combatEvent)
        {
            if (pathfinder == null) pathfinder = FindFirstObjectByType<GridPathfinder>();
            if (pathfinder == null || combatEvent.AffectedTiles == null || combatEvent.AffectedTiles.Count == 0)
                return;

            Color color = useElementColor ? elementColor : tileOverlayTint;
            foreach (CombatTileCoordinate tile in combatEvent.AffectedTiles)
            {
                if (!pathfinder.TryGetTileWorldPosition(tile, combatEvent.Floor, out Vector3 worldPosition))
                    continue;

                var overlay = new GameObject("VFX_Placeholder_TileArea_" + tile.X + "_" + tile.Y);
                overlay.transform.position = worldPosition + tileOverlayRenderOffset;
                var line = overlay.AddComponent<LineRenderer>();
                line.loop = true;
                line.positionCount = 4;
                line.useWorldSpace = false;
                if (_tileOverlayMaterial == null)
                    _tileOverlayMaterial = new Material(Shader.Find("Sprites/Default"));
                line.sharedMaterial = _tileOverlayMaterial;
                line.startWidth = tileOverlayLineWidth;
                line.endWidth = tileOverlayLineWidth;
                line.startColor = color;
                line.endColor = color;
                line.sortingOrder = tileOverlaySortingOrder;
                Vector3 halfSize = new Vector3(tileOverlaySize.x * 0.5f, tileOverlaySize.y * 0.5f, 0f);
                line.SetPosition(0, new Vector3(0f, halfSize.y, 0f));
                line.SetPosition(1, new Vector3(halfSize.x, 0f, 0f));
                line.SetPosition(2, new Vector3(0f, -halfSize.y, 0f));
                line.SetPosition(3, new Vector3(-halfSize.x, 0f, 0f));
                _tileOverlays.Add(overlay);
                StartCoroutine(FadeTileOverlay(overlay, line, color));
            }
        }

        private IEnumerator FadeTileOverlay(GameObject overlay, LineRenderer line, Color baseColor)
        {
            float elapsed = 0f;
            while (elapsed < tileOverlayDuration && overlay != null)
            {
                elapsed += Time.deltaTime;
                float progress = tileOverlayDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / tileOverlayDuration);
                Color color = baseColor;
                color.a = baseColor.a * (1f - progress);
                line.startColor = color;
                line.endColor = color;
                yield return null;
            }
            _tileOverlays.Remove(overlay);
            if (overlay != null) Destroy(overlay);
        }

        private void SpawnScheduledProjectile(CombatEvent combatEvent)
        {
            if (player == null || !_targets.TryGetValue(combatEvent.TargetId, out CombatTargetView target)) return;
            CompleteScheduledProjectile(combatEvent.ActionSequenceId);
            var projectile = new GameObject("VFX_Placeholder_ProjectileOrb_" + combatEvent.SkillId);
            projectile.transform.position = player.LogicalPosition;
            projectile.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
            var renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = PixelSprite;
            renderer.color = new Color(0.67f, 0.32f, 1f, 0.95f);
            renderer.sortingOrder = 1001;
            _projectiles[combatEvent.ActionSequenceId] = projectile;
            float duration = Mathf.Max(
                0.01f,
                CombatTimeContract.TicksToMilliseconds(combatEvent.DurationTicks) / 1000f);
            StartCoroutine(AnimateScheduledProjectile(
                combatEvent.ActionSequenceId,
                projectile,
                target,
                duration));
        }

        private IEnumerator AnimateScheduledProjectile(
            long actionSequenceId,
            GameObject projectile,
            CombatTargetView target,
            float duration)
        {
            Vector3 start = projectile.transform.position;
            float elapsed = 0f;
            while (elapsed < duration && projectile != null && target != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                projectile.transform.position = Vector3.Lerp(start, target.transform.position, progress);
                yield return null;
            }
            while (_projectiles.TryGetValue(actionSequenceId, out GameObject current) &&
                   current == projectile && projectile != null && target != null)
            {
                projectile.transform.position = target.transform.position;
                yield return null;
            }
        }

        private void CompleteScheduledProjectile(long actionSequenceId)
        {
            if (!_projectiles.TryGetValue(actionSequenceId, out GameObject projectile)) return;
            _projectiles.Remove(actionSequenceId);
            if (projectile != null) Destroy(projectile);
        }

        private void ShowStatusIndicator(string targetId, string skillId)
        {
            string key = targetId + ":" + skillId;
            if (_statusIndicators.ContainsKey(key) ||
                !_targets.TryGetValue(targetId, out CombatTargetView target)) return;
            var indicator = new GameObject("VFX_Placeholder_StatusIndicator_" + skillId);
            indicator.transform.SetParent(target.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            indicator.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
            var renderer = indicator.AddComponent<SpriteRenderer>();
            renderer.sprite = PixelSprite;
            renderer.color = new Color(1f, 0.22f, 0.05f, 0.9f);
            renderer.sortingOrder = 1002;
            _statusIndicators[key] = indicator;
        }

        private void HideStatusIndicator(string targetId, string skillId)
        {
            string key = targetId + ":" + skillId;
            if (!_statusIndicators.TryGetValue(key, out GameObject indicator)) return;
            _statusIndicators.Remove(key);
            if (indicator != null) Destroy(indicator);
        }

        private void SpawnGroundRing(Vector3 center, float radius)
        {
            var ring = new GameObject("VFX_Placeholder_GroundCircle");
            ring.transform.position = center;
            var line = ring.AddComponent<LineRenderer>();
            const int segments = 32;
            line.loop = true;
            line.positionCount = segments;
            line.useWorldSpace = false;
            line.startWidth = 0.035f;
            line.endWidth = 0.035f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.95f, 0.48f, 0.16f, 0.9f);
            line.endColor = line.startColor;
            line.sortingOrder = 999;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            StartCoroutine(FadeGroundRing(ring, line));
        }

        private static IEnumerator FadeGroundRing(GameObject ring, LineRenderer line)
        {
            const float duration = 0.35f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                Color color = line.startColor;
                color.a = alpha;
                line.startColor = color;
                line.endColor = color;
                yield return null;
            }
            Destroy(ring);
        }

        private void SpawnImpactPulse(Vector3 position, string skillId)
        {
            var pulse = new GameObject("VFX_Placeholder_" + (skillId ?? "SkillImpact"));
            pulse.transform.position = position;
            var renderer = pulse.AddComponent<SpriteRenderer>();
            renderer.sprite = PixelSprite;
            renderer.color = new Color(1f, 0.72f, 0.18f, 0.8f);
            renderer.sortingOrder = 1000;
            pulse.transform.localScale = new Vector3(0.12f, 0.12f, 1f);
            StartCoroutine(AnimateImpactPulse(pulse, renderer));
        }

        private static IEnumerator AnimateImpactPulse(GameObject pulse, SpriteRenderer renderer)
        {
            const float duration = 0.22f;
            float elapsed = 0f;
            Color color = renderer.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float size = Mathf.Lerp(0.12f, 0.72f, progress);
                pulse.transform.localScale = new Vector3(size, size, 1f);
                color.a = 1f - progress;
                renderer.color = color;
                yield return null;
            }
            Destroy(pulse);
        }

        private void SpawnPopup(Vector3 position, int amount, bool critical)
        {
            var popup = new GameObject("CombatDamagePopup");
            popup.transform.position = position + new Vector3(0f, 0.85f, 0f);
            var text = popup.AddComponent<TextMeshPro>();
            text.text = critical ? $"{amount}!" : amount.ToString();
            text.fontSize = critical ? critPopupFontSize : 3.6f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = critical ? critPopupColor : new Color(1f, 0.32f, 0.25f);
            popup.transform.localScale = critical ? Vector3.zero : Vector3.one;
            StartCoroutine(AnimatePopup(popup, text, critical));
        }

        private void SpawnLootPopup(Vector3 position, string message)
            => SpawnFloatingText(position, message, lootPopupColor, lootPopupFontSize);

        private void SpawnFloatingText(Vector3 position, string message, Color color, float fontSize)
        {
            var popup = new GameObject("LootPickupPopup");
            popup.transform.position = position + lootPopupOffset;
            var text = popup.AddComponent<TextMeshPro>();
            text.text = message;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            StartCoroutine(AnimateLootPopup(popup, text));
        }

        private IEnumerator AnimateLootPopup(GameObject popup, TextMeshPro text)
        {
            float duration = Mathf.Max(0.05f, lootPopupDuration);
            float elapsed = 0f;
            Color color = text.color;
            float baseAlpha = color.a;
            while (elapsed < duration && popup != null)
            {
                elapsed += Time.deltaTime;
                popup.transform.position += Vector3.up * (lootPopupRiseSpeed * Time.deltaTime);
                color.a = baseAlpha * (1f - Mathf.Clamp01(elapsed / duration));
                text.color = color;
                yield return null;
            }
            if (popup != null) Destroy(popup);
        }

        private IEnumerator AnimatePopup(GameObject popup, TextMeshPro text, bool critical)
        {
            if (critical)
            {
                float punchDuration = Mathf.Max(0.01f, critPunchDuration);
                float punchElapsed = 0f;
                while (punchElapsed < punchDuration && popup != null)
                {
                    punchElapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(punchElapsed / punchDuration);
                    float scale = progress < 0.5f
                        ? Mathf.Lerp(0f, critPunchScale, progress * 2f)
                        : Mathf.Lerp(critPunchScale, 1f, (progress - 0.5f) * 2f);
                    popup.transform.localScale = Vector3.one * scale;
                    yield return null;
                }
                if (popup != null) popup.transform.localScale = Vector3.one;
            }

            float duration = Mathf.Max(0.05f, combatPopupDuration);
            float elapsed = 0f;
            Color color = text.color;
            while (elapsed < duration && popup != null)
            {
                elapsed += Time.deltaTime;
                popup.transform.position += Vector3.up * (combatPopupRiseSpeed * Time.deltaTime);
                color.a = 1f - Mathf.Clamp01(elapsed / duration);
                text.color = color;
                yield return null;
            }
            if (popup != null) Destroy(popup);
        }

        private static Sprite PixelSprite
        {
            get
            {
                if (_pixelSprite != null) return _pixelSprite;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply(false, true);
                _pixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                return _pixelSprite;
            }
        }

        private static Sprite _pixelSprite;
    }
}
