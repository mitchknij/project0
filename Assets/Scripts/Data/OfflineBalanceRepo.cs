using IdleCloud.Core;

namespace IdleCloud.Data
{
    /// <summary>Default offline progression tuning. Keep values in data, not Core formulas.</summary>
    public static class OfflineBalanceRepo
    {
        public static readonly OfflineBalanceConfig Default = new OfflineBalanceConfig
        {
            Rate = 0.4,
            CapMs = 24L * 60 * 60 * 1000,
            MinimumDurationMs = 60_000,
        };
    }
}
