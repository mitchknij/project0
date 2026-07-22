// SaveManager.cs — Persistente save-laag (spiegelt de Zustand persist-config in store.ts).
// MonoBehaviour singleton; gebruikt Newtonsoft JSON voor volledige Dictionary/nullable-serialisatie.

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using Newtonsoft.Json;
using IdleCloud.Core;
using IdleCloud.Data;

[assembly: InternalsVisibleTo("IdleCloud.Tests.EditMode")]

namespace IdleCloud.Managers
{
    /// <summary>
    /// DTO: bevat alleen de velden die naar schijf worden geschreven — spiegelt de
    /// `partialize`-stap van Zustand: { account, selectedCharacterId, worldKills }.
    /// </summary>
    public class SaveData
    {
        public int SaveSchemaVersion;
        public string ContentVersion;
        public Account Account;
        public string SelectedCharacterId;
        public Dictionary<string, int> WorldKills;
        public long SessionRevision;
    }

    /// <summary>
    /// Beheert het laden en opslaan van de game-state.
    /// Maakt gebruik van Newtonsoft JSON zodat Dictionary- en nullable-velden correct
    /// geserialiseerd worden (JsonUtility kan dat niet).
    /// Spiegelt de persist-configuratie met naam 'idlecloud-save-v1' uit store.ts.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public const int CurrentSaveSchemaVersion = 4;
        // ── Singleton ─────────────────────────────────────────────────────────────

        public static SaveManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Constanten ────────────────────────────────────────────────────────────

        private const string SaveFileName = "idlecloud-save-v1.json";
        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        // Hergebruik settings-instantie om GC te vermijden
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            // Enum-keyed dicts (bijv. Dictionary<SkillId,…>) vereisen StringEnumConverter
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
            NullValueHandling   = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        // ── Publieke API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Schrijft de huidige state naar schijf.
        /// Serialiseert alleen de gepartitioneerde velden (account, selectedCharacterId, worldKills).
        /// </summary>
        public bool Save(GameSession session)
        {
            if (session == null) return false;
            return Save(session.Account, session.SelectedCharacterId, session.CopyWorldKills(), session.AccountRevision);
        }

        public bool Save(Account account, string selectedCharacterId, Dictionary<string, int> worldKills, long sessionRevision = 0)
        {
            return SaveToPath(SavePath, account, selectedCharacterId, worldKills, sessionRevision);
        }

