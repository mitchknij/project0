// CombatMath.cs — Gevechts- en vaardigheidsberekeningen (vertaling van src/core/combatMath.ts).
// Enkelvoudige bron van waarheid voor zowel actieve als offline efficiëntie-snapshots,
// zodat voortgang overeenkomt. Alleen pure functies.

using System;

namespace IdleCloud.Core
{
    public static class CombatMath
    {
        public const double DefaultMoveOverheadMs = 1500.0;
        private static CombatBalanceConfig _configuration = new CombatBalanceConfig();

        public static CombatBalanceConfig CurrentConfiguration => _configuration;

        /// <summary>Installs a detached configuration snapshot from the Manager layer.</summary>
        public static void Configure(CombatBalanceConfig configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            _configuration = configuration;
        }

        // ── Hulpfuncties ────────────────────────────────────────────────────

        /// <summary>Beperkt v tot het bereik [lo, hi].</summary>
        public static double Clamp(double v, double lo, double hi)
            => Math.Min(hi, Math.Max(lo, v));

        // ── HP & verdediging ────────────────────────────────────────────────

        /// <summary>
        /// Maximale HP van het personage. hpMult past klasse-passieve bonussen toe
        /// (bv. Warrior +30% → hpMult=1.30).
        /// </summary>
        public static int MaxHp(int level, CoreStats stats, double hpMult = 1.0)
            => (int)Math.Floor((_configuration.MaxHpBase + _configuration.MaxHpPerLevel * level +
                _configuration.MaxHpPerStrength * stats.Strength) * hpMult);

        // ── Nauwkeurigheid & trefkans ────────────────────────────────────────

        /// <summary>Nauwkeurigheidsrating afgeleid van stats.</summary>
        public static int AccuracyRating(CoreStats stats)
            => (int)Math.Floor(_configuration.AccuracyBase + stats.Agility);

        /// <summary>Kans (0.05..1) om een monster te raken, gegeven zijn drempel.</summary>
        public static double HitChance(CoreStats stats, MonsterDef monster)
            => Clamp((double)AccuracyRating(stats) / monster.Accuracy,
                _configuration.HitChanceMinimum, _configuration.HitChanceMaximum);

        // ── Schade ──────────────────────────────────────────────────────────

        /// <summary>Ruwe schade per aanval vóór mitigatie.</summary>
        public static int BaseDamage(CoreStats stats)
            => 1 + (int)Math.Floor(stats.Strength / 2.0);

        /// <summary>Schade na mitigatie door de verdediging van de verdediger (soft-cap curve).</summary>
        public static int Mitigate(int damage, int defense)
            => Math.Max(1, (int)Math.Floor(damage * (_configuration.DefenseCurveConstant /
                (_configuration.DefenseCurveConstant + defense))));

        // ── Aanvalssnelheid ──────────────────────────────────────────────────

        /// <summary>Aanvalssnelheid van de speler in aanvallen per seconde (1.0..2.0).</summary>
        public static double AttacksPerSecond(CoreStats stats)
            => Clamp(_configuration.AttackSpeedBase + stats.Agility /
                _configuration.AttackSpeedAgilityDivisor, _configuration.AttackSpeedBase,
                _configuration.AttackSpeedMaximum);

        // ── Monster-aanval ───────────────────────────────────────────────────

        /// <summary>Kans van het monster om de speler te raken (gebruikt door actieve sim).</summary>
        public static double MonsterHitChance(MonsterDef monster, CoreStats stats)
            => Clamp((double)monster.Agility / (monster.Agility + stats.Agility),
                _configuration.MonsterHitChanceMinimum, _configuration.MonsterHitChanceMaximum);

        // ── Kill-snelheid ────────────────────────────────────────────────────

        /// <summary>Gemiddelde tijd in ms om één monster te doden, inclusief verplaatsingsoverhead.</summary>
        public static double TimeToKillMs(CoreStats stats, MonsterDef monster)
            => TimeToKillMs(stats, monster, DefaultMoveOverheadMs);

        /// <summary>
        /// Average time to kill one monster with an authored map encounter/travel overhead.
        /// </summary>
        public static double TimeToKillMs(CoreStats stats, MonsterDef monster, double travelOverheadMs)
        {
            if (travelOverheadMs < 0)
                throw new ArgumentOutOfRangeException(nameof(travelOverheadMs));
            double dps = HitChance(stats, monster)
                         * Mitigate(BaseDamage(stats), monster.Defense)
                         * AttacksPerSecond(stats);
            return (monster.Hp / dps) * 1000.0 + travelOverheadMs;
        }

