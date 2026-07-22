using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>Explicit encounter ownership, leash policy, and single-hop help alerts for placed mobs.</summary>
    public sealed class MobEncounter : MonoBehaviour
    {
        [SerializeField] private string encounterId = "encounter";
        [Tooltip("Optional 2D collider defining the area in which this encounter may pursue.")]
        [SerializeField] private Collider2D leashBoundary;
        [Tooltip("Used as a circular leash when no boundary collider is authored.")]
        [SerializeField, Min(0f)] private float fallbackLeashRadius = 8f;
        [SerializeField, Min(0f)] private float helpRadius = 4f;
        [SerializeField] private EnemyController[] members = Array.Empty<EnemyController>();

        private readonly List<EnemyController> _registered = new();

        public string EncounterId => encounterId;
        public float HelpRadius => helpRadius;

        private void Awake()
        {
            RegisterConfiguredMembers();
        }

        private void OnValidate()
        {
            encounterId = string.IsNullOrWhiteSpace(encounterId) ? "encounter" : encounterId.Trim();
            if (leashBoundary == null) leashBoundary = GetComponent<Collider2D>();
            if (leashBoundary != null && !leashBoundary.isTrigger)
                Debug.LogWarning("[MobEncounter] The leash boundary should be a trigger collider.", this);
        }

        public void Register(EnemyController member)
        {
            if (member != null && !_registered.Contains(member)) _registered.Add(member);
        }

        public void Unregister(EnemyController member) => _registered.Remove(member);

        public bool Contains(Vector3 worldPosition)
        {
            if (leashBoundary != null)
                return leashBoundary.OverlapPoint(worldPosition);
            return fallbackLeashRadius <= 0f ||
                ((Vector2)(worldPosition - transform.position)).sqrMagnitude <= fallbackLeashRadius * fallbackLeashRadius;
        }

        /// <summary>Only the directly attacked mob calls this method; alerted members never relay it.</summary>
        public void AlertNearby(EnemyController directlyAttacked, PlayerController player)
        {
            if (directlyAttacked == null || player == null) return;
            for (int index = _registered.Count - 1; index >= 0; index--)
            {
                EnemyController member = _registered[index];
                if (member == null) { _registered.RemoveAt(index); continue; }
                if (member == directlyAttacked || !member.isActiveAndEnabled) continue;
                if (Vector2.Distance(member.LogicalPosition, directlyAttacked.LogicalPosition) <= helpRadius)
                    member.ReceiveEncounterAlert(player);
            }
        }

        private void RegisterConfiguredMembers()
        {
            foreach (EnemyController member in members) Register(member);
            foreach (EnemyController childMember in GetComponentsInChildren<EnemyController>(includeInactive: true))
                Register(childMember);
        }
    }
}
