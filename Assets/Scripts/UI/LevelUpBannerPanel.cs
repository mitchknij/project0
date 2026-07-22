using System.Collections;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Managers;
using TMPro;
using UnityEngine;

namespace IdleCloud.UI
{
    public sealed class LevelUpBannerPanel : MonoBehaviour
    {
        [HideInInspector] public TextMeshProUGUI label;
        [HideInInspector] public CanvasGroup canvasGroup;

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float popInDuration = 0.18f;
        [SerializeField, Min(1f)] private float popOvershootScale = 1.15f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.16f;
        [SerializeField, Min(0f)] private float holdDuration = 0.8f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;
        [SerializeField] private Color goldFlashColor = UITheme.GoldPale;

        private readonly Queue<LevelUpEvent> _pending = new Queue<LevelUpEvent>();
        private GameManager _manager;
        private Coroutine _animation;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            _baseScale = transform.localScale;
            SetIdleState();
        }

        private void Update()
        {
            GameManager current = GameManager.Instance;
            if (_manager != current)
            {
                Unsubscribe();
                _manager = current;
                if (_manager != null) _manager.LevelUp += HandleLevelUp;
            }

            if (_animation == null && _pending.Count > 0 && label != null && canvasGroup != null)
                _animation = StartCoroutine(PlayQueuedLevels());
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_animation != null) StopCoroutine(_animation);
        }

        private void Unsubscribe()
        {
            if (_manager == null) return;
            _manager.LevelUp -= HandleLevelUp;
            _manager = null;
        }

        private void HandleLevelUp(LevelUpEvent payload)
        {
            if (payload == null) return;
            _pending.Enqueue(payload);
        }

        private IEnumerator PlayQueuedLevels()
        {
            while (_pending.Count > 0)
            {
                LevelUpEvent payload = _pending.Dequeue();
                Color baseColor = label.color;
                label.text = FormatLevelUp(payload);
                label.color = baseColor;
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                float popDuration = Mathf.Max(0.01f, popInDuration);
                float elapsed = 0f;
                while (elapsed < popDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / popDuration);
                    float scale = progress < 0.65f
                        ? Mathf.Lerp(0f, popOvershootScale, progress / 0.65f)
                        : Mathf.Lerp(popOvershootScale, 1f, (progress - 0.65f) / 0.35f);
                    transform.localScale = _baseScale * scale;
                    yield return null;
                }
                transform.localScale = _baseScale;

                float flashTime = Mathf.Max(0.01f, flashDuration);
                elapsed = 0f;
                while (elapsed < flashTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    label.color = Color.Lerp(goldFlashColor, baseColor,
                        Mathf.Clamp01(elapsed / flashTime));
                    yield return null;
                }
                label.color = baseColor;

                yield return WaitUnscaled(holdDuration);

                float fadeTime = Mathf.Max(0.01f, fadeDuration);
                elapsed = 0f;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeTime);
                    yield return null;
                }
                SetIdleState();
            }

            _animation = null;
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            float waitDuration = Mathf.Max(0f, duration);
            while (elapsed < waitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetIdleState()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            transform.localScale = _baseScale;
        }

        private static string FormatLevelUp(LevelUpEvent payload)
        {
            if (payload.Kind != LevelUpKind.Skill || !payload.SkillId.HasValue)
                return $"LEVEL {payload.NewLevel}!";
            return $"{ResolveSkillName(payload.SkillId.Value)} LEVEL {payload.NewLevel}!";
        }

        private static string ResolveSkillName(SkillId skillId)
        {
            switch (skillId)
            {
                case SkillId.Combat: return "COMBAT";
                case SkillId.Mining: return "MINING";
                case SkillId.Chopping: return "CHOPPING";
                case SkillId.Gathering: return "GATHERING";
                default: return skillId.ToString().ToUpperInvariant();
            }
        }
    }
}
