using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>Triggerable scene portal. Configure the target map and its arrival spawn in the Inspector.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MapPortal : MonoBehaviour
    {
        [Header("Destination")]
        [Tooltip("Map ID from MapSceneCatalog, for example rock_1.")]
        [SerializeField] private string destinationMapId;
        [Tooltip("Arrival marker ID in the target map. Leave as default unless the target scene has additional MapSpawnPoints.")]
        [SerializeField] private string destinationSpawnId = "default";
        [Header("Travel Rule")]
        [Tooltip("Off: only an adjacent map. On: an unlocked waypoint warp.")]
        [SerializeField] private bool waypointWarp;

        private bool _used;

        private void Reset()
        {
            CircleCollider2D trigger = GetComponent<CircleCollider2D>();
            if (trigger == null) trigger = gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_used || other.GetComponentInParent<PlayerController>() == null) return;
            Activate();
        }

        public void Activate()
        {
            if (_used) return;
            MapTransitionCoordinator coordinator = MapTransitionCoordinator.Instance;
            if (coordinator == null) return;
            bool requested = waypointWarp
                ? coordinator.RequestWaypointWarp(destinationMapId, destinationSpawnId)
                : coordinator.RequestTravel(destinationMapId, destinationSpawnId);
            if (requested) _used = true;
        }

        private void OnValidate()
        {
            destinationMapId = destinationMapId?.Trim();
            destinationSpawnId = string.IsNullOrWhiteSpace(destinationSpawnId) ? "default" : destinationSpawnId.Trim();
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, trigger is CircleCollider2D circle ? circle.radius : 0.5f);
        }
    }
}
