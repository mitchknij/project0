using System.Collections.Generic;
using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Map Gameplay Definition", fileName = "MapGameplayDefinition")]
    public sealed class MapGameplayDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable map ID used by saves, travel, and activity snapshots.")]
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int recommendedLevel = 1;
        [Header("Travel")]
        [SerializeField] private List<string> connections = new List<string>();
        [Header("Efficiency")]
        [SerializeField, Min(0.01f)] private double encounterDensity = 1.0;
        [Tooltip("Milliseconds of travel/respawn overhead included in kills-per-hour calculations.")]
        [SerializeField, Min(0f)] private double combatTravelOverheadMilliseconds = 1500.0;

        public string StableId => stableId;
        public MapDef ToPureDefinition() => new MapDef
        {
            Id = stableId, Name = displayName, RecommendedLevel = recommendedLevel,
            Connections = new List<string>(connections ?? new List<string>()),
            EncounterDensity = encounterDensity,
            CombatTravelOverheadMs = combatTravelOverheadMilliseconds,
        };
    }
}
