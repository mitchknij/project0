// Pure bonus-state contract. It lives in Data because simulation input contracts
// carry it across the Core/Manager boundary.

namespace IdleCloud.Core
{
    [System.Serializable]
    public class AccountBonuses
    {
        public int Strength;
        public int Agility;
        public int Wisdom;
        public int Luck;
        public double HpPct;
        public double DropPct;
        public double XpPct;
        public double CombatPct;
        public double MiningPct;
        public double ChoppingPct;
        public double GatheringPct;

        public static AccountBonuses Zero() => new AccountBonuses();

        public void Add(BonusStat stat, double value)
        {
            switch (stat)
            {
                case BonusStat.Strength: Strength += (int)value; break;
                case BonusStat.Agility: Agility += (int)value; break;
                case BonusStat.Wisdom: Wisdom += (int)value; break;
                case BonusStat.Luck: Luck += (int)value; break;
                case BonusStat.HpPct: HpPct += value; break;
                case BonusStat.DropPct: DropPct += value; break;
                case BonusStat.XpPct: XpPct += value; break;
                case BonusStat.CombatPct: CombatPct += value; break;
                case BonusStat.MiningPct: MiningPct += value; break;
                case BonusStat.ChoppingPct: ChoppingPct += value; break;
                case BonusStat.GatheringPct: GatheringPct += value; break;
            }
        }
    }
}
