// GameManager.cs — Centrale game-state en acties (vertaling van src/state/store.ts).
// MonoBehaviour singleton; houdt Account, SelectedCharacterId, OfflineReport en WorldKills
// vast als mutable C#-velden (geen React/Zustand-detectie nodig — Unity is single-threaded).
//
// Alle acties die afhankelijk zijn van een Unity-scene (enterCombat, exitCombat, syncCombat,
// setCombatHud, portalTravel, travelToWorld, applySimInventoryDelta) worden uitgesteld naar de
// scene-module; ze zijn hier expliciet weggelaten (zie plan Module 3 §4).

using System;
using System.Collections.Generic;
using UnityEngine;
using IdleCloud.Core;
using IdleCloud.Data;

namespace IdleCloud.Managers
{
    public enum LevelUpKind
    {
        Character,
        Skill,
    }

    public sealed class XpAwardedEvent
    {
        public long CharacterXp;
        public long SkillXp;
        public SkillId SkillId;
    }

    public sealed class LevelUpEvent
    {
        public LevelUpKind Kind;
        public SkillId? SkillId;
        public int PreviousLevel;
        public int NewLevel;
    }

    /// <summary>
    /// Centrale game-state manager. Spiegelt de Zustand-store uit store.ts:
    /// - State-velden zijn publiek leesbaar (no setter buiten GameManager).
    /// - Acties zijn publieke methoden (geen lambda-currying nodig).
    /// - Heartbeat wordt via Update() getickt (~1 s interval).
    /// - Offline-simulatie wordt verwerkt bij het laden en bij terugkeer van achtergrond.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────────

        public static GameManager Instance { get; private set; }

