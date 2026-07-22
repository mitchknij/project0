using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;

namespace IdleCloud.Managers
{
    public sealed class CombatReward
    {
        public int KillCount;
        public long CharacterXp;
        public long CombatSkillXp;
        public int Coins;
        public int CharacterPreviousLevel;
        public int CharacterNewLevel;
        public int CombatSkillPreviousLevel;
        public int CombatSkillNewLevel;
        public List<ItemStack> Loot = new List<ItemStack>();
        public List<KillLootRecord> KillLoot = new List<KillLootRecord>();
        public List<ItemStack> Overflow = new List<ItemStack>();
    }

    public sealed class KillLootRecord
    {
        public string ActorEntityId;
        public string MonsterId;
        public int Coins;
        public List<ItemStack> Stacks = new List<ItemStack>();
    }

    public sealed class ActiveCombatTickResult
    {
        public Account Account;
        public ActiveSimResult Simulation;
        public CombatReward Reward;
        public string RejectionReason;
    }

    /// <summary>
    /// Manager-layer bridge for active combat. It owns only transient simulation
    /// continuation state; callers remain responsible for committing Account.
    /// </summary>
    public sealed class ActiveCombatCoordinator
    {
        private readonly Dictionary<string, ActiveCombatState> _states = new Dictionary<string, ActiveCombatState>();
        private readonly IRandomSource _random;
        private readonly Dictionary<string, MonsterDef> _monsters;
        private readonly Dictionary<string, ItemDef> _items;

        public ActiveCombatConfig Config { get; set; }

        public ActiveCombatCoordinator(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _monsters = new Dictionary<string, MonsterDef>(RuntimeContent.Monsters);
            _items = new Dictionary<string, ItemDef>(RuntimeContent.Items);
        }

        public ActiveCombatTickResult Tick(
            Account account,
            string characterId,
            string targetEntityId,
            string monsterId,
            CombatWorldFacts world,
            List<CombatCommand> commands,
            long timestamp,
            AccountBonuses bonuses)
        {
            if (account == null) return Reject("missing_account");
            Character character = FindCharacter(account, characterId);
            if (character == null) return Reject("unknown_character");
            if (string.IsNullOrWhiteSpace(targetEntityId)) return Reject("missing_target_entity_id");
            ClassDef classDef = RuntimeContent.Get(character.ClassId);
            if (classDef == null) return Reject("unknown_class");
            if (!_monsters.TryGetValue(monsterId, out MonsterDef monster)) return Reject("unknown_monster");
            if (!MapScope.Includes(monster.MapId, character.MapId)) return Reject("target_not_on_character_map");

            _states.TryGetValue(characterId, out ActiveCombatState prior);
            ActiveSimResult simulation = ActiveSim.Tick(new ActiveSimInput
            {
                Timestamp = timestamp,
                Character = character,
                Class = classDef,
                Monster = monster,
                TargetEntityId = targetEntityId,
                ItemDefs = _items,
                MonsterDefs = _monsters,
                State = prior,
                World = world ?? new CombatWorldFacts(),
                Commands = commands,
                Bonuses = bonuses ?? AccountBonuses.Zero(),
                Config = Config,
            }, _random);
            _states[characterId] = simulation.State.Clone();

            List<CombatEvent> killedEvents = FindEnemyKilledEvents(simulation.Events);
            if (killedEvents.Count == 0)
                return new ActiveCombatTickResult { Account = account.Clone(), Simulation = simulation };

            CombatReward reward = new CombatReward();
            AccountBonuses effectiveBonuses = bonuses ?? AccountBonuses.Zero();
            foreach (CombatEvent killedEvent in killedEvents)
            {
                MonsterDef lootMonster = FindMonsterForActor(world, killedEvent.TargetId, monster);
                CombatReward killReward = ResolveReward(account, character, classDef, monster, effectiveBonuses);
                killReward.Loot = RollLootForFeed(lootMonster, effectiveBonuses);
                MergeReward(reward, killReward);
                reward.KillLoot.Add(new KillLootRecord
                {
                    ActorEntityId = killedEvent.TargetId,
                    MonsterId = lootMonster.Id,
                    Coins = killReward.Coins,
                    Stacks = CloneStacks(killReward.Loot),
                });
            }
            reward.KillCount = killedEvents.Count;
            Account updated = ApplyReward(account, characterId, reward);
            return new ActiveCombatTickResult
            {
                Account = updated,
                Simulation = simulation,
                Reward = reward,
            };
        }

