// ItemsRepo.cs — Statische item-definities (vertaling van src/data/items.ts).
// 1:1 met de TypeScript-bron; bonuses als CoreStats-velden, slot als nullable EquipSlot.

using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Data
{
    public static class ItemsRepo
    {
        public static readonly Dictionary<string, ItemDef> All = new Dictionary<string, ItemDef>
        {
            // ── Ores ──────────────────────────────────────────────────────────────
            { "copper_ore",     Mat("copper_ore",    "Copper Ore",    999, 2)  },
            { "iron_ore",       Mat("iron_ore",      "Iron Ore",      999, 5)  },
            { "gold_ore",       Mat("gold_ore",      "Gold Ore",      999, 12) },
            { "mithril_ore",    Mat("mithril_ore",   "Mithril Ore",   999, 30) },

            // ── Logs ──────────────────────────────────────────────────────────────
            { "oak_log",        Mat("oak_log",       "Oak Log",       999, 2)  },
            { "birch_log",      Mat("birch_log",     "Birch Log",     999, 5)  },
            { "maple_log",      Mat("maple_log",     "Maple Log",     999, 12) },
            { "swamp_log",      Mat("swamp_log",     "Swamp Log",     999, 18) },
            { "frozen_log",     Mat("frozen_log",    "Frozen Log",    999, 22) },
            { "ancient_wood",   Mat("ancient_wood",  "Ancient Wood",  999, 50) },

            // ── Monster Drops ─────────────────────────────────────────────────────
            { "bear_pelt",      Mat("bear_pelt",     "Bear Pelt",     999, 14) },
            { "bear_claw",      Mat("bear_claw",     "Bear Claw",     999, 22) },
            { "slime_goo",      Mat("slime_goo",     "Slime Goo",     999, 3)  },
            { "mush_cap",       Mat("mush_cap",      "Mush Cap",      999, 4)  },
            { "bean_pod",       Mat("bean_pod",      "Bean Pod",      999, 6)  },
            { "spore_dust",     Mat("spore_dust",    "Spore Dust",    999, 8)  },
            { "wolf_pelt",      Mat("wolf_pelt",     "Wolf Pelt",     999, 10) },
            { "vine_fiber",     Mat("vine_fiber",    "Vine Fiber",    999, 8)  },
            { "stone_shard",    Mat("stone_shard",   "Stone Shard",   999, 5)  },
            { "rune_fragment",  Mat("rune_fragment", "Rune Fragment", 999, 15) },
            { "crystal_shard",  Mat("crystal_shard", "Crystal Shard",999, 25) },
            { "bog_moss",       Mat("bog_moss",      "Bog Moss",      999, 12) },
            { "venom_gland",    Mat("venom_gland",   "Venom Gland",   999, 20) },
            { "frost_crystal",  Mat("frost_crystal", "Frost Crystal", 999, 18) },
            { "glacier_shard",  Mat("glacier_shard", "Glacier Shard", 999, 35) },
            { "ancient_ice",    Mat("ancient_ice",   "Ancient Ice",   999, 60) },

            // ── Herbs (Gathering) ─────────────────────────────────────────────────
            { "wildflower",       Mat("wildflower",       "Wildflower",       999, 3)  },
            { "sunleaf",          Mat("sunleaf",          "Sunleaf",          999, 7)  },
            { "moonberry",        Mat("moonberry",        "Moonberry",        999, 14) },
            { "stone_herb",       Mat("stone_herb",       "Stone Herb",       999, 6)  },
            { "runic_moss",       Mat("runic_moss",       "Runic Moss",       999, 20) },
            { "bog_plant",        Mat("bog_plant",        "Bog Plant",        999, 10) },
            { "poison_herb",      Mat("poison_herb",      "Poison Herb",      999, 18) },
            { "gloomcap",         Mat("gloomcap",         "Gloomcap",         999, 28) },
            { "frost_fern",       Mat("frost_fern",       "Frost Fern",       999, 22) },
            { "ice_crystal_herb", Mat("ice_crystal_herb", "Ice Crystal Herb", 999, 40) },
            { "moon_bloom",       Mat("moon_bloom",       "Moon Bloom",       999, 65) },

            // ── Consumables ───────────────────────────────────────────────────────
            { "small_hp_potion",  Con("small_hp_potion",  "Small HP Potion",  50, 10)  },
            { "medium_hp_potion", Con("medium_hp_potion", "Medium HP Potion", 50, 35)  },
            { "large_hp_potion",  Con("large_hp_potion",  "Large HP Potion",  50, 80)  },
            { "small_mp_potion",  Con("small_mp_potion",  "Small MP Potion",  50, 10)  },
            { "cooked_mushroom",  Con("cooked_mushroom",  "Cooked Mushroom",  50, 6)   },
            { "health_tonic",     Con("health_tonic",     "Health Tonic",     50, 18)  },
            { "strength_tonic",   Con("strength_tonic",   "Strength Tonic",   50, 30)  },
            { "swift_tonic",      Con("swift_tonic",      "Swift Tonic",      50, 30)  },
            { "sage_tonic",       Con("sage_tonic",       "Sage Tonic",       50, 35)  },
            { "elixir_of_luck",   Con("elixir_of_luck",  "Elixir of Luck",   50, 55)  },
            { "grand_elixir",     Con("grand_elixir",     "Grand Elixir",     50, 120) },

            // ── Weapons — Swords ──────────────────────────────────────────────────
            { "wooden_sword",  Equip("wooden_sword",  "Wooden Sword",  15,   EquipSlot.Weapon, 1,  B(2,0,0,0)) },
            { "copper_sword",  Equip("copper_sword",  "Copper Sword",  40,   EquipSlot.Weapon, 5,  B(5,0,0,0)) },
            { "iron_sword",    Equip("iron_sword",    "Iron Sword",    120,  EquipSlot.Weapon, 12, B(11,1,0,0)) },
            { "gold_sword",    Equip("gold_sword",    "Gold Sword",    350,  EquipSlot.Weapon, 20, B(20,3,0,0)) },
            { "vine_blade",    Equip("vine_blade",    "Vine Blade",    600,  EquipSlot.Weapon, 28, B(32,5,0,3)) },
            { "poison_blade",  Equip("poison_blade",  "Poison Blade",  1200, EquipSlot.Weapon, 38, B(48,5,0,8)) },
            { "frost_blade",   Equip("frost_blade",   "Frost Blade",   2500, EquipSlot.Weapon, 50, B(72,8,0,8)) },
            { "mithril_sword", Equip("mithril_sword", "Mithril Sword", 5000, EquipSlot.Weapon, 65, B(105,10,0,12)) },

            // ── Weapons — Staves ──────────────────────────────────────────────────
            { "swamp_staff",   Equip("swamp_staff",   "Swamp Staff",   1100, EquipSlot.Weapon, 38, B(0,0,22,10)) },

            // ── Weapons — Bows ────────────────────────────────────────────────────
            { "wooden_bow",    Equip("wooden_bow",    "Wooden Bow",    15,   EquipSlot.Weapon, 1,  B(0,2,0,0)) },
            { "iron_bow",      Equip("iron_bow",      "Iron Bow",      130,  EquipSlot.Weapon, 12, B(0,9,0,2)) },
            { "gold_bow",      Equip("gold_bow",      "Gold Bow",      360,  EquipSlot.Weapon, 20, B(0,16,0,5)) },
            { "vine_bow",      Equip("vine_bow",      "Vine Bow",      620,  EquipSlot.Weapon, 28, B(0,26,0,8)) },
            { "swamp_bow",     Equip("swamp_bow",     "Swamp Bow",     1250, EquipSlot.Weapon, 38, B(0,40,0,12)) },
            { "ice_bow",       Equip("ice_bow",       "Ice Bow",       2600, EquipSlot.Weapon, 50, B(0,60,0,16)) },
            { "mithril_bow",   Equip("mithril_bow",   "Mithril Bow",   5200, EquipSlot.Weapon, 65, B(0,88,0,22)) },

            // ── Tools — Pickaxes ──────────────────────────────────────────────────
            { "copper_pickaxe",  Equip("copper_pickaxe",  "Copper Pickaxe",  25,   EquipSlot.Tool, 1,  B(1,0,0,0)) },
            { "iron_pickaxe",    Equip("iron_pickaxe",    "Iron Pickaxe",    100,  EquipSlot.Tool, 10, B(4,0,0,0)) },
            { "gold_pickaxe",    Equip("gold_pickaxe",    "Gold Pickaxe",    380,  EquipSlot.Tool, 25, B(8,0,0,0)) },
            { "mithril_pickaxe", Equip("mithril_pickaxe", "Mithril Pickaxe", 2200, EquipSlot.Tool, 55, B(18,0,0,0)) },

            // ── Tools — Axes ──────────────────────────────────────────────────────
            { "iron_axe",    Equip("iron_axe",    "Iron Axe",    95,   EquipSlot.Tool, 10, B(0,0,4,0)) },
            { "gold_axe",    Equip("gold_axe",    "Gold Axe",    360,  EquipSlot.Tool, 25, B(0,0,8,0)) },
            { "mithril_axe", Equip("mithril_axe", "Mithril Axe", 2000, EquipSlot.Tool, 55, B(0,0,16,0)) },

            // ── Armor — Leather (Lv 1–3) ──────────────────────────────────────────
            { "leather_cap",   Equip("leather_cap",   "Leather Cap",   12, EquipSlot.Helmet, 1, B(0,1,0,0)) },
            { "leather_tunic", Equip("leather_tunic", "Leather Tunic", 30, EquipSlot.Chest,  3, B(1,1,0,0)) },
            { "leather_pants", Equip("leather_pants", "Leather Pants", 25, EquipSlot.Legs,   3, B(0,1,0,1)) },

            // ── Armor — Iron (Lv 14) ──────────────────────────────────────────────
            { "iron_helm",       Equip("iron_helm",       "Iron Helm",       95,  EquipSlot.Helmet, 14, B(2,2,0,0)) },
            { "iron_chestplate", Equip("iron_chestplate", "Iron Chestplate", 220, EquipSlot.Chest,  14, B(5,2,0,0)) },
            { "iron_greaves",    Equip("iron_greaves",    "Iron Greaves",    180, EquipSlot.Legs,   14, B(0,3,0,2)) },

            // ── Armor — Gold (Lv 22) ──────────────────────────────────────────────
            { "gold_helm",       Equip("gold_helm",       "Gold Helm",       280, EquipSlot.Helmet, 22, B(4,4,0,0)) },
            { "gold_chestplate", Equip("gold_chestplate", "Gold Chestplate", 600, EquipSlot.Chest,  22, B(8,5,0,0)) },
            { "gold_greaves",    Equip("gold_greaves",    "Gold Greaves",    500, EquipSlot.Legs,   22, B(0,5,0,5)) },

            // ── Armor — Bog/Swamp (Lv 40) ─────────────────────────────────────────
            { "bog_cowl",     Equip("bog_cowl",     "Bog Cowl",     700,  EquipSlot.Helmet, 40, B(0,0,8,4)) },
            { "bog_robe",     Equip("bog_robe",     "Bog Robe",     1400, EquipSlot.Chest,  40, B(4,0,12,5)) },
            { "bog_leggings", Equip("bog_leggings", "Bog Leggings", 1200, EquipSlot.Legs,   40, B(0,0,8,8)) },

            // ── Armor — Frost/Ice (Lv 52) ─────────────────────────────────────────
            { "frost_helm",       Equip("frost_helm",       "Frost Helm",       1800, EquipSlot.Helmet, 52, B(0,8,0,6)) },
            { "frost_chestplate", Equip("frost_chestplate", "Frost Chestplate", 3800, EquipSlot.Chest,  52, B(12,8,0,4)) },
            { "frost_greaves",    Equip("frost_greaves",    "Frost Greaves",    3200, EquipSlot.Legs,   52, B(0,8,0,10)) },

            // ── Armor — Mithril (Lv 66) ───────────────────────────────────────────
            { "mithril_helm",    Equip("mithril_helm",    "Mithril Helm",    4000, EquipSlot.Helmet, 66, B(8,12,0,8)) },
            { "mithril_plate",   Equip("mithril_plate",   "Mithril Plate",   8500, EquipSlot.Chest,  66, B(20,10,0,6)) },
            { "mithril_greaves", Equip("mithril_greaves", "Mithril Greaves", 7000, EquipSlot.Legs,   66, B(0,12,0,14)) },
        };

        /// <summary>Retourneert de ItemDef voor het gegeven id, of null wanneer niet gevonden.</summary>
        public static ItemDef Get(string id) => All.TryGetValue(id, out ItemDef def) ? def : null;

        // ── Fabriekshulpers ───────────────────────────────────────────────────────

        private static ItemDef Mat(string id, string name, int stackLimit, int sellValue)
            => new ItemDef { Id = id, Name = name, Type = ItemType.Material, StackLimit = stackLimit, SellValue = sellValue };

        private static ItemDef Con(string id, string name, int stackLimit, int sellValue)
            => new ItemDef { Id = id, Name = name, Type = ItemType.Consumable, StackLimit = stackLimit, SellValue = sellValue };

        private static ItemDef Equip(string id, string name, int sellValue, EquipSlot slot, int levelReq, CoreStats bonuses)
            => new ItemDef
            {
                Id         = id,
                Name       = name,
                Type       = ItemType.Equipment,
                StackLimit = 1,
                SellValue  = sellValue,
                Slot       = slot,
                LevelReq   = levelReq,
                Bonuses    = bonuses,
            };

        /// <summary>Maakt een CoreStats-bonus aan (str, agi, wis, lck).</summary>
        private static CoreStats B(int str, int agi, int wis, int lck)
            => new CoreStats { Strength = str, Agility = agi, Wisdom = wis, Luck = lck };
    }
}
