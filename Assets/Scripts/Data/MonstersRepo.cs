// MonstersRepo.cs — Statische monster-definities (vertaling van src/data/monsters.ts).
// 1:1 met de TypeScript-bron; OSRS-stijl droptabellen exact overgenomen.

using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Data
{
    public static class MonstersRepo
    {
        public static readonly Dictionary<string, MonsterDef> All = new Dictionary<string, MonsterDef>
        {
            {
                "slime", new MonsterDef
                {
                    Id       = "slime",
                    Name     = "Slime",
                    MapId    = "grass_1",
                    Hp       = 12,
                    Damage   = 2,
                    Defense  = 1,
                    Accuracy = 10,
                    Agility  = 8,
                    Xp       = 8,
                    Coins    = new CoinRange { Min = 1, Max = 3 },
                    Element  = Element.Nature,
                    Behavior = MonsterBehavior.Melee,
                    RespawnMs = 14000,
                    Drops    = new DropTable
                    {
                        Always = new List<DropItem>
                        {
                            new DropItem { ItemId = "wolf_pelt", Min = 1, Max = 2 },
                        },
                        Main = new WeightedTable
                        {
                            Rolls = 1,
                            Slots = new List<WeightedSlot>
                            {
                                new WeightedSlot { Weight = 84, Nothing = true },
                                new WeightedSlot { Weight = 32, ItemId = "small_hp_potion", Min = 1, Max = 1 },
                                new WeightedSlot { Weight = 11, ItemId = "wolf_pelt",       Min = 1, Max = 3 },
                                new WeightedSlot { Weight = 1,  ItemId = "small_hp_potion", Min = 2, Max = 3 },
                            },
                        },
                    },
                }
            },
            {
                "world_slime", new MonsterDef
                {
                    Id       = "world_slime",
                    Name     = "Wandering Slime",
                    MapId    = "*",
                    Hp       = 12,
                    Damage   = 2,
                    Defense  = 1,
                    Accuracy = 10,
                    Agility  = 8,
                    Xp       = 8,
                    Coins    = new CoinRange { Min = 1, Max = 3 },
                    Element  = Element.Nature,
                    Behavior = MonsterBehavior.Melee,
                    RespawnMs = 14000,
                    Drops    = new DropTable
                    {
                        Always = new List<DropItem>
                        {
                            new DropItem { ItemId = "wolf_pelt", Min = 1, Max = 2 },
                        },
                        Main = new WeightedTable
                        {
                            Rolls = 1,
                            Slots = new List<WeightedSlot>
                            {
                                new WeightedSlot { Weight = 84, Nothing = true },
                                new WeightedSlot { Weight = 32, ItemId = "small_hp_potion", Min = 1, Max = 1 },
                                new WeightedSlot { Weight = 11, ItemId = "wolf_pelt",       Min = 1, Max = 3 },
                                new WeightedSlot { Weight = 1, ItemId = "small_hp_potion", Min = 2, Max = 3 },
                            },
                        },
                    },
                }
            },
        };

        /// <summary>Retourneert de MonsterDef voor het gegeven id, of null wanneer niet gevonden.</summary>
        public static MonsterDef Get(string id) => All.TryGetValue(id, out MonsterDef def) ? def : null;
    }
}
