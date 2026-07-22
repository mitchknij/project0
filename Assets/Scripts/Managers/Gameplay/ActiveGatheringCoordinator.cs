using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;

namespace IdleCloud.Managers
{
    public sealed class ActiveGatheringTickResult
    {
        public Account Account;
        public ActiveGatheringResult Simulation;
        public List<ItemStack> Overflow = new List<ItemStack>();
        public SkillId SkillId;
        public int SkillPreviousLevel;
        public int SkillNewLevel;
        public string RejectionReason;
    }

    public sealed class ActiveGatheringCoordinator
    {
        private readonly Dictionary<string, ActiveGatheringState> _states = new Dictionary<string, ActiveGatheringState>();
        private readonly IRandomSource _random;
        private readonly Dictionary<string, ResourceNodeDef> _nodes;
        private readonly Dictionary<string, ItemDef> _items;

        public ActiveGatheringCoordinator(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _nodes = new Dictionary<string, ResourceNodeDef>(RuntimeContent.Nodes);
            _items = new Dictionary<string, ItemDef>(RuntimeContent.Items);
        }

        public ActiveGatheringTickResult Tick(Account account, string characterId, string targetEntityId, string nodeId,
            GatheringWorldFacts world, List<GatheringCommand> commands, long timestamp, AccountBonuses bonuses)
        {
            Character character = FindCharacter(account, characterId);
            if (character == null || !_nodes.TryGetValue(nodeId, out ResourceNodeDef node) ||
                !RuntimeContent.All.TryGetValue(character?.ClassId ?? ClassId.Beginner, out ClassDef classDef))
                return new ActiveGatheringTickResult { RejectionReason = "invalid_gathering_context" };

            _states.TryGetValue(characterId, out ActiveGatheringState state);
            ActiveGatheringResult simulation = LifeSkills.Tick(new ActiveGatheringInput
            {
                Timestamp = timestamp, Character = character, Class = classDef, Node = node,
                TargetEntityId = targetEntityId, ItemDefs = _items, Bonuses = bonuses ?? AccountBonuses.Zero(),
                State = state, World = world ?? new GatheringWorldFacts(), Commands = commands,
            }, _random);
            _states[characterId] = simulation.State.Clone();
            if (simulation.SuccessfulActions == 0)
                return new ActiveGatheringTickResult { Account = account.Clone(), Simulation = simulation };

            SkillXpResult skillXpResult = CharacterHelper.ApplySkillXp(
                character,
                ActivitySkillMapping.ToSkillId(ActivitySkillMapping.ToActivityKind(node.Skill)),
                simulation.SkillXp);
            Character updatedCharacter = skillXpResult.Character;
            Bank updatedBank = account.Bank.Clone();
            var overflow = new List<ItemStack>();
            foreach (ItemStack stack in simulation.Loot)
            {
                if (!_items.TryGetValue(stack.ItemId, out ItemDef def)) { overflow.Add(stack); continue; }
                var add = Inventory.AddToInventory(updatedCharacter, stack, def);
                updatedCharacter = add.Character;
                if (add.Overflow > 0) overflow.Add(new ItemStack { ItemId = stack.ItemId, Qty = add.Overflow });
            }
            Account updated = AccountHelper.UpdateCharacter(account, characterId, _ => updatedCharacter);
            updated.Bank = updatedBank;
            return new ActiveGatheringTickResult
            {
                Account = updated,
                Simulation = simulation,
                Overflow = overflow,
                SkillId = skillXpResult.SkillId,
                SkillPreviousLevel = skillXpResult.PreviousLevel,
                SkillNewLevel = skillXpResult.NewLevel,
            };
        }

        public void ClearCharacterState(string characterId) => _states.Remove(characterId);

        private static Character FindCharacter(Account account, string characterId)
        {
            foreach (Character character in account?.Characters ?? new List<Character>())
                if (character?.Id == characterId) return character;
            return null;
        }
    }
}
