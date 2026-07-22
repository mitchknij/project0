// GameTypes.cs — Centrale datacontracten voor IdleCloud (vertaling van src/core/types.ts).
// Pure POCO's: geen MonoBehaviour, geen engine-imports.
// Compatibility namespace: these contracts now belong to the IdleCloud.Data
// assembly. Keeping the existing namespace avoids a save-facing API break while
// callers migrate to narrower Data namespaces in later change sets.
#pragma warning disable UAC1001, UAC1009 // Dictionary/nullable/jagged-array velden worden via JSON geserialiseerd, niet via Unity Inspector

using System.Collections.Generic;

namespace IdleCloud.Core
{
    // ── Enums (string-unions uit TS) ────────────────────────────────────────

    public enum ItemType { Currency, Material, Equipment, Consumable }

    public enum EquipSlot { Weapon, Helmet, Chest, Legs, Tool }

    public enum ClassId { Beginner, Warrior, Archer, Mage }

    /// <summary>Elementaire schadesoort — gebruikt door skills en monsters.</summary>
    public enum Element { Physical, Fire, Ice, Nature, Arcane }

    /// <summary>Status-effect dat kan worden opgelegd door skills/monster-abilities.</summary>
    public enum StatusKind { Burn, Poison, Chill, Stun }

    /// <summary>Stat die wordt beïnvloed door een self-buff skill.</summary>
    public enum BuffStat { Damage, Haste, Defense, Lifesteal }

    /// <summary>Mechanisch archetype van een actieve klasseskill.</summary>
    public enum SkillMechanic { Melee, Aoe, Projectile, Debuff, Buff }

    /// <summary>AI-gedragsarchetype voor een monster. Standaard Melee wanneer niet opgegeven.</summary>
    public enum MonsterBehavior { Melee, Kiter, Charger, Caster }

    public enum ActivityKind { Idle, Fighting, Mining, Chopping, Gathering }

    public enum SkillId { Combat, Mining, Chopping, Gathering }

    /// <summary>Stat-sleutels die meta-systemen (talents) aanpassen — spiegelt AccountBonuses.</summary>
    public enum BonusStat
    {
        Strength, Agility, Wisdom, Luck,
        HpPct, DropPct, XpPct,
        CombatPct, MiningPct, ChoppingPct, GatheringPct
    }

    public enum HubServiceKind { Bank, Shop, Crafting, Teleport, Blacksmith }

    public enum BiomeTheme { Grassland, Desert, Snow, Volcanic, Forest, Cave }

    public enum MapKind { Hub, Wild }

    public enum WarpKind { Portal, Waypoint }

    /// <summary>Skill die een ResourceNode vereist.</summary>
    public enum HarvestSkill { Mining, Chopping, Gathering }

    // ── Gedeelde data-shapes ──────────────────────────────────────────────────

    [System.Serializable]
    public class CoreStats
    {
        public int Strength; // max schade / mijnbouw-efficiëntie
        public int Agility;  // beweging / nauwkeurigheid
        public int Wisdom;   // mana / hakefficiëntie
        public int Luck;     // dropkans / zeldzame rolls
    }

    [System.Serializable]
    public class ItemStack
    {
        public string ItemId;
        public int Qty;
    }

    [System.Serializable]
    public class ItemDef
    {
        public string Id;
        public string Name;
        public ItemType Type;
        public int StackLimit;   // max aantal per bank/inventaris-slot
        public int SellValue;    // coins
        public EquipSlot? Slot;  // alleen voor equipment
        public CoreStats Bonuses;  // vlakke stat-bonussen terwijl uitgerust (null = geen)
        public int? LevelReq;    // alleen voor equipment
    }

    // ── Combat-elementen, status-effecten, buff-stats, skill/AI-gedrag ────────

    [System.Serializable]
    public class StatusInflict
    {
        public StatusKind Kind;
        public int DurationMs;
        public double Magnitude;
        public int TickIntervalMs = 500;
    }

    [System.Serializable]
    public class SelfBuff
    {
        public BuffStat Stat;
        public double Magnitude;
        public int DurationMs;
    }

