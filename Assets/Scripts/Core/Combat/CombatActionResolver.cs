using System;

namespace IdleCloud.Core
{
    public sealed class CombatActionResolution
    {
        public bool Hit;
        public bool Critical;
        public int Damage;
    }

    /// <summary>
    /// Pure shared hit/crit/damage resolution used by basic attacks and skills.
    /// Target validation, state mutation, death and rewards remain with ActiveSim.
    /// </summary>
    public static class CombatActionResolver
    {
        public static CombatActionDefinition BasicAttack() => new CombatActionDefinition
        {
            Id = "basic_attack",
            Kind = CombatActionKind.BasicAttack,
            Element = Element.Physical,
            DamageMultiplier = 1.0,
        };

        public static CombatActionDefinition FromLegacySkill(ClassSkillDef skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            return new CombatActionDefinition
            {
                Id = skill.Id,
                Kind = CombatActionKind.Skill,
                Element = skill.Element,
                DamageMultiplier = skill.DamageMultiplier,
            };
        }

        public static CombatActionResolution ResolvePlayerDamage(
            CombatActionDefinition action,
            CoreStats stats,
            MonsterDef target,
            AccountBonuses bonuses,
            PassiveMultipliers passive,
            IRandomSource random)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (random == null) throw new ArgumentNullException(nameof(random));

            bool hit = random.NextDouble() < CombatMath.HitChance(stats, target);
            var result = new CombatActionResolution { Hit = hit };
            if (!hit) return result;

            double multiplier = action.DamageMultiplier;
            multiplier *= 1.0 + (bonuses?.CombatPct ?? 0.0) + (passive?.DamagePct ?? 0.0);
            int rawDamage = Math.Max(1, (int)Math.Floor(CombatMath.BaseDamage(stats) * multiplier));
            int damage = CombatMath.Mitigate(rawDamage, target.Defense);
            damage = Math.Max(1, (int)Math.Floor(
                damage * Elements.ElementMultiplier(action.Element, target.Element ?? Element.Physical)));

            bool critical = random.NextDouble() < CombatMath.Clamp(
                CombatMath.CritChance(stats) + (passive?.CritPct ?? 0.0), 0.0, 1.0);
            if (critical)
                damage = Math.Max(1, (int)Math.Floor(damage * CombatMath.CritMultiplier(stats)));

            result.Critical = critical;
            result.Damage = damage;
            return result;
        }
    }
}