        public void ClearCharacterState(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId)) _states.Remove(characterId);
        }

        public ActiveCombatState PrepareNextEncounter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || !_states.TryGetValue(characterId, out ActiveCombatState state)) return null;

            state.EnemyHp = 0;
            state.KillResolved = false;
            state.TargetId = null;
            state.EnemyNextAttackAt = 0;
            state.EnemyNextAttackAtByActorId?.Clear();
            foreach (string defeatedActorId in state.DefeatedActorIds ?? new List<string>())
                state.EnemyHpByActorId?.Remove(defeatedActorId);
            _states[characterId] = state.Clone();
            return _states[characterId].Clone();
        }

        private CombatReward ResolveReward(Account account, Character character, ClassDef classDef, MonsterDef monster, AccountBonuses bonuses)
        {
            PassiveMultipliers passive = classDef.Passive?.Multipliers;
            double xpMultiplier = (passive?.Xp ?? 1.0) * (1.0 + bonuses.XpPct);
            long xp = (long)Math.Floor(monster.Xp * xpMultiplier);
            int coins = monster.Coins == null ? 0 : _random.NextIntInclusive(monster.Coins.Min, monster.Coins.Max);

            return new CombatReward
            {
                CharacterXp = xp,
                CombatSkillXp = xp,
                Coins = coins,
                Loot = new List<ItemStack>(),
            };
        }

        private List<ItemStack> RollLootForFeed(MonsterDef monster, AccountBonuses bonuses)
        {
            if (monster?.Drops == null) return new List<ItemStack>();

            // Unknown item IDs are deliberately kept: they surface in the loot bag/feed
            // instead of being silently destroyed (pickup leaves them as remaining stacks).
            var loot = new List<ItemStack>();
            foreach (ItemStack stack in DropSystem.RollDropTable(monster.Drops, _random, bonuses.DropPct))
            {
                if (stack == null || stack.Qty <= 0) continue;
                loot.Add(new ItemStack { ItemId = stack.ItemId, Qty = stack.Qty });
            }
            return loot;
        }

        private Account ApplyReward(Account account, string characterId, CombatReward reward)
        {
            Character character = FindCharacter(account, characterId);
            CharacterXpResult characterXpResult = CharacterHelper.ApplyCharacterXp(character, reward.CharacterXp);
            Character updatedCharacter = characterXpResult.Character;
            SkillXpResult skillXpResult = CharacterHelper.ApplySkillXp(updatedCharacter, SkillId.Combat, reward.CombatSkillXp);
            updatedCharacter = skillXpResult.Character;
            reward.CharacterPreviousLevel = characterXpResult.PreviousLevel;
            reward.CharacterNewLevel = characterXpResult.NewLevel;
            reward.CombatSkillPreviousLevel = skillXpResult.PreviousLevel;
            reward.CombatSkillNewLevel = skillXpResult.NewLevel;
            Bank updatedBank = BankHelper.AddCoins(account.Bank, reward.Coins);

            Account updated = AccountHelper.UpdateCharacter(account, characterId, _ => updatedCharacter);
            updated.Bank = updatedBank;
            return updated;
        }

        private static List<CombatEvent> FindEnemyKilledEvents(List<CombatEvent> events)
        {
            var killed = new List<CombatEvent>();
            if (events == null) return killed;
            foreach (CombatEvent combatEvent in events)
                if (combatEvent != null && combatEvent.Kind == CombatEventKind.EnemyKilled) killed.Add(combatEvent);
            return killed;
        }

        private MonsterDef FindMonsterForActor(CombatWorldFacts world, string actorEntityId, MonsterDef fallback)
        {
            foreach (CombatSpatialSnapshot actor in world?.Spatial?.Actors ?? new List<CombatSpatialSnapshot>())
            {
                if (actor == null || actor.ActorId != actorEntityId) continue;
                if (_monsters.TryGetValue(actor.DefinitionId, out MonsterDef monster)) return monster;
                break;
            }
            return fallback;
        }

        private static void MergeReward(CombatReward aggregate, CombatReward reward)
        {
            aggregate.CharacterXp = checked(aggregate.CharacterXp + reward.CharacterXp);
            aggregate.CombatSkillXp = checked(aggregate.CombatSkillXp + reward.CombatSkillXp);
            aggregate.Coins = checked(aggregate.Coins + reward.Coins);
            aggregate.Loot.AddRange(reward.Loot);
        }

        private static List<ItemStack> CloneStacks(List<ItemStack> stacks)
        {
            var copy = new List<ItemStack>();
            foreach (ItemStack stack in stacks ?? new List<ItemStack>())
            {
                if (stack == null || stack.Qty <= 0) continue;
                copy.Add(new ItemStack { ItemId = stack.ItemId, Qty = stack.Qty });
            }
            return copy;
        }

        private static Character FindCharacter(Account account, string characterId)
        {
            foreach (Character character in account.Characters ?? new List<Character>())
                if (character != null && character.Id == characterId) return character;
            return null;
        }

        private static ActiveCombatTickResult Reject(string reason)
            => new ActiveCombatTickResult { RejectionReason = reason };
    }
}
