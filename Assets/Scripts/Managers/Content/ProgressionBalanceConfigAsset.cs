using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Balance/Progression", fileName = "ProgressionBalanceConfig")]
    public sealed class ProgressionBalanceConfigAsset : ScriptableObject
    {
        [SerializeField] private string balanceVersion = "progression-v1";
        [Header("XP curve")]
        [Min(0)] public float xpCoefficient = 50;
        [Min(0.001f)] public float xpExponent = 1.8f;
        [Min(0)] public float xpBase = 50;

        public string ConfigurationVersion => "progression+" + balanceVersion;
        public ProgressionBalanceConfig ToPureDefinition() => new ProgressionBalanceConfig
        {
            XpCoefficient = xpCoefficient, XpExponent = xpExponent, XpBase = xpBase,
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (xpExponent <= 0 || xpCoefficient < 0 || xpBase < 0)
                Debug.LogWarning("[ProgressionBalanceConfig] XP curve values are invalid.", this);
        }
#endif
    }
}
