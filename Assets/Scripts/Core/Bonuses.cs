// Bonus aggregation helpers. The AccountBonuses contract itself lives in Data.

namespace IdleCloud.Core
{
    public static class BonusHelper
    {
        public static AccountBonuses AddBonuses(params AccountBonuses[] sets)
        {
            var result = AccountBonuses.Zero();
            if (sets == null) return result;

            foreach (AccountBonuses set in sets)
            {
                if (set == null) continue;
                result.Strength += set.Strength;
                result.Agility += set.Agility;
                result.Wisdom += set.Wisdom;
                result.Luck += set.Luck;
                result.HpPct += set.HpPct;
                result.DropPct += set.DropPct;
                result.XpPct += set.XpPct;
                result.CombatPct += set.CombatPct;
                result.MiningPct += set.MiningPct;
                result.ChoppingPct += set.ChoppingPct;
                result.GatheringPct += set.GatheringPct;
            }
            return result;
        }
    }
}
