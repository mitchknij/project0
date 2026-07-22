using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>
    /// Authoritative, scene-local tile claims for actors. Occupancy represents where an actor is
    /// standing; reservations represent where it is travelling. Both block other actors.
    /// </summary>
    public static class TileOccupancy
    {
        private static readonly Dictionary<Vector2Int, object> Occupants = new();
        private static readonly Dictionary<Vector2Int, object> Reservations = new();

        public static event Action<Vector2Int> TileReleased;

        /// <summary>Compatibility alias for older callers that claimed a standing tile.</summary>
        public static bool TryClaim(Vector2Int cell, object owner) => TryOccupy(cell, owner);

        public static bool TryOccupy(Vector2Int cell, object owner)
        {
            if (owner == null || IsBlockedByOther(cell, owner)) return false;
            Occupants[cell] = owner;
            return true;
        }

        public static bool TryReserve(Vector2Int cell, object owner)
        {
            CleanupStaleOwners();
            if (owner == null || IsBlockedByOther(cell, owner)) return false;
            Reservations[cell] = owner;
            return true;
        }

        /// <summary>Atomically turns this owner's reservation into standing occupancy.</summary>
        public static bool TryCommitReservation(Vector2Int from, Vector2Int to, object owner)
        {
            CleanupStaleOwners();
            if (owner == null || !Reservations.TryGetValue(to, out object reservedBy) ||
                !ReferenceEquals(reservedBy, owner) || IsOccupiedByOther(to, owner))
                return false;

            Reservations.Remove(to);
            Occupants[to] = owner;
            ReleaseOccupancy(from, owner);
            return true;
        }

        public static bool IsBlocked(Vector2Int cell, object requester = null)
        {
            CleanupStaleOwners();
            return IsBlockedByOther(cell, requester);
        }

        public static bool IsOccupied(Vector2Int cell, object requester = null)
        {
            CleanupStaleOwners();
            return IsOccupiedByOther(cell, requester);
        }

        public static void Release(Vector2Int cell, object owner)
        {
            ReleaseOccupancy(cell, owner);
            ReleaseReservation(cell, owner);
        }

        public static void ReleaseOccupancy(Vector2Int cell, object owner)
        {
            if (Occupants.TryGetValue(cell, out object existing) && ReferenceEquals(existing, owner))
            {
                Occupants.Remove(cell);
                TileReleased?.Invoke(cell);
            }
        }

        public static void ReleaseReservation(Vector2Int cell, object owner)
        {
            if (Reservations.TryGetValue(cell, out object existing) && ReferenceEquals(existing, owner))
            {
                Reservations.Remove(cell);
                TileReleased?.Invoke(cell);
            }
        }

        public static void ReleaseAll(object owner)
        {
            if (owner == null) return;
            ReleaseOwned(Occupants, owner);
            ReleaseOwned(Reservations, owner);
        }

        public static void ReleaseAllReservationsFor(object owner)
        {
            if (owner != null) ReleaseOwned(Reservations, owner);
        }

        public static void CleanupStaleOwners()
        {
            Cleanup(Occupants);
            Cleanup(Reservations);
        }

        private static bool IsBlockedByOther(Vector2Int cell, object requester)
            => IsOccupiedByOther(cell, requester) ||
               (Reservations.TryGetValue(cell, out object reservation) && !ReferenceEquals(reservation, requester));

        private static bool IsOccupiedByOther(Vector2Int cell, object requester)
            => Occupants.TryGetValue(cell, out object occupant) && !ReferenceEquals(occupant, requester);

        private static void ReleaseOwned(Dictionary<Vector2Int, object> claims, object owner)
        {
            var cells = new List<Vector2Int>();
            foreach (var pair in claims)
                if (ReferenceEquals(pair.Value, owner)) cells.Add(pair.Key);
            foreach (Vector2Int cell in cells)
            {
                claims.Remove(cell);
                TileReleased?.Invoke(cell);
            }
        }

        private static void Cleanup(Dictionary<Vector2Int, object> claims)
        {
            var stale = new List<Vector2Int>();
            foreach (var pair in claims)
                if (pair.Value == null || (pair.Value is UnityEngine.Object unityObject && unityObject == null))
                    stale.Add(pair.Key);
            foreach (Vector2Int cell in stale)
            {
                claims.Remove(cell);
                TileReleased?.Invoke(cell);
            }
        }
    }
}