    [System.Serializable]
    public class TilePatternDef
    {
        /// <summary>Tile patterns are unrotated until authoritative rotation input exists.</summary>
        public TilePatternKind PatternKind = TilePatternKind.SingleTile;
        public int Size = 1;
        public List<CombatTileCoordinate> CustomOffsets;
        public TilePatternFloorPolicy FloorPolicy = TilePatternFloorPolicy.SameFloor;

        // Zero means unlimited. MaxTargets lives here because the current skill model has no shared cap.
        public int MaxTargets;

        /// <summary>Maximum absolute X/Y offset accepted by content validation.</summary>
        public const int MaxSafeOffsetMagnitude = 32;
    }

    [System.Serializable]
    public class ClassSkillDef
    {
        public string Id;
        public string Name;
        public string Description;
        public Element Element;           // physical/fire/ice/nature/arcane
        public double DamageMultiplier;   // 0 voor pure buff-skills
        public int AoeColor;              // hex-tint als int, bv. 0xffdd44 (presentatie)
        public int Priority;              // auto-combat prioriteit (1..3)
        public double CooldownMs;
        public SkillMechanic Mechanic;
        public int? RangePx;
        public int? AoeRadiusPx;
        public double? ProjectileSpeed;
        public double? ProjectileRadius;
        public List<StatusInflict> Inflicts;  // status-effecten per treffer
        public SelfBuff Buff;                 // self-buff (alleen Buff-mechanic)
        public SkillTargetingKind Targeting = SkillTargetingKind.HostileActor;
        public TilePatternDef TilePattern;
        public bool? AutoEnabled;
        public int MinimumAutoTargets = 1;
        public double RadiusWorldUnits;
        public CombatStatProperty ModifierProperty;
        public CombatModifierOperation ModifierOperation;
        public double ModifierMagnitude;
        public long ModifierDurationTicks;
        public SkillTimingKind Timing = SkillTimingKind.Immediate;
        public long ImpactDelayTicks;
        public string BranchId = "Class";
        public int Tier = 1;
        public int SkillPointCost = 1;
        public string PrerequisiteSkillId;
    }

    [System.Serializable]
    public class PassiveMultipliers
    {
        public double? Hp;            // multiplier op max HP (bv. 1.30 = +30%)
        public double? Luck;          // multiplier op effectieve luck-stat
        public double? Xp;            // multiplier op gewonnen XP per actie
        public double? MiningEff;     // multiplier op mijnbouw-oogsten/uur
        public double? ChoppingEff;   // multiplier op hak-oogsten/uur
        public double? GatheringEff;  // multiplier op verzamelen-oogsten/uur
        public double? DamagePct;     // additief schade-%-bonus (0.10 = +10%)
        public double? CritPct;       // additief krit-kans-bonus (0.08 = +8%)
        public int? DefenseFlat;      // vlakke verdediging toegevoegd aan speler
        public double? Lifesteal;     // fractie uitgedeelde schade teruggewonnen als HP
    }

    [System.Serializable]
    public class PassiveSkillDef
    {
        public string Name;           // bv. "Iron Resolve"
        public string Description;
        public PassiveMultipliers Multipliers;
    }

    /// <summary>
    /// Groeisnelheid per level per stat (fractie van een stat-punt).
    /// Verschilt van CoreStats (int); hieronder kunnen waarden als 0.4, 0.9 worden opgeslagen.
    /// </summary>
    [System.Serializable]
    public class StatGrowthDef
    {
        public double Strength;
        public double Agility;
        public double Wisdom;
        public double Luck;
    }

    [System.Serializable]
    public class ClassDef
    {
        public ClassId Id;
        public string Name;
        public string Description;
        public string PassiveBonus;   // korte UI-samenvatting passief
        public CoreStats BaseStats;
        public StatGrowthDef StatGrowth; // groei per niveau als double (bv. 0.4, 0.9)
        public PassiveSkillDef Passive;
        public List<ClassSkillDef> Skills; // 3 per klasse, prioriteitsgeordend door auto-combat
    }

    [System.Serializable]
    public class ActivityState
    {
        public ActivityKind Kind;
        public string TargetId;   // monster- of resource-node-id
        public long StartedAt;    // epoch ms — anker voor offline-berekening
    }

