using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Talent Definition", fileName = "TalentDefinition")]
    public sealed class TalentDefinitionAsset : ScriptableObject
    {
        [Tooltip("Stable talent ID. Do not use the display name as an identifier.")]
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private bool availableToAll;
        [SerializeField] private ClassId classId;
        [SerializeField] private BonusStat bonusStat;
        [SerializeField] private double bonusPerPoint;
        [SerializeField, Min(1)] private int maxPoints = 1;

        public string StableId => stableId;
        public TalentDef ToPureDefinition() => new TalentDef
        {
            Id = stableId, Name = displayName, Description = description, AvailableToAll = availableToAll,
            ClassId = availableToAll ? (ClassId?)null : classId, BonusStat = bonusStat,
            BonusPerPoint = bonusPerPoint, MaxPoints = maxPoints,
        };
    }
}
