using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Managers;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class GameSessionTests
    {
        [Test]
        public void Commit_PublishesAfterStateAndProtectsSnapshotFromExternalMutation()
        {
            var session = new GameSession();
            Account observed = null;
            SessionEvent observedEvent = null;
            session.Changed += gameEvent =>
            {
                observed = session.Account;
                observedEvent = gameEvent;
            };

            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            session.Commit(account, new CharacterMutationCommand("test", null), SessionEventKind.AccountChanged);
            account.Bank.Coins = 99;
            Account snapshot = session.Account;
            snapshot.Bank.Coins = 50;

            Assert.That(observed, Is.Not.Null);
            Assert.That(observedEvent.AccountRevision, Is.EqualTo(1));
            Assert.That(session.Account.Bank.Coins, Is.EqualTo(0));
            Assert.That(session.IsDirty, Is.True);
        }

        [Test]
        public void RestoreThenMarkSaved_TracksDirtyStateAndSelection()
        {
            var session = new GameSession();
            Account account = AccountHelper.CreateAccount("account", "Family", 0);

            session.Restore(account, "character", new Dictionary<string, int> { ["slime"] = 2 });

            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.SelectedCharacterId, Is.EqualTo("character"));
            Assert.That(session.WorldKills["slime"], Is.EqualTo(2));

            session.SelectCharacter("other");
            session.MarkSaved();

            Assert.That(session.SelectedCharacterId, Is.EqualTo("other"));
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void Commit_RecordsBoundedPresentationNeutralTraceAfterCommit()
        {
            var session = new GameSession();
            Account account = AccountHelper.CreateAccount("account", "Family", 0);

            session.Commit(account, new CharacterMutationCommand("test-command", null), SessionEventKind.AccountChanged);

            var trace = session.CopyTrace();
            Assert.That(trace, Has.Count.EqualTo(1));
            Assert.That(trace[0].CommandName, Is.EqualTo("test-command"));
            Assert.That(trace[0].AccountRevision, Is.EqualTo(1));
            Assert.That(trace[0].Timestamp, Is.GreaterThan(0));
        }
    }
}