    [System.Serializable]
    public class SkillProgress
    {
        public int Level;
        public long Xp; // huidig XP binnen het niveau
    }

    [System.Serializable]
    public partial class Character
    {
        public const int SkillBarSlots = 8;

        /// <summary>
        /// How many leading bar slots <c>CreateDefaultSkillBar</c> pre-fills. Deliberately
        /// decoupled from <see cref="SkillBarSlots"/>: every seeded skill is also granted for
        /// free (<c>SkillBuild.DefaultUnlocked</c>), so widening the bar must never widen the
        /// free grant — that would bypass the skill-point progression entirely.
        /// </summary>
        public const int DefaultSeededSkillBarSlots = 4;

        public string Id;
        public string Name;
        public ClassId ClassId;
        public int Level;
        public long Xp;                                   // huidig XP binnen niveau
        public Dictionary<SkillId, SkillProgress> Skills;
        public Dictionary<EquipSlot, string> Equipment;  // itemId per slot
        public List<ItemStack> Inventory;
        public int MaxInventorySlots;
        public string MapId;
        public ActivityState Activity;
        public EfficiencySnapshot Efficiency;             // null terwijl idle
        public long CharacterRevision;
        public long ActivityRevision;
        public Dictionary<string, int> Talents;           // talentId → punten
        public int? FreeStatPoints;
        public CoreStats AllocatedStats;                  // null = geen handmatige allocatie

        /// <summary>
        /// Manual skill loadout: exactly <see cref="SkillBarSlots"/> entries, where each
        /// non-null value is a skill id from this character's class.
        /// </summary>
        public List<string> SkillBar;
        public List<string> UnlockedSkillIds;
        public int AvailableSkillPoints;
        public int SpentSkillPoints;
        public int SkillStateSchemaVersion;
    }

    [System.Serializable]
    public class Bank
    {
        public int Coins;
        public List<ItemStack> Slots;
        public int MaxSlots;

        /// <summary>
        /// Ondiepe kopie met verse Slots-list zodat mutations de originele instantie
        /// niet beïnvloeden (equivalent aan TS spread { ...bank, slots }).
        /// </summary>
        public Bank Clone() => new Bank
        {
            Coins    = Coins,
            Slots    = CloneStacks(Slots),
            MaxSlots = MaxSlots,
        };

        private static List<ItemStack> CloneStacks(List<ItemStack> source)
        {
            var result = new List<ItemStack>();
            if (source == null) return result;
            foreach (var stack in source)
                if (stack != null)
                    result.Add(new ItemStack { ItemId = stack.ItemId, Qty = stack.Qty });
            return result;
        }
    }

    [System.Serializable]
    public class Account
    {
        public string Id;
        public string Name;         // familienaam
        public long CreatedAt;
        public List<Character> Characters;
        public int MaxCharacters;
        public Bank Bank;
        public long LastSeenAt;
        public List<string> UnlockedWaypoints;
        public bool AutoLoot;
        public bool AutoCombatDisabled;

        public Account Clone()
        {
            var characters = new List<Character>();
            if (Characters != null)
                foreach (var character in Characters)
                    if (character != null) characters.Add(character.Clone());

            return new Account
            {
                Id = Id,
                Name = Name,
                CreatedAt = CreatedAt,
                Characters = characters,
                MaxCharacters = MaxCharacters,
                Bank = Bank?.Clone(),
                LastSeenAt = LastSeenAt,
                UnlockedWaypoints = UnlockedWaypoints != null
                    ? new List<string>(UnlockedWaypoints)
                    : new List<string>(),
                AutoLoot = AutoLoot,
                AutoCombatDisabled = AutoCombatDisabled,
            };
        }
    }

