using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>Optional explicit authoring record for a placed mob's encounter and home tile.</summary>
    public sealed class MobSpawnPoint : MonoBehaviour
    {
        [SerializeField] private EnemyController mob;
        [SerializeField] private MobEncounter encounter;
        [SerializeField] private Transform homeOverride;

        public EnemyController Mob => mob;
        public MobEncounter Encounter => encounter;
        public Vector3 HomePosition => homeOverride != null ? homeOverride.position : transform.position;

        private void OnValidate()
        {
            if (mob == null) mob = GetComponentInChildren<EnemyController>();
            if (encounter == null) encounter = GetComponentInParent<MobEncounter>();
            if (mob == null) Debug.LogWarning("[MobSpawnPoint] Assign a mob instance.", this);
            if (encounter == null) Debug.LogWarning("[MobSpawnPoint] Assign an encounter explicitly.", this);
        }
    }
}
