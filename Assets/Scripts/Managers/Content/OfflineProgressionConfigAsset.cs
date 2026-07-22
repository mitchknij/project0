using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Balance/Offline Progression", fileName = "OfflineProgressionConfig")]
    public sealed class OfflineProgressionConfigAsset : ScriptableObject
    {
        [Header("Versioning")]
        [Tooltip("Increment when offline rates, caps, or policy change. This invalidates existing efficiency snapshots.")]
        [SerializeField] private string balanceVersion = "offline-v1";
        [Header("Offline timing")]
        [Tooltip("Fraction of the active snapshot rate used while offline (0..1).")]
        [SerializeField, Range(0f, 1f)] private float rate = 0.4f;
        [Tooltip("Maximum elapsed time processed in one login, in hours.")]
        [SerializeField, Min(0.01f)] private float capHours = 24f;
        [Tooltip("Elapsed time shorter than this is ignored, in minutes.")]
        [SerializeField, Min(0f)] private float minimumDurationMinutes = 1f;

        public string BalanceVersion => balanceVersion;
        public string ConfigurationVersion => "offline+" + balanceVersion;

        public OfflineBalanceConfig ToPureDefinition()
        {
            long capMs = (long)(capHours * 60.0 * 60.0 * 1000.0);
            long minimumMs = (long)(minimumDurationMinutes * 60.0 * 1000.0);
            return new OfflineBalanceConfig
            {
                Rate = rate, CapMs = capMs, MinimumDurationMs = minimumMs,
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(balanceVersion)) Debug.LogWarning("[OfflineProgressionConfig] balance version is missing", this);
            OfflineBalanceConfig value = ToPureDefinition();
            if (value.CapMs <= 0 || value.MinimumDurationMs < 0 || value.MinimumDurationMs > value.CapMs)
                Debug.LogWarning("[OfflineProgressionConfig] duration range is invalid", this);
        }
#endif
    }
}
