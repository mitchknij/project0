using System.Collections.Generic;
using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Monster Definition", fileName = "MonsterDefinition")]
    public sealed class MonsterDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable monster ID used by activities and saves. Display names are never identifiers.")]
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private string mapId;
        [Header("Combat")]
        [SerializeField, Min(1)] private int hp = 1;
        [SerializeField, Min(0)] private int damage;
        [SerializeField, Min(0)] private int defense;
        [SerializeField, Min(1)] private int accuracy = 1;
        [SerializeField, Min(0)] private int agility;
        [SerializeField, Min(0)] private int xp;
        [SerializeField] private Element element;
        [SerializeField] private bool hasElement;
        [SerializeField] private MonsterBehavior behavior;
        [SerializeField] private bool hasBehavior;
        [Header("Rewards")]
        [SerializeField, Min(0)] private int minimumCoins;
        [SerializeField, Min(0)] private int maximumCoins;
        [SerializeField] private DropTableDefinitionAsset drops;
        [Header("Respawn")]
        [SerializeField, Min(0)] private int respawnMilliseconds;
        [Header("Presentation")]
        [SerializeField] private GameObject visualPrefab;

        public string StableId => stableId;
        public GameObject VisualPrefab => visualPrefab;

        public MonsterDef ToPureDefinition() => new MonsterDef
        {
            Id = stableId, Name = displayName, MapId = mapId, Hp = hp, Damage = damage,
            Defense = defense, Accuracy = accuracy, Agility = agility, Xp = xp,
            Coins = new CoinRange { Min = minimumCoins, Max = maximumCoins },
            Drops = drops == null ? new DropTable { Always = new List<DropItem>(), Main = null } : drops.ToPureDefinition(),
            RespawnMs = respawnMilliseconds,
            Element = hasElement ? element : (Element?)null,
            Behavior = hasBehavior ? behavior : (MonsterBehavior?)null,
        };
    }
}
