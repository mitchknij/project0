using System;
using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Managers
{
    public enum SessionEventKind
    {
        SessionRestored,
        AccountChanged,
        CharacterSelected,
        CharacterChanged,
        InventoryChanged,
        BankChanged,
        ActivityChanged,
        CombatRewardsResolved,
        LootPickedUp,
        ResourceGathered,
        SkillUnlocked,
        SkillSlotChanged,
        OfflineRewardsClaimed,
        SaveCompleted,
    }

    public sealed class SessionEvent
    {
        public long Timestamp;
        public SessionEventKind Kind;
        public string CommandName;
        public string CharacterId;
        public long AccountRevision;
        public long CharacterRevision;
        public long BankRevision;
        public long ActivityRevision;
    }

    public sealed class SessionTraceEntry
    {
        public long Timestamp;
        public SessionEventKind Kind;
        public string CommandName;
        public string CharacterId;
        public long AccountRevision;
        public long CharacterRevision;
        public long BankRevision;
        public long ActivityRevision;

        public SessionTraceEntry Clone() => (SessionTraceEntry)MemberwiseClone();
    }

    public abstract class SessionCommand
    {
        public abstract string Name { get; }
        public virtual string CharacterId => null;
    }

    public sealed class SelectCharacterCommand : SessionCommand
    {
        private readonly string _characterId;
        public SelectCharacterCommand(string characterId) => _characterId = characterId;
        public override string Name => "select-character";
        public override string CharacterId => _characterId;
    }

    public sealed class DepositItemCommand : SessionCommand
    {
        private readonly string _characterId;
        public DepositItemCommand(string characterId) => _characterId = characterId;
        public override string Name => "deposit-item";
        public override string CharacterId => _characterId;
    }

    public sealed class WithdrawItemCommand : SessionCommand
    {
        private readonly string _characterId;
        public WithdrawItemCommand(string characterId) => _characterId = characterId;
        public override string Name => "withdraw-item";
        public override string CharacterId => _characterId;
    }

    public sealed class ChangeActivityCommand : SessionCommand
    {
        private readonly string _characterId;
        public ChangeActivityCommand(string characterId) => _characterId = characterId;
        public override string Name => "change-activity";
        public override string CharacterId => _characterId;
    }

    public sealed class CharacterMutationCommand : SessionCommand
    {
        private readonly string _name;
        private readonly string _characterId;
        public CharacterMutationCommand(string name, string characterId)
        {
            _name = name;
            _characterId = characterId;
        }
        public override string Name => _name;
        public override string CharacterId => _characterId;
    }

    public sealed class OfflineClaimCommand : SessionCommand
    {
        public override string Name => "claim-offline-rewards";
    }

    public sealed class ResolveCombatKillCommand : SessionCommand
    {
        private readonly string _characterId;
        public ResolveCombatKillCommand(string characterId) => _characterId = characterId;
        public override string Name => "resolve-combat-kill";
        public override string CharacterId => _characterId;
    }

    public sealed class LootPickupCommand : SessionCommand
    {
        private readonly string _characterId;
        public LootPickupCommand(string characterId) => _characterId = characterId;
        public override string Name => "loot-pickup";
        public override string CharacterId => _characterId;
    }

    public sealed class RestoreSessionCommand : SessionCommand
    {
        public override string Name => "restore-session";
    }

    /// <summary>
    /// Single owner of mutable application state. Core returns a fully updated account;
    /// only this class publishes the committed snapshot and revision events.
    /// </summary>
    public sealed class GameSession
    {
        private Account _account;
        public Account Account => _account?.Clone();
        public string SelectedCharacterId { get; private set; }
        public IReadOnlyDictionary<string, int> WorldKills => _worldKills;
        public bool IsDirty { get; private set; }

        public long AccountRevision { get; private set; }
        public long CharacterRevision { get; private set; }
        public long BankRevision { get; private set; }
        public long ActivityRevision { get; private set; }

        private Dictionary<string, int> _worldKills = new Dictionary<string, int>();
        private readonly List<SessionTraceEntry> _trace = new List<SessionTraceEntry>();
        private const int TraceCapacity = 128;

        public event Action<SessionEvent> Changed;

        public IReadOnlyList<SessionTraceEntry> CopyTrace()
        {
            var copy = new List<SessionTraceEntry>(_trace.Count);
            foreach (SessionTraceEntry entry in _trace) copy.Add(entry.Clone());
            return copy;
        }

        public void Restore(Account account, string selectedCharacterId, Dictionary<string, int> worldKills)
        {
            _account = account?.Clone();
            SelectedCharacterId = selectedCharacterId;
            _worldKills = worldKills != null
                ? new Dictionary<string, int>(worldKills)
                : new Dictionary<string, int>();
            IsDirty = false;
            Publish(SessionEventKind.SessionRestored, new RestoreSessionCommand());
        }

        public void Commit(Account account, SessionCommand command, SessionEventKind kind)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _account = account?.Clone();
            IsDirty = true;
            AccountRevision++;

            if (kind == SessionEventKind.CharacterChanged || kind == SessionEventKind.ActivityChanged ||
                kind == SessionEventKind.CombatRewardsResolved || kind == SessionEventKind.LootPickedUp ||
                kind == SessionEventKind.ResourceGathered ||
                kind == SessionEventKind.SkillUnlocked || kind == SessionEventKind.SkillSlotChanged)
                CharacterRevision++;
            if (kind == SessionEventKind.BankChanged || kind == SessionEventKind.InventoryChanged ||
                kind == SessionEventKind.CombatRewardsResolved || kind == SessionEventKind.LootPickedUp ||
                kind == SessionEventKind.ResourceGathered)
                BankRevision++;
            if (kind == SessionEventKind.ActivityChanged)
                ActivityRevision++;

            Publish(kind, command);
        }

        public void SelectCharacter(string characterId)
        {
            SelectedCharacterId = characterId;
            IsDirty = true;
            Publish(SessionEventKind.CharacterSelected, new SelectCharacterCommand(characterId));
        }

        public void ReplaceWorldKills(Dictionary<string, int> worldKills)
        {
            _worldKills = worldKills != null
                ? new Dictionary<string, int>(worldKills)
                : new Dictionary<string, int>();
            IsDirty = true;
        }

        public Dictionary<string, int> CopyWorldKills() => new Dictionary<string, int>(_worldKills);

        public void IncrementWorldKill(string monsterId)
        {
            if (string.IsNullOrWhiteSpace(monsterId)) throw new ArgumentException("Monster ID is required.", nameof(monsterId));
            _worldKills.TryGetValue(monsterId, out int kills);
            _worldKills[monsterId] = checked(kills + 1);
        }

        public void MarkSaved()
        {
            IsDirty = false;
            Publish(SessionEventKind.SaveCompleted, new CharacterMutationCommand("save", SelectedCharacterId));
        }

        private void Publish(SessionEventKind kind, SessionCommand command)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var trace = new SessionTraceEntry
            {
                Timestamp = timestamp,
                Kind = kind,
                CommandName = command.Name,
                CharacterId = command.CharacterId,
                AccountRevision = AccountRevision,
                CharacterRevision = CharacterRevision,
                BankRevision = BankRevision,
                ActivityRevision = ActivityRevision,
            };
            _trace.Add(trace);
            if (_trace.Count > TraceCapacity) _trace.RemoveAt(0);

            Changed?.Invoke(new SessionEvent
            {
                Timestamp = timestamp,
                Kind = kind,
                CommandName = command.Name,
                CharacterId = command.CharacterId,
                AccountRevision = AccountRevision,
                CharacterRevision = CharacterRevision,
                BankRevision = BankRevision,
                ActivityRevision = ActivityRevision,
            });
        }
    }
}
