using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Balance/Auto Combat Policy", fileName = "AutoCombatPolicyConfig")]
    public sealed class AutoCombatPolicyConfigAsset : ScriptableObject
    {
        [SerializeField] private string balanceVersion = "auto-combat-v1";
        [Tooltip("Seconds after manual input before automatic skill selection resumes.")]
        [Min(0)] public float autoResumeGraceSeconds = 5f;
        public bool autoSkillRotation = true;
        [Tooltip("Fraction of maximum HP regenerated per second outside combat.")]
        [Min(0)] public float outOfCombatRegenPctPerSec = 0.02f;
        [Tooltip("Seconds before a defeated character can resume combat.")]
        [Min(0)] public float deathDowntimeSeconds = 10f;

        public string ConfigurationVersion => "auto-combat+" + balanceVersion;
    }
}
