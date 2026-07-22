namespace IdleCloud.Core
{
    public static class SnapshotValidation
    {
        private const string DefaultContentVersion = "code-v3";
        private static string _currentContentVersion = DefaultContentVersion;

        /// <summary>
        /// Version of the pure runtime content/configuration snapshot currently
        /// installed by the Manager layer. Unity assets never cross into Core.
        /// </summary>
        public static string CurrentContentVersion => _currentContentVersion;

        public static void ConfigureContentVersion(string version)
        {
            _currentContentVersion = string.IsNullOrWhiteSpace(version)
                ? DefaultContentVersion
                : version;
        }

        public static bool IsUsable(EfficiencySnapshot snapshot)
        {
            return snapshot != null && snapshot.ContentVersion == CurrentContentVersion &&
                snapshot.ActionsPerHour >= 0.0 && snapshot.XpPerAction >= 0.0 &&
                snapshot.CoinsPerAction >= 0.0 && snapshot.MapDensity > 0.0 &&
                snapshot.SurvivalFactor >= 0.0 && snapshot.SurvivalFactor <= 1.0;
        }

        public static bool IsUsable(EfficiencySnapshot snapshot, Character character)
        {
            return IsUsable(snapshot) && character != null &&
                snapshot.CharacterRevision == character.CharacterRevision &&
                snapshot.ActivityRevision == character.ActivityRevision;
        }
    }
}
