// ClassesRepo.cs — Statische klasse-definities (vertaling van src/data/classes.ts).
// 1:1 met de TypeScript-bron; hex-kleuren als C# int-literals.

using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Data
{
    public static class ClassesRepo
    {
        public static readonly Dictionary<ClassId, ClassDef> All = new Dictionary<ClassId, ClassDef>
        {
            // ── Journeyman (Beginner) ─────────────────────────────────────────────
            {
                ClassId.Beginner, new ClassDef
                {
                    Id          = ClassId.Beginner,
                    Name        = "Journeyman",
                    Description = "A well-rounded adventurer who excels at nothing but survives everything. " +
                                  "Good choice for learning the ropes before committing to a specialisation.",
                    PassiveBonus = "Balanced stat growth. Minor speed bonus to all skills.",
                    BaseStats   = new CoreStats { Strength = 2, Agility = 2, Wisdom = 2, Luck = 2 },
                    StatGrowth  = new StatGrowthDef { Strength = 0.4, Agility = 0.4, Wisdom = 0.4, Luck = 0.3 },
                    Passive     = new PassiveSkillDef
                    {
                        Name        = "Jack of All Trades",
                        Description = "Balanced stat growth. Minor speed bonus to all skills.",
                        Multipliers = new PassiveMultipliers(),
                    },
                    Skills = new List<ClassSkillDef>
                    {
                        new ClassSkillDef
                        {
                            Id               = "wild_strike",
                            Name             = "Wild Strike",
                            Description      = "A frenzied slash that catches every nearby enemy in a wide arc.",
                            Mechanic         = SkillMechanic.Aoe,
                            Element          = Element.Physical,
                            CooldownMs       = 5000,
                            DamageMultiplier = 2.2,
                            AoeRadiusPx      = 120,
                            AoeColor         = 0xffdd44,
                            Priority         = 2,
                            RangePx          = 100,
                        },
                        new ClassSkillDef
                        {
                            Id               = "quick_jab",
                            Name             = "Quick Jab",
                            Description      = "A rapid lunging strike that extends the attack reach. Low cooldown, reliable damage.",
                            Mechanic         = SkillMechanic.Projectile,
                            Element          = Element.Physical,
                            CooldownMs       = 2000,
                            DamageMultiplier = 1.5,
                            AoeColor         = 0xffee88,
                            Priority         = 3,
                            RangePx          = 130,
                            ProjectileSpeed  = 280,
                            ProjectileRadius = 8,
                        },
                        new ClassSkillDef
                        {
                            Id               = "adrenaline",
                            Name             = "Adrenaline",
                            Description      = "Floods the body with combat energy, briefly boosting attack speed.",
                            Mechanic         = SkillMechanic.Buff,
                            Element          = Element.Physical,
                            CooldownMs       = 20000,
                            DamageMultiplier = 0,
                            AoeColor         = 0xffbb00,
                            Priority         = 1,
                            Buff             = new SelfBuff { Stat = BuffStat.Haste, Magnitude = 0.30, DurationMs = 5000 },
                        },
                    },
                }
            },

            // ── Warrior ───────────────────────────────────────────────────────────
            {
                ClassId.Warrior, new ClassDef
                {
                    Id          = ClassId.Warrior,
                    Name        = "Warrior",
                    Description = "A heavily-armoured melee fighter built for prolonged slugfests. " +
                                  "Deals bone-crushing blows and laughs off punishment that would floor others.",
                    PassiveBonus = "+30 % max HP. Bonus efficiency when mining ore veins.",
                    BaseStats   = new CoreStats { Strength = 3, Agility = 1, Wisdom = 1, Luck = 1 },
                    StatGrowth  = new StatGrowthDef { Strength = 0.9, Agility = 0.2, Wisdom = 0.1, Luck = 0.2 },
                    Passive     = new PassiveSkillDef
                    {
                        Name        = "Iron Resolve",
                        Description = "+30% max HP, +8 flat defense, 20% bonus mining efficiency.",
                        Multipliers = new PassiveMultipliers { Hp = 1.30, MiningEff = 1.20, DefenseFlat = 8 },
                    },
                    Skills = new List<ClassSkillDef>
                    {
                        new ClassSkillDef
                        {
                            Id               = "ground_slam",
                            Name             = "Ground Slam",
                            Description      = "Drives a weapon into the earth, sending a shockwave through everything close by. " +
                                               "Short range but devastating damage.",
                            Mechanic         = SkillMechanic.Aoe,
                            Element          = Element.Physical,
                            CooldownMs       = 6000,
                            DamageMultiplier = 3.2,
                            AoeRadiusPx      = 88,
                            AoeColor         = 0xff6600,
                            Priority         = 2,
                            RangePx          = 80,
                        },
                        new ClassSkillDef
                        {
                            Id               = "cleaving_throw",
                            Name             = "Cleaving Throw",
                            Description      = "Hurls a heavy weapon through the air. Deals strong single-target damage at range.",
                            Mechanic         = SkillMechanic.Projectile,
                            Element          = Element.Physical,
                            CooldownMs       = 3500,
                            DamageMultiplier = 2.0,
                            AoeColor         = 0xff9900,
                            Priority         = 3,
                            RangePx          = 200,
                            ProjectileSpeed  = 300,
                            ProjectileRadius = 12,
                        },
                        new ClassSkillDef
                        {
                            Id               = "battle_roar",
                            Name             = "Battle Roar",
                            Description      = "A terrifying war cry that temporarily boosts damage output.",
                            Mechanic         = SkillMechanic.Buff,
                            Element          = Element.Physical,
                            CooldownMs       = 15000,
                            DamageMultiplier = 0,
                            AoeColor         = 0xffcc00,
                            Priority         = 1,
                            Buff             = new SelfBuff { Stat = BuffStat.Damage, Magnitude = 0.40, DurationMs = 5000 },
                        },
                    },
                }
            },

            // ── Ranger (Archer) ───────────────────────────────────────────────────
            {
                ClassId.Archer, new ClassDef
                {
                    Id          = ClassId.Archer,
                    Name        = "Ranger",
                    Description = "A nimble scout who peppers enemies from afar. " +
                                  "Lower single-hit power is offset by blazing attack speed, high evasion, and a wide-area barrage skill.",
                    PassiveBonus = "+25 % crit/luck. Bonus efficiency when chopping trees.",
                    BaseStats   = new CoreStats { Strength = 1, Agility = 3, Wisdom = 1, Luck = 2 },
                    StatGrowth  = new StatGrowthDef { Strength = 0.3, Agility = 0.9, Wisdom = 0.1, Luck = 0.4 },
                    Passive     = new PassiveSkillDef
                    {
                        Name        = "Keen Eye",
                        Description = "+25% effective luck/crit, +8% bonus crit chance, 20% bonus chopping efficiency.",
                        Multipliers = new PassiveMultipliers { Luck = 1.25, ChoppingEff = 1.20, CritPct = 0.08 },
                    },
                    Skills = new List<ClassSkillDef>
                    {
                        new ClassSkillDef
                        {
                            Id               = "volley",
                            Name             = "Volley",
                            Description      = "Looses a storm of arrows in all directions, piercing every enemy in a broad radius. " +
                                               "Lower per-hit damage but hits everything at once.",
                            Mechanic         = SkillMechanic.Aoe,
                            Element          = Element.Nature,
                            CooldownMs       = 4000,
                            DamageMultiplier = 1.7,
                            AoeRadiusPx      = 165,
                            AoeColor         = 0x44ee66,
                            Priority         = 2,
                            RangePx          = 150,
                        },
                        new ClassSkillDef
                        {
                            Id               = "piercing_shot",
                            Name             = "Piercing Shot",
                            Description      = "A focused, high-velocity arrow that punches through armor for massive single-target damage.",
                            Mechanic         = SkillMechanic.Projectile,
                            Element          = Element.Nature,
                            CooldownMs       = 2000,
                            DamageMultiplier = 3.5,
                            AoeColor         = 0x22ff88,
                            Priority         = 3,
                            RangePx          = 280,
                            ProjectileSpeed  = 450,
                            ProjectileRadius = 8,
                        },
                        new ClassSkillDef
                        {
                            Id               = "hunters_focus",
                            Name             = "Hunter's Focus",
                            Description      = "Centres the mind and steadies the hand, temporarily increasing attack speed.",
                            Mechanic         = SkillMechanic.Buff,
                            Element          = Element.Nature,
                            CooldownMs       = 18000,
                            DamageMultiplier = 0,
                            AoeColor         = 0x88ff44,
                            Priority         = 1,
                            Buff             = new SelfBuff { Stat = BuffStat.Haste, Magnitude = 0.35, DurationMs = 6000 },
                        },
                    },
                }
            },

            // ── Arcanist (Mage) ───────────────────────────────────────────────────
            {
                ClassId.Mage, new ClassDef
                {
                    Id          = ClassId.Mage,
                    Name        = "Arcanist",
                    Description = "A scholar of forbidden energies who trades survivability for catastrophic magical output. " +
                                  "Patience during the long cooldown is rewarded with screen-clearing explosions.",
                    PassiveBonus = "+15 % XP gain from all sources. Bonus wisdom scaling on spells.",
                    BaseStats   = new CoreStats { Strength = 1, Agility = 1, Wisdom = 4, Luck = 1 },
                    StatGrowth  = new StatGrowthDef { Strength = 0.1, Agility = 0.2, Wisdom = 1.0, Luck = 0.3 },
                    Passive     = new PassiveSkillDef
                    {
                        Name        = "Arcane Mastery",
                        Description = "+15% XP from all sources, +10% bonus spell damage.",
                        Multipliers = new PassiveMultipliers { Xp = 1.15, DamagePct = 0.10 },
                    },
                    Skills = new List<ClassSkillDef>
                    {
                        new ClassSkillDef
                        {
                            Id               = "arcane_surge",
                            Name             = "Arcane Surge",
                            Description      = "Channels raw arcane energy into a devastating explosion that engulfs the entire screen. " +
                                               "Long recharge — timing is everything.",
                            Mechanic         = SkillMechanic.Aoe,
                            Element          = Element.Arcane,
                            CooldownMs       = 9500,
                            DamageMultiplier = 4.8,
                            AoeRadiusPx      = 210,
                            AoeColor         = 0xaa44ff,
                            Priority         = 2,
                            RangePx          = 200,
                        },
                        new ClassSkillDef
                        {
                            Id               = "firebolt",
                            Name             = "Firebolt",
                            Description      = "Hurls a blazing projectile that deals fire damage and leaves the target burning.",
                            Mechanic         = SkillMechanic.Projectile,
                            Element          = Element.Fire,
                            CooldownMs       = 2500,
                            DamageMultiplier = 1.8,
                            AoeColor         = 0xff4400,
                            Priority         = 3,
                            RangePx          = 220,
                            ProjectileSpeed  = 350,
                            ProjectileRadius = 10,
                            Inflicts         = new List<StatusInflict>
                            {
                                new StatusInflict { Kind = StatusKind.Burn, DurationMs = 3000, Magnitude = 0.15 },
                            },
                        },
                        new ClassSkillDef
                        {
                            Id               = "frost_nova",
                            Name             = "Frost Nova",
                            Description      = "Releases a burst of freezing energy that chills and briefly stuns nearby enemies.",
                            Mechanic         = SkillMechanic.Debuff,
                            Element          = Element.Ice,
                            CooldownMs       = 12000,
                            DamageMultiplier = 1.5,
                            AoeRadiusPx      = 160,
                            AoeColor         = 0x44aaff,
                            Priority         = 1,
                            RangePx          = 140,
                            Inflicts         = new List<StatusInflict>
                            {
                                new StatusInflict { Kind = StatusKind.Chill, DurationMs = 4000, Magnitude = 0.40 },
                                new StatusInflict { Kind = StatusKind.Stun,  DurationMs = 800,  Magnitude = 1 },
                            },
                        },
                    },
                }
            },
        };

        /// <summary>Retourneert de ClassDef voor de gegeven ClassId.</summary>
        public static ClassDef Get(ClassId id) => RuntimeContent.Get(id);
    }
}