    public partial class Character
    {
        /// <summary>
        /// Ondiepe kopie met verse collections zodat mutations de originele instantie
        /// niet beïnvloeden (equivalent aan TS spread { ...character, inventory }).
        /// Skills, Equipment en Talents krijgen elk een nieuwe dictionary.
        /// </summary>
        public Character Clone() => new Character
        {
            Id                = Id,
            Name              = Name,
            ClassId           = ClassId,
            Level             = Level,
            Xp                = Xp,
            Skills            = CloneSkills(Skills),
            SkillBar          = SkillBar != null ? new List<string>(SkillBar) : null,
            UnlockedSkillIds  = UnlockedSkillIds != null ? new List<string>(UnlockedSkillIds) : null,
            AvailableSkillPoints = AvailableSkillPoints,
            SpentSkillPoints = SpentSkillPoints,
            SkillStateSchemaVersion = SkillStateSchemaVersion,
            Equipment         = Equipment != null
                                    ? new Dictionary<EquipSlot, string>(Equipment)
                                    : null,
            Inventory         = CloneStacks(Inventory),
            MaxInventorySlots = MaxInventorySlots,
            MapId             = MapId,
            Activity          = Activity == null ? null : new ActivityState
            {
                Kind = Activity.Kind,
                TargetId = Activity.TargetId,
                StartedAt = Activity.StartedAt,
            },
            Efficiency        = Efficiency == null ? null : new EfficiencySnapshot
            {
                Kind = Efficiency.Kind,
                TargetId = Efficiency.TargetId,
                ActionsPerHour = Efficiency.ActionsPerHour,
                XpPerAction = Efficiency.XpPerAction,
                CoinsPerAction = Efficiency.CoinsPerAction,
                SnapshotAt = Efficiency.SnapshotAt,
                CharacterRevision = Efficiency.CharacterRevision,
                ActivityRevision = Efficiency.ActivityRevision,
                ContentVersion = Efficiency.ContentVersion,
                MapDensity = Efficiency.MapDensity,
                TravelOverheadMs = Efficiency.TravelOverheadMs,
                SurvivalFactor = Efficiency.SurvivalFactor,
                DebugBreakdown = Efficiency.DebugBreakdown,
                SkillDiagnostics = CloneSkillDiagnostics(Efficiency.SkillDiagnostics),
            },
            CharacterRevision = CharacterRevision,
            ActivityRevision = ActivityRevision,
            Talents           = Talents != null
                                    ? new Dictionary<string, int>(Talents)
                                    : null,
            FreeStatPoints    = FreeStatPoints,
            AllocatedStats    = CloneStats(AllocatedStats),
        };

        private static Dictionary<SkillId, SkillProgress> CloneSkills(Dictionary<SkillId, SkillProgress> source)
        {
            if (source == null) return null;
            var result = new Dictionary<SkillId, SkillProgress>();
            foreach (var pair in source)
                result[pair.Key] = pair.Value == null ? null : new SkillProgress
                {
                    Level = pair.Value.Level,
                    Xp = pair.Value.Xp,
                };
            return result;
        }

        private static List<ItemStack> CloneStacks(List<ItemStack> source)
        {
            var result = new List<ItemStack>();
            if (source == null) return result;
            foreach (var stack in source)
                if (stack != null)
                    result.Add(new ItemStack { ItemId = stack.ItemId, Qty = stack.Qty });
            return result;
        }

        private static List<SkillSnapshotDiagnostic> CloneSkillDiagnostics(List<SkillSnapshotDiagnostic> source)
        {
            var result = new List<SkillSnapshotDiagnostic>();
            foreach (SkillSnapshotDiagnostic item in source ?? new List<SkillSnapshotDiagnostic>())
                if (item != null)
                    result.Add(new SkillSnapshotDiagnostic
                    {
                        SlotIndex = item.SlotIndex,
                        SkillId = item.SkillId,
                        Included = item.Included,
                        Reason = item.Reason,
                        ExpectedCastsPerHour = item.ExpectedCastsPerHour,
                        ExpectedTargetsPerCast = item.ExpectedTargetsPerCast,
                        ExpectedDamageContribution = item.ExpectedDamageContribution,
                        AssumptionSource = item.AssumptionSource,
                    });
            return result;
        }

        private static CoreStats CloneStats(CoreStats source) => source == null ? null : new CoreStats
        {
            Strength = source.Strength,
            Agility = source.Agility,
            Wisdom = source.Wisdom,
            Luck = source.Luck,
        };
    }

