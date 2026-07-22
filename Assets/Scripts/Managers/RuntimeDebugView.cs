using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using UnityEngine;

namespace IdleCloud.Managers
{
    /// <summary>
    /// Read-only Inspector bridge for authoritative session state. It owns no
    /// gameplay state and is safe to add to the persistent manager object.
    /// </summary>
    public sealed class RuntimeDebugView : MonoBehaviour
    {
        [Tooltip("Optional explicit manager reference. If empty, the singleton is used.")]
        [SerializeField] private GameManager gameManager;

        public GameManager Manager => gameManager != null ? gameManager : GameManager.Instance;
        public string AccountId => Manager?.Account?.Id;
        public string CharacterId => Manager?.SelectedCharacterId;
        public string ConfigurationVersion => RuntimeContent.ConfigurationVersion;
        public long AccountRevision => Manager?.Session.AccountRevision ?? 0;
        public Character Character
        {
            get
            {
                Account account = Manager?.Account;
                string id = Manager?.SelectedCharacterId;
                foreach (Character character in account?.Characters ?? new List<Character>())
                    if (character != null && character.Id == id) return character;
                return null;
            }
        }

        public CoreStats EffectiveStats
        {
            get
            {
                Character character = Character;
                return character == null ? null : Progression.EffectiveStats(
                    character, RuntimeContent.Get(character.ClassId),
                    new Dictionary<string, ItemDef>(RuntimeContent.Items), AccountBonuses.Zero());
            }
        }

        public string Activity => Character?.Activity == null
            ? "<none>"
            : Character.Activity.Kind + ":" + Character.Activity.TargetId;
        public string EfficiencyVersion => Character?.Efficiency?.ContentVersion ?? "<none>";
        public string SnapshotBreakdown => Character?.Efficiency?.DebugBreakdown ?? "<none>";
        public string SnapshotValidity => Character?.Efficiency == null
            ? "none"
            : SnapshotValidation.IsUsable(Character.Efficiency, Character) ? "valid" : "invalid";
        public int BankSlots => Manager?.Account?.Bank?.Slots?.Count ?? 0;
        public int BankCapacity => Manager?.Account?.Bank?.MaxSlots ?? 0;
        public int InventorySlots => Character?.Inventory?.Count ?? 0;
        public int InventoryCapacity => Character?.MaxInventorySlots ?? 0;
        public ActiveCombatState CombatState => Manager?.ActiveCombatState;
        public string LastRewardTransactionId => FindLastRewardTransactionId(Manager?.SessionTrace);

        private static string FindLastRewardTransactionId(IReadOnlyList<SessionTraceEntry> trace)
        {
            for (int index = (trace?.Count ?? 0) - 1; index >= 0; index--)
            {
                SessionTraceEntry entry = trace[index];
                if (entry?.Kind == SessionEventKind.CombatRewardsResolved ||
                    entry?.Kind == SessionEventKind.LootPickedUp ||
                    entry?.Kind == SessionEventKind.ResourceGathered ||
                    entry?.Kind == SessionEventKind.OfflineRewardsClaimed)
                    return entry.Kind + ":" + entry.CommandName;
            }
            return "<none>";
        }
    }
}