        [Header("Active Combat")]
        [Tooltip("Focused automatic-combat policy asset.")]
        [SerializeField] private AutoCombatPolicyConfigAsset autoCombatPolicy;
        [Header("Skill Content")]
        [Tooltip("Single entry point for focused authoring registries and their versions.")]
        [SerializeField] private ContentRegistryAsset contentRegistry;
        [Tooltip("Legacy skill-only registry kept for migration of older scenes. New scenes should use Content Registry.")]
        [SerializeField] private SkillContentRegistryAsset skillContentRegistry;
        [Header("Ground Loot")]
        [SerializeField, Min(0)] private long lootDespawnMs = 300000L;
        [SerializeField, Min(0)] private long lootAutoVacuumDelayMs = 1000L;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            try
            {
                if (contentRegistry != null)
                {
                    RuntimeContent.Configure(new ContentRegistryProvider(contentRegistry));
                    SnapshotValidation.ConfigureContentVersion(RuntimeContent.ConfigurationVersion);
                }
                else if (skillContentRegistry != null)
                {
                    RuntimeContent.Configure(new SkillContentProvider(skillContentRegistry));
                    SnapshotValidation.ConfigureContentVersion(RuntimeContent.ConfigurationVersion);
                }
                else
                    throw new InvalidOperationException("content_registry_missing");
                if (offlineProgressionConfig == null)
                    throw new InvalidOperationException("offline_progression_config_missing");
                if (autoCombatPolicy == null)
                    throw new InvalidOperationException("auto_combat_policy_missing");
                if (combatBalanceConfig == null)
                    throw new InvalidOperationException("combat_balance_config_missing");
                if (progressionBalanceConfig == null)
                    throw new InvalidOperationException("progression_balance_config_missing");
                CombatMath.Configure(combatBalanceConfig.ToPureDefinition());
                Progression.Configure(progressionBalanceConfig.ToPureDefinition());
                SnapshotValidation.ConfigureContentVersion(
                    RuntimeContent.ConfigurationVersion + "+" + offlineProgressionConfig.ConfigurationVersion + "+" +
                    combatBalanceConfig.ConfigurationVersion + "+" + progressionBalanceConfig.ConfigurationVersion + "+" +
                    autoCombatPolicy.ConfigurationVersion);
            }
            catch (Exception exception)
            {
                Debug.LogError("[GameManager] Skill content initialization failed: " + exception.Message);
                enabled = false;
                return;
            }
            _activityMaps = CloneMaps(RuntimeContent.Maps);
            _activityData = new ActivityDataBundle
            {
                Classes = new Dictionary<ClassId, ClassDef>(RuntimeContent.All),
                Items = new Dictionary<string, ItemDef>(RuntimeContent.Items),
                Monsters = new Dictionary<string, MonsterDef>(RuntimeContent.Monsters),
                Nodes = new Dictionary<string, ResourceNodeDef>(RuntimeContent.Nodes),
                Maps = _activityMaps,
            };
            _offlineData = new OfflineDataBundle
            {
                Items = new Dictionary<string, ItemDef>(RuntimeContent.Items),
                Monsters = new Dictionary<string, MonsterDef>(RuntimeContent.Monsters),
                Nodes = new Dictionary<string, ResourceNodeDef>(RuntimeContent.Nodes),
                OfflineBalance = offlineProgressionConfig.ToPureDefinition(),
            };
            _lootDrops = new LootDropManager(
                new Dictionary<string, ItemDef>(RuntimeContent.Items),
                new LootDropConfig
                {
                    DespawnMs = lootDespawnMs,
                    AutoVacuumDelayMs = lootAutoVacuumDelayMs,
                });
            _lootDrops.LootSpawned += RelayLootSpawned;
            _lootDrops.LootPickedUp += RelayLootPickedUp;
            _lootDrops.LootExpired += RelayLootExpired;
            _activeCombat = new ActiveCombatCoordinator(new SystemRandomSource());
            _activeCombat.Config = new ActiveCombatConfig
            {
                // Inspector values are seconds; ActiveSim consumes timestamps in milliseconds.
                AutoResumeGraceMs = Math.Max(0L, (long)Math.Ceiling(autoCombatPolicy.autoResumeGraceSeconds * 1000f)),
                AutoSkillRotation = autoCombatPolicy.autoSkillRotation,
                OutOfCombatRegenPctPerSec = Math.Max(0f, autoCombatPolicy.outOfCombatRegenPctPerSec),
            };
            _activeGathering = new ActiveGatheringCoordinator(new SystemRandomSource());
            ReportContentValidation();
        }

        // ── State (spiegelt StoreState in store.ts) ───────────────────────────────

        private readonly GameSession _session = new GameSession();
        private ActiveCombatCoordinator _activeCombat;
        private ActiveGatheringCoordinator _activeGathering;
        private LootDropManager _lootDrops;
        private ActiveCombatState _activeCombatState;
        private readonly List<CombatCommand> _queuedCombatCommands = new List<CombatCommand>();
        private long _respawnAtMs;
        private long _lootClockMs;
        private readonly HashSet<string> _vacuumFailureAnnounced = new HashSet<string>();

        public GameSession Session => _session;
        public IReadOnlyList<SessionTraceEntry> SessionTrace => _session.CopyTrace();
        public Account Account => _session.Account;
        public string SelectedCharacterId
        {
            get => _session.SelectedCharacterId;
            private set => _session.SelectCharacter(value);
        }
        public OfflineReport OfflineReport { get; private set; }
        public ActiveCombatState ActiveCombatState => _activeCombatState?.Clone();
        public bool AutoCombatEnabled => !(Account?.AutoCombatDisabled ?? false);
        public long LootDespawnMs => lootDespawnMs;
        public event Action<ActiveCombatTickResult> ActiveCombatResolved;
        public event Action<ActiveGatheringTickResult> ActiveGatheringResolved;
        public event Action<XpAwardedEvent> XpAwarded;
        public event Action<LevelUpEvent> LevelUp;
        public event Action<LootSpawnedEvent> LootSpawned;
        public event Action<LootPickedUpEvent> LootPickedUp;
        public event Action<LootPickupResult> LootPickupAttempted;
        public event Action<LootExpiredEvent> LootExpired;
        public event Action<string> LootCleared;
        public event Action<ItemStack> CraftCompleted;
        public Dictionary<string, int> WorldKills
        {
            get => _session.CopyWorldKills();
            private set => _session.ReplaceWorldKills(value);
        }

        // UI-state (waypoint-menu / hub-service)
        public string ActiveWaypointMenu { get; private set; }
        public HubServiceKind? ActiveHubService { get; private set; }

        [Header("Offline Progression Balance")]
        [Tooltip("Focused authoring asset for offline rate, cap, and minimum duration.")]
        [SerializeField] private OfflineProgressionConfigAsset offlineProgressionConfig;
        [Header("Formula Balance")]
        [SerializeField] private CombatBalanceConfigAsset combatBalanceConfig;
        [SerializeField] private ProgressionBalanceConfigAsset progressionBalanceConfig;

        // ── Data-bundles (spiegelt module-level consts in store.ts) ───────────────

        // Gecachete bundles — repos zijn statisch en veranderen nooit tijdens play.
        private Dictionary<string, MapDef> _activityMaps;
        private ActivityDataBundle _activityData;

        private OfflineDataBundle _offlineData;

        private static Dictionary<string, MapDef> CloneMaps(IReadOnlyDictionary<string, MapDef> source)
        {
            var result = new Dictionary<string, MapDef>();
            foreach (var pair in source)
            {
                MapDef map = pair.Value;
                result[pair.Key] = new MapDef
                {
                    Id = map.Id,
                    Name = map.Name,
                    RecommendedLevel = map.RecommendedLevel,
                    Connections = map.Connections == null ? new List<string>() : new List<string>(map.Connections),
                    EncounterDensity = map.EncounterDensity,
                    CombatTravelOverheadMs = map.CombatTravelOverheadMs,
                };
            }
            return result;
        }

        // ── Heartbeat-accumulator ─────────────────────────────────────────────────

        private float _heartbeatAccum;
        private const float HeartbeatIntervalSec = 1.0f;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            // Laad opgeslagen state (inclusief mapId-rehydratie via SaveManager)
            var save = SaveManager.Instance?.Load();
            if (save != null)
            {
                _session.Restore(save.Account, save.SelectedCharacterId, save.WorldKills);
            }

            // Verwerk offline progressie direct na het laden
            ProcessOffline();
        }

        private void Update()
        {
            DrainLootVacuum();
            _heartbeatAccum += Time.deltaTime;
            if (_heartbeatAccum >= HeartbeatIntervalSec)
            {
                _heartbeatAccum -= HeartbeatIntervalSec;
                Heartbeat();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Bij terugkeer vanuit de achtergrond offline progressie opnieuw verwerken
            if (hasFocus) ProcessOffline();
        }

        private void OnApplicationQuit()
        {
            ClearAllLoot();
        }

        // ── Save-aanvraag ─────────────────────────────────────────────────────────

        /// <summary>
        /// Triggert een bewaaroperatie. Aangeroepen door SaveManager.OnApplicationPause/Quit.
        /// </summary>
        public void RequestSave()
        {
            if (SaveManager.Instance == null || Account == null) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Account refreshed = ResnapshotActive(Account, now).Clone();
            refreshed.LastSeenAt = now;
            var command = new CharacterMutationCommand("save-snapshot", SelectedCharacterId);
            Account prepared = PrepareAccountForCommit(refreshed, command, SessionEventKind.AccountChanged);

            if (!SaveManager.Instance.Save(
                    prepared,
                    SelectedCharacterId,
                    _session.CopyWorldKills(),
                    _session.AccountRevision + 1))
                return;

            _session.Commit(prepared, command, SessionEventKind.AccountChanged);
            _session.MarkSaved();
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Aggregeert talent-bonussen voor één personage.
        /// Spiegelt bonusesFor(_account, character) in store.ts.
        /// </summary>
        private void CommitAccount(Account account, SessionCommand command, SessionEventKind kind)
        {
            _session.Commit(PrepareAccountForCommit(account, command, kind), command, kind);
        }

        private static bool IsSkillBarSlot(int slotIndex)
            => slotIndex >= 0 && slotIndex < Character.SkillBarSlots;

        private static bool IsClassSkill(ClassId classId, string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return false;
            ClassDef classDef = RuntimeContent.Get(classId);
            foreach (ClassSkillDef skill in classDef?.Skills ?? new List<ClassSkillDef>())
                if (skill != null && skill.Id == skillId)
                    return true;
            return false;
        }

        private static ClassSkillDef FindClassSkill(ClassId classId, string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return null;
            foreach (ClassSkillDef skill in RuntimeContent.Get(classId)?.Skills ?? new List<ClassSkillDef>())
                if (skill != null && skill.Id == skillId) return skill;
            return null;
        }

        private static List<string> CopySkillBar(List<string> source)
        {
            var result = new List<string>(Character.SkillBarSlots);
            for (int slot = 0; slot < Character.SkillBarSlots; slot++)
                result.Add(source != null && slot < source.Count ? source[slot] : null);
            return result;
        }

        private Account PrepareAccountForCommit(Account account, SessionCommand command, SessionEventKind kind)
        {
            if (account?.Characters == null || command == null || !ChangesCharacterState(kind)) return account;
            bool allCharacters = string.IsNullOrEmpty(command.CharacterId);
            var result = account;
            foreach (Character character in account.Characters)
            {
                if (character == null || (!allCharacters && character.Id != command.CharacterId)) continue;
                result = AccountHelper.UpdateCharacter(result, character.Id, current =>
                {
                    Character updated = current.Clone();
                    updated.CharacterRevision++;
                    if (kind == SessionEventKind.ActivityChanged) updated.ActivityRevision++;
                    if (updated.Efficiency != null)
                    {
                        updated.Efficiency.CharacterRevision = updated.CharacterRevision;
                        updated.Efficiency.ActivityRevision = updated.ActivityRevision;
                        updated.Efficiency.ContentVersion = SnapshotValidation.CurrentContentVersion;
                    }
                    return updated;
                });
            }
            return result;
        }

        private static bool ChangesCharacterState(SessionEventKind kind)
            => kind == SessionEventKind.CharacterChanged || kind == SessionEventKind.ActivityChanged ||
                kind == SessionEventKind.CombatRewardsResolved || kind == SessionEventKind.LootPickedUp ||
                kind == SessionEventKind.ResourceGathered ||
                kind == SessionEventKind.OfflineRewardsClaimed || kind == SessionEventKind.SkillUnlocked ||
                kind == SessionEventKind.SkillSlotChanged;

        private AccountBonuses BonusesFor(Character character)
            => BonusHelper.AddBonuses(
                AccountBonuses.Zero(),
                Talents.ComputeTalentBonuses(character, new Dictionary<string, TalentDef>(RuntimeContent.Talents)));

        private static void ReportContentValidation()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (string issue in ContentValidator.Validate())
                Debug.LogWarning($"[GameManager] Content validation: {issue}");
#endif
        }

        /// <summary>
        /// Herberekent de efficiency-snapshot van alle niet-idle personages nadat
        /// bonussen veranderd zijn (talent-allocatie, stat-allocatie).
        /// Spiegelt resnapshotActive(account, now) in store.ts.
        /// </summary>
        private Account ResnapshotActive(Account account, long now)
        {
            if (account?.Characters == null) return account;

            var result = account;
            foreach (var c in account.Characters)
            {
                if (c.Activity == null
                    || c.Activity.Kind == ActivityKind.Idle
                    || string.IsNullOrEmpty(c.Activity.TargetId))
                    continue;

                try
                {
                    result = AccountHelper.UpdateCharacter(result, c.Id, ch =>
                        Activity.AssignActivity(
                            ch,
                            ch.Activity.Kind,
                            ch.Activity.TargetId,
                            _activityData,
                            now,
                            BonusesFor(ch)));
                }
                catch { /* laat het personage ongewijzigd */ }
            }
            return result;
        }

        /// <summary>
        /// Applies inspector-authored encounter values to this runtime map copy and refreshes
        /// active snapshots. The canonical MapsRepo definitions remain unchanged.
        /// </summary>
        public bool ConfigureSceneMapEfficiency(string mapId, double encounterDensity, double travelOverheadMs)
        {
            if (string.IsNullOrWhiteSpace(mapId) || encounterDensity <= 0 || travelOverheadMs < 0 ||
                !_activityMaps.TryGetValue(mapId, out MapDef map))
                return false;

            map.EncounterDensity = encounterDensity;
            map.CombatTravelOverheadMs = travelOverheadMs;

            if (Account?.Characters == null) return true;
            bool hasActiveCharacterOnMap = false;
            foreach (Character character in Account.Characters)
            {
                if (character?.MapId == mapId && character.Activity != null &&
                    character.Activity.Kind != ActivityKind.Idle && !string.IsNullOrEmpty(character.Activity.TargetId))
                {
                    hasActiveCharacterOnMap = true;
                    break;
                }
            }
            if (!hasActiveCharacterOnMap) return true;

            Account updated = ResnapshotActive(Account, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated, new CharacterMutationCommand("scene-map-efficiency", null), SessionEventKind.ActivityChanged);
            return true;
        }

        /// <summary>Zoekt het geselecteerde personage op (null als geen selectie).</summary>
        private Character SelectedCharacter()
        {
            if (Account == null || SelectedCharacterId == null) return null;
            foreach (var c in Account.Characters)
                if (c.Id == SelectedCharacterId) return c;
            return null;
        }

        // ── Acties (spiegelen de store-acties in store.ts) ────────────────────────

        /// <summary>
        /// Maakt een nieuw account (familienaam) aan en reset de selectie.
        /// Spiegelt createFamily() in store.ts.
        /// </summary>
        public void CreateFamily(string familyName)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            CommitAccount(
                AccountHelper.CreateAccount(Guid.NewGuid().ToString(), familyName, now),
                new CharacterMutationCommand("create-family", null),
                SessionEventKind.AccountChanged);
            SelectedCharacterId = null;
            OfflineReport       = null;
            WorldKills          = new Dictionary<string, int>();
        }

        /// <summary>
        /// Maakt een nieuw personage aan en voegt het toe aan het account.
        /// Fouten (limiet, dubbele naam) worden stil genegeerd (UI handelt pre-validatie af).
        /// Spiegelt createCharacter() in store.ts.
        /// </summary>
        public void CreateCharacter(string name, ClassId classId)
        {
            if (Account == null) return;
            if (!RuntimeContent.All.ContainsKey(classId)) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var character = CharacterHelper.CreateCharacter(
                Guid.NewGuid().ToString(),
                name,
                classId,
                MapsRepo.StartingMapId,
                now);

            try
            {
                CommitAccount(
                    AccountHelper.AddCharacter(Account, character),
                    new CharacterMutationCommand("create-character", character.Id),
                    SessionEventKind.CharacterChanged);
            }
            catch { /* cap of duplicaatnaam — stil negeren */ }
        }

        /// <summary>Stelt het geselecteerde personage in. Spiegelt selectCharacter().</summary>
        public void SelectCharacter(string id)
        {
            SelectedCharacterId = id;
        }

        /// <summary>
        /// Verplaatst een item van de inventaris van het geselecteerde personage naar de bank.
        /// Spiegelt deposit() in store.ts.
        /// </summary>
        public void Deposit(string itemId, int qty)
        {
            if (Account == null || SelectedCharacterId == null) return;
            if (!RuntimeContent.Items.TryGetValue(itemId, out ItemDef def)) return;
            CommitAccount(
                AccountHelper.DepositItemToBank(Account, SelectedCharacterId, itemId, qty, def),
                new DepositItemCommand(SelectedCharacterId),
                SessionEventKind.BankChanged);
        }

        /// <summary>
        /// Verplaatst een item van de bank naar de inventaris van het geselecteerde personage.
        /// Spiegelt withdraw() in store.ts.
        /// </summary>
        public void Withdraw(string itemId, int qty)
        {
            if (Account == null || SelectedCharacterId == null) return;
            if (!RuntimeContent.Items.TryGetValue(itemId, out ItemDef def)) return;
            CommitAccount(
                AccountHelper.WithdrawItemFromBank(Account, SelectedCharacterId, itemId, qty, def),
                new WithdrawItemCommand(SelectedCharacterId),
                SessionEventKind.InventoryChanged);
        }

        /// <summary>
        /// Reset de volledige game-state en verwijdert de save.
        /// Spiegelt resetSave() in store.ts.
        /// </summary>
        public void ResetSave()
        {
            ClearAllLoot();
            _session.Restore(null, null, null);
            OfflineReport       = null;
            WorldKills          = new Dictionary<string, int>();
            ActiveWaypointMenu  = null;
            ActiveHubService    = null;
            SaveManager.Instance?.DeleteSave();
        }

        /// <summary>
        /// Geeft een item direct aan de inventaris van het geselecteerde personage (debug).
        /// Spiegelt debugGrantItem() in store.ts.
        /// </summary>
        public void DebugGrantItem(string itemId, int qty)
        {
            if (Account == null || SelectedCharacterId == null) return;
            if (!RuntimeContent.Items.TryGetValue(itemId, out ItemDef def)) return;

            CommitAccount(AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
            {
                var (updated, _) = Inventory.AddToInventory(
                    c, new ItemStack { ItemId = itemId, Qty = qty }, def);
                return updated;
            }), new CharacterMutationCommand("debug-grant-item", SelectedCharacterId), SessionEventKind.InventoryChanged);
        }

        /// <summary>Grants shared bank currency for development verification.</summary>
        public void DebugGrantCoins(int amount)
        {
            if (Account == null || amount <= 0) return;
            Account updated = Account.Clone();
            updated.Bank = BankHelper.AddCoins(updated.Bank, amount);
            CommitAccount(updated,
                new CharacterMutationCommand("debug-grant-coins", SelectedCharacterId),
                SessionEventKind.BankChanged);
        }

        /// <summary>
        /// Kent XP direct toe aan het geselecteerde personage (debug).
        /// Spiegelt debugGrantXp() in store.ts.
        /// </summary>
        public void DebugGrantXp(long amount)
        {
            if (Account == null || SelectedCharacterId == null) return;
            Account updated = AccountHelper.UpdateCharacter(
                Account, SelectedCharacterId, c => CharacterHelper.GainXp(c, amount));
            updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated,
                new CharacterMutationCommand("debug-grant-xp", SelectedCharacterId),
                SessionEventKind.CharacterChanged);
        }

        /// <summary>
        /// Wijst een activiteit toe aan het geselecteerde personage.
        /// Fouten (validatie) worden stil genegeerd.
        /// Spiegelt assign() in store.ts.
        /// </summary>
        public void Assign(ActivityKind kind, string targetId = null)
        {
            if (Account == null || SelectedCharacterId == null) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            try
            {
                CommitAccount(AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
                    Activity.AssignActivity(c, kind, targetId, _activityData, now, BonusesFor(c))),
                    new ChangeActivityCommand(SelectedCharacterId), SessionEventKind.ActivityChanged);
            }
            catch { /* validatiefout — stil negeren */ }
        }

        /// <summary>Stopt de huidige activiteit (stelt Idle in). Spiegelt stop().</summary>
        public void Stop()
        {
            _activeCombat?.ClearCharacterState(SelectedCharacterId);
            _activeCombatState = null;
            Assign(ActivityKind.Idle);
        }

        public bool StartActiveCombat(string monsterId)
        {
            if (!RuntimeContent.Monsters.ContainsKey(monsterId)) return false;
            Assign(ActivityKind.Fighting, monsterId);
            Character selected = SelectedCharacter();
            bool active = selected?.Activity?.Kind == ActivityKind.Fighting && selected.Activity.TargetId == monsterId;
            return active;
        }

        public void EnqueueCombatCommand(CombatCommand command)
        {
            if (command == null) return;
            _queuedCombatCommands.Add(new CombatCommand
            {
                Kind = command.Kind,
                TargetId = command.TargetId,
                SkillId = command.SkillId,
            });
        }

        public string SpawnLoot(string mapId, CombatTileCoordinate tile, int floor, List<ItemStack> stacks)
        {
            if (_lootDrops == null) return null;
            return _lootDrops.Spawn(mapId, tile, floor, stacks, AdvanceLootClock());
        }

        public LootPickupResult TryPickupLoot(string dropId)
            => TryPickupLoot(dropId, announceAttempt: true, vacuum: false);

        private LootPickupResult TryPickupLoot(string dropId, bool announceAttempt, bool vacuum)
        {
            if (_lootDrops == null || Account == null || string.IsNullOrWhiteSpace(SelectedCharacterId)) return null;
            LootPickupResult result = _lootDrops.TryPickup(Account, SelectedCharacterId, dropId, vacuum);
            // Only manual pickups announce their attempt: vacuum retries against a full
            // inventory would repeat the UI warning every retry cycle.
            if (announceAttempt) LootPickupAttempted?.Invoke(result);
            if (result != null && result.PickedStacks.Count > 0)
                CommitAccount(result.Account, new LootPickupCommand(SelectedCharacterId), SessionEventKind.LootPickedUp);
            if (result != null && result.RemainingStacks.Count == 0)
                _vacuumFailureAnnounced.Remove(dropId);
            return result;
        }

        private void DrainLootVacuum()
        {
            if (_lootDrops == null) return;
            List<string> due = _lootDrops.CollectDue(AdvanceLootClock(), Account?.AutoLoot ?? false);
            foreach (string dropId in due)
            {
                LootPickupResult result = TryPickupLoot(dropId, announceAttempt: false, vacuum: true);
                // A vacuum that leaves loot behind (full inventory) warns exactly once per
                // drop; later retries stay silent so the feed is not flooded.
                if (result != null && result.RemainingStacks.Count > 0 && _vacuumFailureAnnounced.Add(dropId))
                    LootPickupAttempted?.Invoke(result);
            }
        }

        private long AdvanceLootClock()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now > _lootClockMs) _lootClockMs = now;
            return _lootClockMs;
        }

        private void ClearLootMap(string mapId)
        {
            if (_lootDrops == null || string.IsNullOrWhiteSpace(mapId)) return;
            _lootDrops.ClearMap(mapId);
            _vacuumFailureAnnounced.Clear();
            LootCleared?.Invoke(mapId);
        }

        private void ClearAllLoot()
        {
            var mapIds = new HashSet<string>();
            foreach (Character character in Account?.Characters ?? new List<Character>())
                if (!string.IsNullOrWhiteSpace(character?.MapId)) mapIds.Add(character.MapId);
            foreach (string mapId in mapIds) ClearLootMap(mapId);
        }

        private void RelayLootSpawned(LootSpawnedEvent payload) => LootSpawned?.Invoke(payload);
        private void RelayLootPickedUp(LootPickedUpEvent payload) => LootPickedUp?.Invoke(payload);
        private void RelayLootExpired(LootExpiredEvent payload)
        {
            if (payload != null) _vacuumFailureAnnounced.Remove(payload.DropId);
            LootExpired?.Invoke(payload);
        }

        public ActiveCombatTickResult TickActiveCombat(
            string targetEntityId,
            string monsterId,
            CombatWorldFacts world,
            List<CombatCommand> commands,
            long timestampMs)
        {
            Character selected = SelectedCharacter();
            if (selected == null || _activeCombat == null)
                return PublishCombatResult(new ActiveCombatTickResult { RejectionReason = "missing_active_character" });
            if (selected.Activity?.Kind != ActivityKind.Fighting || selected.Activity.TargetId != monsterId)
                return PublishCombatResult(new ActiveCombatTickResult { RejectionReason = "combat_activity_not_selected" });

            long nowMs = timestampMs;
            if (_respawnAtMs > nowMs)
            {
                _queuedCombatCommands.Clear();
                return PublishCombatResult(new ActiveCombatTickResult { RejectionReason = "player_recovering" });
            }
            _respawnAtMs = 0;

            var combinedCommands = new List<CombatCommand>(_queuedCombatCommands);
            _queuedCombatCommands.Clear();
            if (commands != null) combinedCommands.AddRange(commands);

            // The account Auto toggle is also the player-facing switch between automatic
            // skill rotation and manual skill use. Basic attacks may continue against an
            // explicitly selected target, but auto skills must not consume cooldowns first.
            if (_activeCombat.Config != null)
                _activeCombat.Config.AutoSkillRotation = autoCombatPolicy.autoSkillRotation && AutoCombatEnabled;

            ActiveCombatTickResult result = _activeCombat.Tick(
                Account,
                selected.Id,
                targetEntityId,
                monsterId,
                world,
                combinedCommands,
                nowMs,
                BonusesFor(selected));

            _activeCombatState = result.Simulation?.State?.Clone();

            if (result.Reward != null)
            {
                for (int kill = 0; kill < Math.Max(1, result.Reward.KillCount); kill++)
                    _session.IncrementWorldKill(monsterId);
                Account updated = ResnapshotActive(result.Account, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                CommitAccount(updated, new ResolveCombatKillCommand(selected.Id), SessionEventKind.CombatRewardsResolved);
                _activeCombatState = _activeCombat.PrepareNextEncounter(selected.Id);
                PublishProgressionEvents(result.Reward);
            }
            else if (result.Simulation?.PlayerDefeated == true)
            {
                _activeCombat.ClearCharacterState(selected.Id);
                _activeCombatState = null;
                _queuedCombatCommands.Clear();
                _respawnAtMs = nowMs + Math.Max(0L, (long)Math.Ceiling(autoCombatPolicy.deathDowntimeSeconds * 1000f));
                if (AutoCombatEnabled)
                {
                    Account updated = Account.Clone();
                    updated.AutoCombatDisabled = true;
                    CommitAccount(updated, new CharacterMutationCommand("death-disable-auto-combat", selected.Id), SessionEventKind.AccountChanged);
                }
            }

            return PublishCombatResult(result);
        }

        public ActiveGatheringTickResult TickActiveGathering(
            string targetEntityId,
            string nodeId,
            GatheringWorldFacts world,
            List<GatheringCommand> commands)
        {
            Character selected = SelectedCharacter();
            if (selected == null || _activeGathering == null ||
                !ActivitySkillMapping.IsHarvest(selected.Activity?.Kind ?? ActivityKind.Idle) ||
                selected.Activity.TargetId != nodeId)
                return PublishGatheringResult(new ActiveGatheringTickResult { RejectionReason = "gathering_activity_not_selected" });

            ActiveGatheringTickResult result = _activeGathering.Tick(
                Account, selected.Id, targetEntityId, nodeId, world, commands,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), BonusesFor(selected));
            if (result.Simulation?.SuccessfulActions > 0)
            {
                Account updated = ResnapshotActive(result.Account, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                CommitAccount(updated, new CharacterMutationCommand("resolve-gathering", selected.Id), SessionEventKind.ResourceGathered);
                PublishProgressionEvents(result);
            }
            return PublishGatheringResult(result);
        }

        /// <summary>
        /// Laat het geselecteerde personage reizen naar een aangrenzende kaart.
        /// Verbindingscheck via Activity.Travel(); fouten worden stil genegeerd.
        /// Spiegelt travelTo() in store.ts.
        /// </summary>
        public bool TryTravelTo(string mapId)
        {
            if (Account == null || SelectedCharacterId == null) return false;
            string oldMapId = SelectedCharacter()?.MapId;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            try
            {
                CommitAccount(AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
                    Activity.Travel(c, mapId, _activityMaps, now)),
                    new CharacterMutationCommand("travel", SelectedCharacterId), SessionEventKind.ActivityChanged);
                ClearLootMap(oldMapId);
                return true;
            }
            catch { /* ongeldige reisbestemming — stil negeren */ }
            return false;
        }

        public void TravelTo(string mapId) => TryTravelTo(mapId);

        public bool CanTravelTo(string mapId)
        {
            if (Account == null || string.IsNullOrWhiteSpace(SelectedCharacterId)) return false;
            try
            {
                Activity.Travel(SelectedCharacter(), mapId, _activityMaps,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Verwerkt offline progressie (tijd verstreken sinds LastSeenAt).
        /// Spiegelt processOffline() in store.ts; gebruikt AccountBonuses.Zero() als offline bonussen.
        /// </summary>
        public void ProcessOffline()
        {
            if (Account == null) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // Spiegelt bonuses = ZERO_BONUSES uit store.ts — intentioneel nul voor offline
            var (newAccount, report) = Offline.SimulateOffline(
                Account,
                now,
                _offlineData,
                new SystemRandomSource(OfflineSeed.Derive(Account, now)),
                AccountBonuses.Zero());

            newAccount = ResnapshotActive(newAccount, now);
            var offlineCommand = new OfflineClaimCommand();
            Account preparedAccount = PrepareAccountForCommit(
                newAccount, offlineCommand, SessionEventKind.OfflineRewardsClaimed);

            if (SaveManager.Instance != null && !SaveManager.Instance.Save(
                    preparedAccount,
                    SelectedCharacterId,
                    _session.CopyWorldKills(),
                    _session.AccountRevision + 1))
                return;

            _session.Commit(preparedAccount, offlineCommand, SessionEventKind.OfflineRewardsClaimed);
            if (SaveManager.Instance != null) _session.MarkSaved();
            if (report != null) OfflineReport = report;
        }

        /// <summary>Verwijdert het offline-rapport nadat de speler het heeft gezien.</summary>
        public void ClaimOfflineReport()
        {
            OfflineReport = null;
        }

        /// <summary>
        /// Werkt LastSeenAt bij zodat de volgende offline-berekening correct begint.
        /// Wordt ~1×/s aangeroepen vanuit Update(). Spiegelt heartbeat() in store.ts.
        /// </summary>
        public void Heartbeat()
        {
            if (Account == null) return;
            Account updated = Account.Clone();
            updated.LastSeenAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            CommitAccount(updated, new CharacterMutationCommand("heartbeat", SelectedCharacterId), SessionEventKind.AccountChanged);
        }

        /// <summary>
        /// Trekt een aantal uren af van LastSeenAt en verwerkt dan direct de offline progressie.
        /// Spiegelt debugTimeTravel() in store.ts.
        /// </summary>
        public void DebugTimeTravel(double hours)
        {
            if (Account == null) return;
            Account updated = Account.Clone();
            updated.LastSeenAt -= (long)(hours * 3_600_000);
            CommitAccount(updated, new CharacterMutationCommand("debug-time-travel", SelectedCharacterId), SessionEventKind.AccountChanged);
            ProcessOffline();
        }

        /// <summary>
        /// Craft een recept: materialen uit bank + inventaris, munten uit de bank,
        /// resultaat naar de inventaris. Fouten (level, materialen, inventaris vol) worden stil genegeerd.
        /// Bewuste afwijking van craftRecipe() in store.ts (zie plan F_0.7.0, Scope Change).
        /// </summary>
        public void CraftRecipe(string recipeId)
        {
            if (Account == null || SelectedCharacterId == null) return;
            if (!RuntimeContent.Recipes.TryGetValue(recipeId, out RecipeDef recipe)) return;
            var character = SelectedCharacter();
            if (character == null) return;
            try
            {
                Account updated = Account.Clone();
                var crafted = Crafting.Craft(
                    updated.Bank,
                    character,
                    recipe,
                    character.Level,
                    new Dictionary<string, ItemDef>(RuntimeContent.Items));
                updated.Bank = crafted.Bank;
                updated = AccountHelper.UpdateCharacter(updated, SelectedCharacterId, _ => crafted.Character);
                CommitAccount(updated, new CharacterMutationCommand("craft", SelectedCharacterId), SessionEventKind.CharacterChanged);
                CraftCompleted?.Invoke(new ItemStack { ItemId = recipe.OutputItemId, Qty = recipe.OutputQty });
            }
            catch { /* UI handelt pre-validatie af — stil negeren */ }
        }

        /// <summary>
        /// Ruist een item uit de inventaris uit. Vernieuwt de efficiency-snapshot indien actief.
        /// Spiegelt equip() in store.ts.
        /// </summary>
        public void EquipItem(string itemId)
        {
            if (Account == null || SelectedCharacterId == null) return;
            if (!RuntimeContent.Items.TryGetValue(itemId, out ItemDef def)) return;
            try
            {
                Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
                    Equip.EquipItem(c, itemId, def, new Dictionary<string, ItemDef>(RuntimeContent.Items)));
                updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                CommitAccount(updated,
                    new CharacterMutationCommand("equip-item", SelectedCharacterId), SessionEventKind.CharacterChanged);
            }
            catch { /* level niet gehaald, inventaris vol — stil negeren */ }
        }

        /// <summary>
        /// Legt het item in een slot terug in de inventaris. Vernieuwt de efficiency-snapshot indien actief.
        /// Spiegelt unequip() in store.ts.
        /// </summary>
        public void UnequipItem(EquipSlot slot)
        {
            if (Account == null || SelectedCharacterId == null) return;
            try
            {
                Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
                    Equip.UnequipItem(c, slot, new Dictionary<string, ItemDef>(RuntimeContent.Items)));
                updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                CommitAccount(updated,
                    new CharacterMutationCommand("unequip-item", SelectedCharacterId), SessionEventKind.CharacterChanged);
            }
            catch { /* slot leeg, inventaris vol — stil negeren */ }
        }

        /// <summary>Assigns a class skill to a bar slot, relocating any existing occurrence.</summary>
        public void AssignSkillToBar(string skillId, int slotIndex)
        {
            Character selected = SelectedCharacter();
            if (selected == null || !IsSkillBarSlot(slotIndex) || !IsClassSkill(selected.ClassId, skillId) ||
                !SkillBuild.IsUnlocked(selected, skillId)) return;

            Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId, character =>
            {
                Character result = character.Clone();
                List<string> skillBar = CopySkillBar(character.SkillBar);
                for (int index = 0; index < skillBar.Count; index++)
                    if (index != slotIndex && skillBar[index] == skillId)
                        skillBar[index] = null;
                skillBar[slotIndex] = skillId;
                result.SkillBar = skillBar;
                return result;
            });
            updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated,
                new CharacterMutationCommand("assign-skillbar-slot", SelectedCharacterId), SessionEventKind.SkillSlotChanged);
        }

        /// <summary>Swaps two skillbar slots, including swaps with empty slots.</summary>
        public void SwapSkillBarSlots(int firstSlotIndex, int secondSlotIndex)
        {
            Character selected = SelectedCharacter();
            if (selected == null || !IsSkillBarSlot(firstSlotIndex) || !IsSkillBarSlot(secondSlotIndex) ||
                firstSlotIndex == secondSlotIndex) return;

            Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId, character =>
            {
                Character result = character.Clone();
                List<string> skillBar = CopySkillBar(character.SkillBar);
                string firstSkill = skillBar[firstSlotIndex];
                skillBar[firstSlotIndex] = skillBar[secondSlotIndex];
                skillBar[secondSlotIndex] = firstSkill;
                result.SkillBar = skillBar;
                return result;
            });
            updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated,
                new CharacterMutationCommand("swap-skillbar-slots", SelectedCharacterId), SessionEventKind.SkillSlotChanged);
        }

        /// <summary>Clears the selected character's skillbar slot.</summary>
        public void ClearSkillBarSlot(int slotIndex)
        {
            Character selected = SelectedCharacter();
            if (selected == null || !IsSkillBarSlot(slotIndex)) return;

            Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId, character =>
            {
                Character result = character.Clone();
                List<string> skillBar = CopySkillBar(character.SkillBar);
                skillBar[slotIndex] = null;
                result.SkillBar = skillBar;
                return result;
            });
            updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated,
                new CharacterMutationCommand("clear-skillbar-slot", SelectedCharacterId), SessionEventKind.SkillSlotChanged);
        }

        public void UnlockSkill(string skillId)
        {
            Character selected = SelectedCharacter();
            ClassSkillDef skill = selected == null ? null : FindClassSkill(selected.ClassId, skillId);
            if (!SkillBuild.CanUnlock(selected, skill)) return;
            Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId,
                character => SkillBuild.Unlock(character, skill));
            updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated,
                new CharacterMutationCommand("unlock-skill", SelectedCharacterId), SessionEventKind.SkillUnlocked);
        }

        public void DevelopmentRespecSkills()
        {
            Character selected = SelectedCharacter();
            if (selected == null) return;
            Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId,
                SkillBuild.DevelopmentRespec);
            updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated,
                new CharacterMutationCommand("development-respec-skills", SelectedCharacterId),
                SessionEventKind.SkillUnlocked);
        }

        /// <summary>
        /// Verdeelt één talent-punt. Herberekent efficiency-snapshots van actieve personages.
        /// Spiegelt allocateTalentAction() in store.ts.
        /// </summary>
        public void AllocateTalent(string talentId)
        {
            if (Account == null || SelectedCharacterId == null) return;
            if (!RuntimeContent.Talents.TryGetValue(talentId, out TalentDef def)) return;
            try
            {
                Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
                    Talents.AllocateTalent(c, def));
                updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                CommitAccount(updated, new CharacterMutationCommand("allocate-talent", SelectedCharacterId), SessionEventKind.CharacterChanged);
            }
            catch { /* geen punten / al max — UI handelt pre-validatie af */ }
        }

        /// <summary>
        /// Geeft alle talent-punten terug. Herberekent efficiency-snapshots.
        /// Spiegelt resetTalentsAction() in store.ts.
        /// </summary>
        public void ResetTalents()
        {
            if (Account == null || SelectedCharacterId == null) return;
            Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
                Talents.ResetTalents(c));
            updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated, new CharacterMutationCommand("reset-talents", SelectedCharacterId), SessionEventKind.CharacterChanged);
        }

        /// <summary>
        /// Schakelt de account-brede auto-loot-vlag om.
        /// Spiegelt toggleAutoLootAction() in store.ts.
        /// </summary>
        public void ToggleAutoLoot()
        {
            if (Account == null) return;
            Account updated = Account.Clone();
            updated.AutoLoot = !updated.AutoLoot;
            CommitAccount(updated, new CharacterMutationCommand("toggle-auto-loot", SelectedCharacterId), SessionEventKind.AccountChanged);
        }

        public void ToggleAutoCombat()
        {
            if (Account == null) return;
            Account updated = Account.Clone();
            updated.AutoCombatDisabled = !updated.AutoCombatDisabled;
            CommitAccount(updated, new CharacterMutationCommand("toggle-auto-combat", SelectedCharacterId), SessionEventKind.AccountChanged);
        }

        /// <summary>
        /// Geeft één gratis stat-punt uit. Herberekent efficiency-snapshots.
        /// stat: "strength" | "agility" | "wisdom" | "luck"
        /// Spiegelt allocateStatAction() in store.ts.
        /// </summary>
        public void AllocateStat(string stat)
        {
            if (Account == null || SelectedCharacterId == null) return;
            Account updated = AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
                CharacterHelper.AllocateStatPoint(c, stat));
            updated = ResnapshotActive(updated, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            CommitAccount(updated, new CharacterMutationCommand("allocate-stat", SelectedCharacterId), SessionEventKind.CharacterChanged);
        }

        /// <summary>
        /// Markeert een waypoint als ontdekt en opent het snel-reismenu.
        /// Spiegelt discoverWaypoint() in store.ts.
        /// </summary>
        public void DiscoverWaypoint(string waypointId)
        {
            if (Account == null) return;
            Account updated = Account.Clone();
            updated.UnlockedWaypoints ??= new List<string>();
            if (!updated.UnlockedWaypoints.Contains(waypointId))
                updated.UnlockedWaypoints.Add(waypointId);
            CommitAccount(updated, new CharacterMutationCommand("discover-waypoint", SelectedCharacterId), SessionEventKind.AccountChanged);
            ActiveWaypointMenu = waypointId;
        }

        /// <summary>Sluit het waypoint snel-reismenu. Spiegelt closeWaypointMenu().</summary>
        public void CloseWaypointMenu()
        {
            ActiveWaypointMenu = null;
        }

        /// <summary>
        /// Opent de hub-service-lade voor de gegeven service.
        /// Spiegelt openHubService() in store.ts.
        /// </summary>
        public void OpenHubService(HubServiceKind kind)
        {
            ActiveHubService = kind;
        }

        /// <summary>Sluit de hub-service-lade. Spiegelt closeHubService().</summary>
        public void CloseHubService()
        {
            ActiveHubService = null;
        }

        /// <summary>
        /// Teleporteert het geselecteerde personage direct naar een ontdekt waypoint
        /// (omzeilt verbindingscheck). Sluit het waypointmenu. Spiegelt warpToWaypoint().
        /// </summary>
        public bool TryWarpToWaypoint(string toMapId)
        {
            if (!CanWarpToWaypoint(toMapId)) return false;
            string oldMapId = SelectedCharacter()?.MapId;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            CommitAccount(AccountHelper.UpdateCharacter(Account, SelectedCharacterId, c =>
            {
                var updated       = c.Clone();
                updated.MapId     = toMapId;
                updated.Activity  = new ActivityState { Kind = ActivityKind.Idle, StartedAt = now };
                updated.Efficiency = null;
                return updated;
            }), new CharacterMutationCommand("warp", SelectedCharacterId), SessionEventKind.ActivityChanged);
            ClearLootMap(oldMapId);
            ActiveWaypointMenu = null;
            return true;
        }

        public void WarpToWaypoint(string toMapId) => TryWarpToWaypoint(toMapId);

        public bool CanWarpToWaypoint(string toMapId)
        {
            if (Account == null || string.IsNullOrWhiteSpace(SelectedCharacterId) ||
                !_activityMaps.ContainsKey(toMapId)) return false;
            return Account.UnlockedWaypoints != null && Account.UnlockedWaypoints.Contains(toMapId);
        }

        /// <summary>Geeft het geselecteerde personage terug (null als geen selectie).</summary>
        public Character GetSelectedCharacter() => SelectedCharacter();

        private ActiveCombatTickResult PublishCombatResult(ActiveCombatTickResult result)
        {
            ActiveCombatResolved?.Invoke(result);
            return result;
        }

        private ActiveGatheringTickResult PublishGatheringResult(ActiveGatheringTickResult result)
        {
            ActiveGatheringResolved?.Invoke(result);
            return result;
        }

        private void PublishProgressionEvents(CombatReward reward)
        {
            XpAwarded?.Invoke(new XpAwardedEvent
            {
                CharacterXp = reward.CharacterXp,
                SkillXp = reward.CombatSkillXp,
                SkillId = SkillId.Combat,
            });
            PublishLevelUpEvents(
                LevelUpKind.Character,
                null,
                reward.CharacterPreviousLevel,
                reward.CharacterNewLevel);
            PublishLevelUpEvents(
                LevelUpKind.Skill,
                SkillId.Combat,
                reward.CombatSkillPreviousLevel,
                reward.CombatSkillNewLevel);
        }

        private void PublishProgressionEvents(ActiveGatheringTickResult result)
        {
            XpAwarded?.Invoke(new XpAwardedEvent
            {
                CharacterXp = 0,
                SkillXp = result.Simulation.SkillXp,
                SkillId = result.SkillId,
            });
            PublishLevelUpEvents(
                LevelUpKind.Skill,
                result.SkillId,
                result.SkillPreviousLevel,
                result.SkillNewLevel);
        }

        private void PublishLevelUpEvents(LevelUpKind kind, SkillId? skillId, int previousLevel, int newLevel)
        {
            for (int level = previousLevel + 1; level <= newLevel; level++)
                LevelUp?.Invoke(new LevelUpEvent
                {
                    Kind = kind,
                    SkillId = skillId,
                    PreviousLevel = level - 1,
                    NewLevel = level,
                });
        }

        // ── Private hulp-acties ───────────────────────────────────────────────────

    }
}
