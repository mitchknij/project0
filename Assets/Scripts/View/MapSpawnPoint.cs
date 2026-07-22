using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>Inspector-authored map arrival point. Scene loading owns all placement logic.</summary>
    public sealed class MapSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnId = "default";

        public string SpawnId => spawnId;

        private void OnValidate()
        {
            spawnId = string.IsNullOrWhiteSpace(spawnId) ? "default" : spawnId.Trim();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.18f);
            Gizmos.DrawRay(transform.position, transform.right * 0.45f);
        }
    }
}