    // ── Talents ──────────────────────────────────────────────────────────────

    [System.Serializable]
    public class TalentDef
    {
        public string Id;
        public string Name;
        public ClassId? ClassId;     // null betekent 'all'
        public bool AvailableToAll;  // true = beschikbaar voor alle klassen
        public string Description;
        public BonusStat BonusStat;
        public double BonusPerPoint;
        public int MaxPoints;
    }

    // ── Drop-systeem ──────────────────────────────────────────────────────────

    /// <summary>
    /// Kans-gebaseerde drop-entry voor life-skill nodes (oogsten).
    /// Chance is de kans per actie (0..1); onderscheidt zich van DropItem
    /// in WeightedTable dat via gewichten werkt.
    /// </summary>
    [System.Serializable]
    public class DropEntry
    {
        public string ItemId;
        public double Chance; // 0..1 — kans per actie (kill/oogst)
        public int Min;
        public int Max;
    }

    [System.Serializable]
    public class DropItem
    {
        public string ItemId;
        public int Min;
        public int Max;
    }

    /// <summary>
    /// Eén slot in een WeightedTable: ofwel een item-drop, ofwel een leeg resultaat.
    /// TS gebruikte een discriminated union; hier geven we Nothing=true aan voor het lege slot.
    /// </summary>
    [System.Serializable]
    public class WeightedSlot
    {
        public int Weight;
        public bool Nothing;   // true = leeg resultaat
        public string ItemId;  // null wanneer Nothing=true
        public int Min;
        public int Max;
    }

    [System.Serializable]
    public class WeightedTable
    {
        public List<WeightedSlot> Slots;
        public int Rolls = 1; // aantal onafhankelijke picks per kill
    }

    [System.Serializable]
    public class DropTable
    {
        public List<DropItem> Always;  // 100% kans elke kill
        public WeightedTable Main;     // gewogen enkel-pick-tabel
        public List<DropEntry> Tertiary; // onafhankelijke kans-roll per kill
    }

    // ── Monster-definitie ────────────────────────────────────────────────────

    [System.Serializable]
    public class MonsterRangedConfig
    {
        public float RangePx;
        public double ProjectileSpeed;
        public int CooldownMs;
        public Element Element;
    }

    [System.Serializable]
    public class MonsterChargeConfig
    {
        public int WindupMs;
        public double Speed;
        public int CooldownMs;
    }

    [System.Serializable]
    public class MonsterCastConfig
    {
        public int AoeRadiusPx;
        public int CooldownMs;
        public int AoeColor; // hex-kleur als int
    }

    [System.Serializable]
    public class CoinRange
    {
        public int Min;
        public int Max;
    }

    [System.Serializable]
    public class MonsterDef
    {
        public string Id;
        public string Name;
        public string MapId;
        public int Hp;
        public int Damage;
        public int Defense;
        public int Accuracy;   // drempel voor nauwkeurigheid
        public int Agility;    // gebruikt voor kans om speler te raken
        public int Xp;         // XP per kill
        public CoinRange Coins;
        public DropTable Drops;
        public int RespawnMs;
        public Element? Element;
        public MonsterBehavior? Behavior;
        public MonsterRangedConfig Ranged;
        public MonsterChargeConfig Charge;
        public MonsterCastConfig Cast;
    }

    // ── Resource-node ─────────────────────────────────────────────────────────

    [System.Serializable]
    public class ResourceNodeDef
    {
        public string Id;
        public string Name;
        public string MapId;
        public HarvestSkill Skill;
        public int LevelReq;
        public int BaseTimeMs;
        public int Xp;              // skill-xp per geslaagde oogst
        public List<DropEntry> Drops; // primaire opbrengst als kans-1-entry
    }

    // ── Crafting ──────────────────────────────────────────────────────────────

    [System.Serializable]
    public class RecipeDef
    {
        public string Id;
        public string Name;
        public string OutputItemId;
        public int OutputQty;
        public List<ItemStack> Inputs; // verbruikt uit de gedeelde account-bank
        public int CoinCost;
        public int LevelReq;
    }

    // ── Efficiency-snapshot (voor offline-formule) ────────────────────────────

