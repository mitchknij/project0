namespace IdleCloud.Core
{
    /// <summary>
    /// Derives a repeatable offline transaction seed from persisted transaction inputs.
    /// Repeating an uncommitted claim window therefore cannot produce new random loot.
    /// </summary>
    public static class OfflineSeed
    {
        public static int Derive(Account account, long now)
        {
            unchecked
            {
                uint hash = 2166136261;
                Add(ref hash, account?.Id);
                Add(ref hash, account?.LastSeenAt ?? 0L);
                Add(ref hash, now);
                return (int)hash;
            }
        }

        private static void Add(ref uint hash, string value)
        {
            if (value == null) return;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }
        }

        private static void Add(ref uint hash, long value)
        {
            for (int index = 0; index < 8; index++)
            {
                hash ^= (byte)(value >> (index * 8));
                hash *= 16777619;
            }
        }
    }
}