        internal static bool SaveToPath(
            string savePath,
            Account account,
            string selectedCharacterId,
            Dictionary<string, int> worldKills,
            long sessionRevision = 0)
        {
            if (account == null || string.IsNullOrWhiteSpace(savePath)) return false;

            var saveData = new SaveData
            {
                SaveSchemaVersion     = CurrentSaveSchemaVersion,
                ContentVersion        = SnapshotValidation.CurrentContentVersion,
                Account               = account,
                SelectedCharacterId   = selectedCharacterId,
                WorldKills            = worldKills ?? new Dictionary<string, int>(),
                SessionRevision       = sessionRevision,
            };

            try
            {
                string json = SerializeData(saveData);
                string temporaryPath = savePath + ".tmp";
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(savePath)) File.Replace(temporaryPath, savePath, null);
                else File.Move(temporaryPath, savePath);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveManager] Save failed: {e.Message}");
                return false;
            }
        }

        internal static string SerializeData(SaveData saveData)
        {
            return JsonConvert.SerializeObject(saveData, Formatting.None, JsonSettings);
        }

        /// <summary>
        /// Laadt de save van schijf en voert rehydratie uit.
        /// Rehydratie (spiegelt onRehydrateStorage in store.ts): elk personage met een
        /// mapId die niet in MapsRepo.All staat valt terug op MapsRepo.StartingMapId.
        /// Geeft null terug als er geen save-bestand is of als het bestand corrupt is.
        /// </summary>
        public SaveData Load()
        {
            return LoadFromPath(SavePath);
        }

        internal static SaveData LoadFromPath(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath) || !File.Exists(savePath)) return null;

            try
            {
                string json  = File.ReadAllText(savePath);
                var saveData = JsonConvert.DeserializeObject<SaveData>(json, JsonSettings);
                if (saveData?.Account == null) return null;

                int sourceSchemaVersion = System.Math.Max(0, saveData.SaveSchemaVersion);
                if (sourceSchemaVersion < CurrentSaveSchemaVersion)
                    BackupSaveBeforeMigration(savePath, sourceSchemaVersion);
                MigrateLoadedData(saveData);
                ReportLoadInvariants(saveData.Account);

                // Rehydratie: corrigeer ongeldige mapId's
                var characters = saveData.Account.Characters ?? new System.Collections.Generic.List<Character>();
                for (int i = 0; i < characters.Count; i++)
                {
                    if (!RuntimeContent.Maps.ContainsKey(characters[i].MapId))
                    {
                        // Clone om het originele object niet te muteren
                        var c        = characters[i].Clone();
                        c.MapId      = MapsRepo.StartingMapId;
                        characters[i] = c;
                    }
                }
                saveData.Account.Characters = characters;

                return saveData;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveManager] Load failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Applies non-destructive, ordered save migrations. Kept public so migration
        /// fixtures can verify the exact persisted-state contract without a scene.
        /// </summary>
        public static SaveData MigrateLoadedData(SaveData saveData)
        {
            if (saveData?.Account == null) return saveData;

            int sourceSchemaVersion = System.Math.Max(0, saveData.SaveSchemaVersion);
            if (sourceSchemaVersion < 1)
                NormalizeLegacyCollections(saveData.Account);

            if (sourceSchemaVersion < 2)
                NormalizeLegacySnapshotMetadata(saveData.Account);

            // Normalization is idempotent and also protects partially-written older saves.
            NormalizeLegacyCollections(saveData.Account);
            NormalizeLegacySnapshotMetadata(saveData.Account);
            if (sourceSchemaVersion < 3)
                SeedMissingSkillBars(saveData.Account);
            NormalizeSkillBars(saveData.Account);
            NormalizeSkillBuildState(saveData.Account);
            saveData.SaveSchemaVersion = CurrentSaveSchemaVersion;
            saveData.ContentVersion = SnapshotValidation.CurrentContentVersion;
            return saveData;
        }

        /// <summary>Verwijdert het save-bestand (voor ResetSave).</summary>
        public void DeleteSave()
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); }
            catch (System.Exception e) { Debug.LogWarning($"[SaveManager] DeleteSave failed: {e.Message}"); }
        }

        private static void NormalizeLegacyCollections(Account account)
        {
            account.Characters ??= new List<Character>();
            account.Bank ??= new Bank { MaxSlots = 48 };
            account.Bank.Slots ??= new List<ItemStack>();
            account.UnlockedWaypoints ??= new List<string>();

            foreach (Character character in account.Characters)
            {
                if (character == null) continue;
                character.Skills ??= new Dictionary<SkillId, SkillProgress>();
                foreach (SkillId skill in CharacterHelper.AllSkills)
                    if (!character.Skills.ContainsKey(skill))
                        character.Skills.Add(skill, new SkillProgress { Level = 1, Xp = 0 });
                character.Equipment ??= new Dictionary<EquipSlot, string>();
                character.Inventory ??= new List<ItemStack>();
                character.Talents ??= new Dictionary<string, int>();
                character.Activity ??= new ActivityState { Kind = ActivityKind.Idle, StartedAt = account.LastSeenAt };
            }
        }

        private static void NormalizeLegacySnapshotMetadata(Account account)
        {
            foreach (Character character in account.Characters)
            {
                if (character?.Efficiency == null) continue;
                bool legacySnapshot = character.Efficiency.ContentVersion != SnapshotValidation.CurrentContentVersion;
                character.Efficiency.ContentVersion ??= SnapshotValidation.CurrentContentVersion;
                if (character.Efficiency.MapDensity <= 0.0) character.Efficiency.MapDensity = 1.0;
                if (legacySnapshot && character.Efficiency.SurvivalFactor <= 0.0)
                    character.Efficiency.SurvivalFactor = 1.0;
                character.Efficiency.DebugBreakdown ??= "migrated=legacy";
            }
        }

        private static void SeedMissingSkillBars(Account account)
        {
            foreach (Character character in account.Characters ?? new List<Character>())
            {
                if (character != null && character.SkillBar == null)
                    character.SkillBar = CharacterHelper.CreateDefaultSkillBar(character.ClassId);
            }
        }

        private static void NormalizeSkillBars(Account account)
        {
            foreach (Character character in account.Characters ?? new List<Character>())
            {
                if (character == null) continue;

                var classSkillIds = new HashSet<string>();
                ClassDef classDef = RuntimeContent.Get(character.ClassId);
                foreach (ClassSkillDef skill in classDef?.Skills ?? new List<ClassSkillDef>())
                    if (skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                        classSkillIds.Add(skill.Id);

                var normalized = new List<string>(Character.SkillBarSlots);
                var assignedSkillIds = new HashSet<string>();
                for (int slot = 0; slot < Character.SkillBarSlots; slot++)
                {
                    string skillId = character.SkillBar != null && slot < character.SkillBar.Count
                        ? character.SkillBar[slot]
                        : null;
                    normalized.Add(skillId != null && classSkillIds.Contains(skillId) && assignedSkillIds.Add(skillId)
                        ? skillId
                        : null);
                }
                character.SkillBar = normalized;
            }
        }

        private static void NormalizeSkillBuildState(Account account)
        {
            foreach (Character character in account.Characters ?? new List<Character>())
            {
                if (character == null) continue;
                var classSkillIds = new HashSet<string>();
                foreach (ClassSkillDef skill in RuntimeContent.Get(character.ClassId)?.Skills ?? new List<ClassSkillDef>())
                    if (skill != null && !string.IsNullOrWhiteSpace(skill.Id)) classSkillIds.Add(skill.Id);

                var unlocked = new List<string>();
                foreach (string skillId in character.UnlockedSkillIds ?? new List<string>())
                    if (classSkillIds.Contains(skillId) && !unlocked.Contains(skillId)) unlocked.Add(skillId);
                foreach (string skillId in character.SkillBar ?? new List<string>())
                    if (classSkillIds.Contains(skillId) && !unlocked.Contains(skillId)) unlocked.Add(skillId);

                bool missingPrototypeState = character.SkillStateSchemaVersion < SkillBuild.CurrentSchemaVersion;
                character.UnlockedSkillIds = unlocked;
                character.AvailableSkillPoints = System.Math.Max(0, character.AvailableSkillPoints);
                character.SpentSkillPoints = System.Math.Max(0, character.SpentSkillPoints);
                if (missingPrototypeState && character.AvailableSkillPoints == 0 && character.SpentSkillPoints == 0)
                    character.AvailableSkillPoints = SkillBuild.PrototypeStartingPoints;
                character.SkillStateSchemaVersion = SkillBuild.CurrentSchemaVersion;
            }
        }

        private static void BackupSaveBeforeMigration(string savePath, int sourceSchemaVersion)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                string backupPath = savePath + $".pre-v{sourceSchemaVersion}.bak";
                if (!File.Exists(backupPath)) File.Copy(savePath, backupPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveManager] Save backup before migration failed: {e.Message}");
            }
#endif
        }

        private static void ReportLoadInvariants(Account account)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (string issue in StateInvariantValidator.Validate(account))
                Debug.LogWarning($"[SaveManager] Loaded-state invariant: {issue}");
#endif
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnApplicationPause(bool pause)
        {
            if (pause && GameManager.Instance != null)
                GameManager.Instance.RequestSave();
        }

        private void OnApplicationQuit()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.RequestSave();
        }
    }
}
