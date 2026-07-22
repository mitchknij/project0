using IdleCloud.View;
using NUnit.Framework;
using UnityEngine;

namespace IdleCloud.Tests.EditMode
{
    public sealed class TileOccupancyTests
    {
        [Test]
        public void Reservation_IsFirstOwnerWins_AndBlocksOccupancy()
        {
            var first = new object();
            var second = new object();
            var tile = new Vector2Int(7123, -918);

            Assert.That(TileOccupancy.TryReserve(tile, first), Is.True);
            Assert.That(TileOccupancy.TryReserve(tile, second), Is.False);
            Assert.That(TileOccupancy.TryOccupy(tile, second), Is.False);

            TileOccupancy.ReleaseReservation(tile, first);
            Assert.That(TileOccupancy.TryOccupy(tile, second), Is.True);
            TileOccupancy.ReleaseAll(second);
        }

        [Test]
        public void CommitReservation_MovesOccupancyWithoutLeavingSourceClaim()
        {
            var owner = new object();
            var other = new object();
            var source = new Vector2Int(7124, -918);
            var destination = new Vector2Int(7125, -918);

            Assert.That(TileOccupancy.TryOccupy(source, owner), Is.True);
            Assert.That(TileOccupancy.TryReserve(destination, owner), Is.True);
            Assert.That(TileOccupancy.TryCommitReservation(source, destination, owner), Is.True);
            Assert.That(TileOccupancy.IsOccupied(source), Is.False);
            Assert.That(TileOccupancy.TryOccupy(destination, other), Is.False);

            TileOccupancy.ReleaseAll(owner);
            Assert.That(TileOccupancy.TryOccupy(destination, other), Is.True);
            TileOccupancy.ReleaseAll(other);
        }
    }
}