    [System.Serializable]
    public class EfficiencySnapshot
    {
        public ActivityKind Kind;
        public string TargetId;
        public double ActionsPerHour;  // kills of oogsten/uur bij actief spelen
        public double XpPerAction;
        public double CoinsPerAction;  // gemiddelde; 0 voor skilling
        public long SnapshotAt;        // epoch ms
        public long CharacterRevision;
        public long ActivityRevision;
        public string ContentVersion;
        public double MapDensity;
        public double TravelOverheadMs;
        public double SurvivalFactor;
        public string DebugBreakdown;
        public List<SkillSnapshotDiagnostic> SkillDiagnostics;
    }

    [System.Serializable]
    public class SkillSnapshotDiagnostic
    {
        public int SlotIndex;
        public string SkillId;
        public bool Included;
        public string Reason;
        public double ExpectedCastsPerHour;
        public double ExpectedTargetsPerCast;
        public double ExpectedDamageContribution;
        public string AssumptionSource;
    }

    // ── Offline-rapportage ────────────────────────────────────────────────────

    [System.Serializable]
    public class OfflineCharacterReport
    {
        public string CharacterId;
        public string CharacterName;
        public ActivityKind Kind;
        public string TargetId;
        public int Actions;
        public long XpGained;
        public int LevelsGained;
        public int CoinsGained;
        public List<ItemStack> Loot;
        public List<ItemStack> LootOverflow;
    }

    [System.Serializable]
    public class OfflineReport
    {
        public long ElapsedMs;
        public long CappedMs;
        public List<OfflineCharacterReport> Characters;
    }

    // ── Data-bundles (voor pure core-functies die data-lookup nodig hebben) ────

    /// <summary>Bundelt alle benodigde game-content voor activiteit- en efficiëntie-berekeningen.</summary>
    [System.Serializable]
    public class ActivityDataBundle
    {
        public Dictionary<ClassId, ClassDef> Classes;
        public Dictionary<string, ItemDef> Items;
        public Dictionary<string, MonsterDef> Monsters;
        public Dictionary<string, ResourceNodeDef> Nodes;
        public Dictionary<string, MapDef> Maps;
    }

    /// <summary>Bundelt de benodigde game-content voor offline-progressie-simulatie.</summary>
    [System.Serializable]
    public class OfflineDataBundle
    {
        public Dictionary<string, ItemDef> Items;
        public Dictionary<string, MonsterDef> Monsters;
        public Dictionary<string, ResourceNodeDef> Nodes;
        public OfflineBalanceConfig OfflineBalance;
    }

    /// <summary>Designer-tunable limits and rate for bulk offline progress.</summary>
    [System.Serializable]
    public class OfflineBalanceConfig
    {
        public double Rate = 0.4;
        public long CapMs = 24L * 60 * 60 * 1000;
        public long MinimumDurationMs = 60_000;
    }

    /// <summary>Designer-tunable inputs for combat and gathering formulas. Algorithms remain in Core.</summary>
    [System.Serializable]
    public class CombatBalanceConfig
    {
        public double MaxHpBase = 100.0;
        public double MaxHpPerLevel = 2.0;
        public double MaxHpPerStrength = 1.0;
        public double AccuracyBase = 2.0;
        public double HitChanceMinimum = 0.05;
        public double HitChanceMaximum = 1.0;
        public double DefenseCurveConstant = 100.0;
        public double AttackSpeedBase = 1.0;
        public double AttackSpeedAgilityDivisor = 20.0;
        public double AttackSpeedMaximum = 2.0;
        public double MonsterHitChanceMinimum = 0.1;
        public double MonsterHitChanceMaximum = 0.9;
        public double CritBase = 0.03;
        public double CritLuckDivisor = 40.0;
        public double CritMaximum = 0.40;
        public double CritMultiplierBase = 1.5;
        public double CritMultiplierLuckDivisor = 50.0;
        public double CritMultiplierLuckBonusMaximum = 0.5;
        public double HarvestSuccessBase = 0.5;
        public double HarvestSuccessPerLevel = 0.04;
        public double HarvestSuccessMinimum = 0.1;
        public double HarvestSuccessMaximum = 0.95;
        public double HarvestStatBase = 20.0;
    }

