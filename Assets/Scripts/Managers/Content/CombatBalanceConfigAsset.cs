using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Balance/Combat", fileName = "CombatBalanceConfig")]
    public sealed class CombatBalanceConfigAsset : ScriptableObject
    {
        [Header("Versioning")]
        [SerializeField] private string balanceVersion = "combat-v1";
        [Header("HP and accuracy")]
        [Min(0)] public float maxHpBase = 5;
        [Min(0)] public float maxHpPerLevel = 2;
        [Min(0)] public float maxHpPerStrength = 1;
        [SerializeField, Min(0)] public float accuracyBase = 2;
        [Range(0, 1)] public float hitChanceMinimum = 0.05f;
        [Range(0, 1)] public float hitChanceMaximum = 1f;
        [Min(0.001f)] public float defenseCurveConstant = 100;
        [Header("Attack speed and crit")]
        [Min(0)] public float attackSpeedBase = 1;
        [Min(0.001f)] public float attackSpeedAgilityDivisor = 20;
        [Min(0.001f)] public float attackSpeedMaximum = 2;
        [Range(0, 1)] public float monsterHitChanceMinimum = 0.1f;
        [Range(0, 1)] public float monsterHitChanceMaximum = 0.9f;
        [Range(0, 1)] public float critBase = 0.03f;
        [Min(0.001f)] public float critLuckDivisor = 40;
        [Range(0, 1)] public float critMaximum = 0.4f;
        [Min(0)] public float critMultiplierBase = 1.5f;
        [Min(0.001f)] public float critMultiplierLuckDivisor = 50;
        [Min(0)] public float critMultiplierLuckBonusMaximum = 0.5f;
        [Header("Gathering")]
        [Range(0, 1)] public float harvestSuccessBase = 0.5f;
        [Min(0)] public float harvestSuccessPerLevel = 0.04f;
        [Range(0, 1)] public float harvestSuccessMinimum = 0.1f;
        [Range(0, 1)] public float harvestSuccessMaximum = 0.95f;
        [Min(0.001f)] public float harvestStatBase = 20;

        public string ConfigurationVersion => "combat+" + balanceVersion;
        public CombatBalanceConfig ToPureDefinition() => new CombatBalanceConfig
        {
            MaxHpBase = maxHpBase, MaxHpPerLevel = maxHpPerLevel, MaxHpPerStrength = maxHpPerStrength,
            AccuracyBase = accuracyBase, HitChanceMinimum = hitChanceMinimum, HitChanceMaximum = hitChanceMaximum,
            DefenseCurveConstant = defenseCurveConstant, AttackSpeedBase = attackSpeedBase,
            AttackSpeedAgilityDivisor = attackSpeedAgilityDivisor, AttackSpeedMaximum = attackSpeedMaximum,
            MonsterHitChanceMinimum = monsterHitChanceMinimum, MonsterHitChanceMaximum = monsterHitChanceMaximum,
            CritBase = critBase, CritLuckDivisor = critLuckDivisor, CritMaximum = critMaximum,
            CritMultiplierBase = critMultiplierBase, CritMultiplierLuckDivisor = critMultiplierLuckDivisor,
            CritMultiplierLuckBonusMaximum = critMultiplierLuckBonusMaximum,
            HarvestSuccessBase = harvestSuccessBase, HarvestSuccessPerLevel = harvestSuccessPerLevel,
            HarvestSuccessMinimum = harvestSuccessMinimum, HarvestSuccessMaximum = harvestSuccessMaximum,
            HarvestStatBase = harvestStatBase,
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (hitChanceMinimum > hitChanceMaximum || monsterHitChanceMinimum > monsterHitChanceMaximum ||
                critBase > critMaximum || harvestSuccessMinimum > harvestSuccessMaximum ||
                attackSpeedAgilityDivisor <= 0 || critLuckDivisor <= 0 || critMultiplierLuckDivisor <= 0)
                Debug.LogWarning("[CombatBalanceConfig] One or more ranges/divisors are invalid.", this);
        }
#endif
    }
}