        /// <summary>Actieve kill-snelheid — voor efficiency-snapshot bij gevecht.</summary>
        public static double KillsPerHour(CoreStats stats, MonsterDef monster)
            => 3_600_000.0 / TimeToKillMs(stats, monster);

        public static double KillsPerHour(CoreStats stats, MonsterDef monster, double travelOverheadMs)
            => 3_600_000.0 / TimeToKillMs(stats, monster, travelOverheadMs);

        /// <summary>
        /// Uses the same one-second enemy attack cadence as ActiveSim to reject an
        /// offline combat policy that cannot survive one complete encounter.
        /// </summary>
        public static double EncounterSurvivalFactor(
            int playerMaxHp,
            int playerDefense,
            CoreStats stats,
            MonsterDef monster,
            double combatDurationMs)
        {
            if (playerMaxHp <= 0 || monster == null || combatDurationMs < 0) return 0.0;
            if (monster.Damage <= 0) return 1.0;

            double attacks = Math.Ceiling(combatDurationMs / 1000.0);
            double expectedDamage = attacks * MonsterHitChance(monster, stats)
                * Mitigate(monster.Damage, playerDefense);
            return expectedDamage >= playerMaxHp ? 0.0 : 1.0;
        }

        // ── Oogsten ─────────────────────────────────────────────────────────

        /// <summary>
        /// Kans dat een oogstpoging slaagt. Geeft 0 terug wanneer onder het level-vereiste.
        /// </summary>
        public static double HarvestSuccessChance(int skillLevel, ResourceNodeDef node)
        {
            if (skillLevel < node.LevelReq) return 0.0;
            return Clamp(_configuration.HarvestSuccessBase +
                _configuration.HarvestSuccessPerLevel * (skillLevel - node.LevelReq),
                _configuration.HarvestSuccessMinimum, _configuration.HarvestSuccessMaximum);
        }

        /// <summary>
        /// Milliseconden per oogstpoging na stat-schaling.
        /// Str=mijnbouw, Wis=hakken, Luck=verzamelen.
        /// </summary>
        public static double HarvestTimeMs(CoreStats stats, ResourceNodeDef node)
        {
            double stat = node.Skill == HarvestSkill.Mining    ? stats.Strength
                        : node.Skill == HarvestSkill.Gathering ? stats.Luck
                        : stats.Wisdom;
            return node.BaseTimeMs * (_configuration.HarvestStatBase /
                (_configuration.HarvestStatBase + stat));
        }

        // ── Kritieke treffers ────────────────────────────────────────────────

        /// <summary>Basis krit-kans (0.03..0.40), aangedreven door luck.</summary>
        public static double CritChance(CoreStats stats)
            => Clamp(_configuration.CritBase + stats.Luck / _configuration.CritLuckDivisor,
                _configuration.CritBase, _configuration.CritMaximum);

        /// <summary>Krit-schademultiplier (1.5 basis, schaalt licht met luck).</summary>
        public static double CritMultiplier(CoreStats stats)
            => _configuration.CritMultiplierBase + Math.Min(
                stats.Luck / _configuration.CritMultiplierLuckDivisor,
                _configuration.CritMultiplierLuckBonusMaximum);

        // ── Status-effecten ──────────────────────────────────────────────────

        /// <summary>
        /// Schade per DoT-tick (brand / gif, 0.5 s interval).
        /// magnitude is de fractie van de basis-aanvalsschade van de aanvaller per tick.
        /// </summary>
        public static int StatusTickDamage(double magnitude, int playerBaseDmg)
            => Math.Max(1, (int)Math.Floor(playerBaseDmg * magnitude));

        // ── Oogst-snelheid ───────────────────────────────────────────────────

        /// <summary>Actieve oogstsnelheid in oogsten per uur — voor efficiency-snapshot bij skilling.</summary>
        public static double HarvestsPerHour(CoreStats stats, int skillLevel, ResourceNodeDef node)
        {
            double success = HarvestSuccessChance(skillLevel, node);
            if (success == 0.0) return 0.0;
            return (3_600_000.0 / HarvestTimeMs(stats, node)) * success;
        }
    }
}