    /// <summary>Designer-tunable inputs for the progression curve; leveling remains pure Core logic.</summary>
    [System.Serializable]
    public class ProgressionBalanceConfig
    {
        public double XpCoefficient = 50.0;
        public double XpExponent = 1.8;
        public double XpBase = 50.0;
    }

    // ── Map-definitie & -layout ───────────────────────────────────────────────

    /// <summary>Gameplay-definitie van een kaart (id, naam, verbindingen). Niet te verwarren met de render-layout.</summary>
    [System.Serializable]
    public class MapDef
    {
        public string Id;
        public string Name;
        public int RecommendedLevel;
        public List<string> Connections; // id's van aangrenzende kaarten
        public double EncounterDensity = 1.0;
        public double CombatTravelOverheadMs = 1500.0;
    }

    [System.Serializable]
    public class SpawnClusterDef
    {
        public string MonsterId;
        public float X;
        public float Y;
        public int Count;
        public float Radius;
        public bool Elite;
    }

    [System.Serializable]
    public class NodePlacementDef
    {
        public string NodeId;
        public float X;
        public float Y;
    }

    [System.Serializable]
    public class WarpDef
    {
        public WarpKind Kind;
        public string ToMapId;
        public float X;
        public float Y;
        public float Radius;
        public string Label;
    }

    [System.Serializable]
    public class HubBuildingDef
    {
        public string Id;
        public string Name;
        public float X;
        public float Y;
        public float W;
        public float H;
        public int RoofColor;   // hex-kleur als int
        public int WallColor;
    }

    [System.Serializable]
    public class ServiceDef
    {
        public HubServiceKind Kind;
        public float X;
        public float Y;
        public float Radius;
        public string Label;
    }

    [System.Serializable]
    public class NpcDef
    {
        public string Id;
        public string Name;
        public int Color;   // hex
        public float Cx;
        public float Cy;
        public float OrbitR;
        public float OrbitSpeed;
        public float OrbitOffset;
    }

    [System.Serializable]
    public class FieldBossDef
    {
        public string MonsterId;
        public float X;
        public float Y;
        public float AreaRadius;
        public int RespawnMs;
        public string Label;
        public double HpMultiplier;
        public double DamageMult;
        public double? EnrageAtHpFrac;
        public int? TelegraphMs;
        public float? SlamRadiusPx;
    }

    // ── Tiled-import helpers (genegeerd door headless engine) ────────────────

    [System.Serializable]
    public class TiledGroundTilesetRef
    {
        public string Key;     // Phaser/Unity texture-sleutel
        public int Columns;    // kolomaantal in spritesheet
        public int FirstGid;   // firstgid uit Tiled-kaartexport
    }

    [System.Serializable]
    public class PlacedProp
    {
        public string Key;
        public int Frame;
        public float X;
        public float Y;
        public float OriginX;
        public float OriginY;
        public float ScaleX;
        public float ScaleY;
        public int? Depth;
    }

    [System.Serializable]
    public class ParallaxBg
    {
        public string Url;
        public float Parallaxx;
        public float Parallaxy;
        public float Offsetx;
        public float Offsety;
    }

    [System.Serializable]
    public class MapLayoutDef
    {
        public string MapId;
        public MapKind Kind;
        public int TileSize;
        public int Cols;
        public int Rows;
        public int[][] Tiles;        // rij-major 2D-array: 0=begaanbaar, 1=muur
        public float SpawnX;
        public float SpawnY;
        public List<SpawnClusterDef> Spawns;
        public List<NodePlacementDef> Nodes;
        public List<WarpDef> Warps;
        public List<HubBuildingDef> Buildings;
        public List<ServiceDef> Services;
        public List<NpcDef> Npcs;
        public FieldBossDef FieldBoss;
        // Render-data (genegeerd door headless engine)
        public int[][][] RenderLayers;
        public TiledGroundTilesetRef GroundTileset;
        public List<PlacedProp> Props;
        public ParallaxBg ParallaxBg;
    }
}
#pragma warning restore UAC1001, UAC1009
