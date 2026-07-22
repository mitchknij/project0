// RecipesRepo.cs — Statische recept-definities (vertaling van src/data/recipes.ts).
// 1:1 met de TypeScript-bron; inputs-volgorde behouden.

using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Data
{
    public static class RecipesRepo
    {
        public static readonly Dictionary<string, RecipeDef> All = new Dictionary<string, RecipeDef>
        {
            // ── Tools ─────────────────────────────────────────────────────────────
            { "recipe_copper_pickaxe", R("recipe_copper_pickaxe", "Copper Pickaxe", "copper_pickaxe", 1, 30, 1,
                I("copper_ore",6), I("oak_log",4)) },
            { "recipe_iron_pickaxe",   R("recipe_iron_pickaxe",   "Iron Pickaxe",   "iron_pickaxe",   1, 150, 9,
                I("iron_ore",10), I("birch_log",5)) },
            { "recipe_iron_axe",       R("recipe_iron_axe",       "Iron Axe",       "iron_axe",       1, 140, 9,
                I("iron_ore",10), I("birch_log",6)) },
            { "recipe_gold_pickaxe",   R("recipe_gold_pickaxe",   "Gold Pickaxe",   "gold_pickaxe",   1, 600, 24,
                I("gold_ore",8), I("birch_log",6)) },
            { "recipe_gold_axe",       R("recipe_gold_axe",       "Gold Axe",       "gold_axe",       1, 580, 24,
                I("gold_ore",8), I("swamp_log",4)) },
            { "recipe_mithril_pickaxe",R("recipe_mithril_pickaxe","Mithril Pickaxe","mithril_pickaxe",1, 4000, 54,
                I("mithril_ore",12), I("ancient_wood",6)) },
            { "recipe_mithril_axe",    R("recipe_mithril_axe",    "Mithril Axe",    "mithril_axe",    1, 3800, 54,
                I("mithril_ore",10), I("ancient_wood",8)) },

            // ── Weapons ───────────────────────────────────────────────────────────
            { "recipe_wooden_bow",   R("recipe_wooden_bow",   "Wooden Bow",   "wooden_bow",   1, 15,   1,  I("oak_log",8)) },
            { "recipe_copper_sword", R("recipe_copper_sword", "Copper Sword", "copper_sword", 1, 50,   4,  I("copper_ore",10), I("oak_log",5)) },
            { "recipe_iron_sword",   R("recipe_iron_sword",   "Iron Sword",   "iron_sword",   1, 200,  11, I("iron_ore",12), I("birch_log",6)) },
            { "recipe_iron_bow",     R("recipe_iron_bow",     "Iron Bow",     "iron_bow",     1, 210,  11, I("iron_ore",8), I("birch_log",10)) },
            { "recipe_gold_sword",   R("recipe_gold_sword",   "Gold Sword",   "gold_sword",   1, 700,  19, I("gold_ore",14), I("iron_ore",6)) },
            { "recipe_gold_bow",     R("recipe_gold_bow",     "Gold Bow",     "gold_bow",     1, 720,  19, I("gold_ore",10), I("vine_fiber",12)) },
            { "recipe_vine_blade",   R("recipe_vine_blade",   "Vine Blade",   "vine_blade",   1, 1400, 27, I("vine_fiber",20), I("gold_ore",10), I("wolf_pelt",8)) },
            { "recipe_vine_bow",     R("recipe_vine_bow",     "Vine Bow",     "vine_bow",     1, 1350, 27, I("vine_fiber",28), I("birch_log",12)) },
            { "recipe_poison_blade", R("recipe_poison_blade", "Poison Blade", "poison_blade", 1, 3000, 37, I("venom_gland",10), I("vine_fiber",15), I("gold_ore",12)) },
            { "recipe_swamp_staff",  R("recipe_swamp_staff",  "Swamp Staff",  "swamp_staff",  1, 2800, 37, I("swamp_log",15), I("venom_gland",8), I("bog_moss",12)) },
            { "recipe_swamp_bow",    R("recipe_swamp_bow",    "Swamp Bow",    "swamp_bow",    1, 3100, 37, I("swamp_log",18), I("venom_gland",6), I("vine_fiber",10)) },
            { "recipe_frost_blade",  R("recipe_frost_blade",  "Frost Blade",  "frost_blade",  1, 7000, 49, I("glacier_shard",12), I("frozen_log",8), I("mithril_ore",5)) },
            { "recipe_ice_bow",      R("recipe_ice_bow",      "Ice Bow",      "ice_bow",      1, 7200, 49, I("frozen_log",16), I("frost_crystal",14), I("glacier_shard",8)) },
            { "recipe_mithril_sword",R("recipe_mithril_sword","Mithril Sword","mithril_sword",1, 15000,64, I("mithril_ore",20), I("ancient_ice",8), I("ancient_wood",6)) },
            { "recipe_mithril_bow",  R("recipe_mithril_bow",  "Mithril Bow",  "mithril_bow",  1, 15500,64, I("mithril_ore",14), I("ancient_wood",18), I("ancient_ice",6)) },

            // ── Armor — Leather ───────────────────────────────────────────────────
            { "recipe_leather_cap",   R("recipe_leather_cap",   "Leather Cap",   "leather_cap",   1, 20,  1,  I("slime_goo",8)) },
            { "recipe_leather_tunic", R("recipe_leather_tunic", "Leather Tunic", "leather_tunic", 1, 60,  3,  I("slime_goo",15), I("bean_pod",6)) },
            { "recipe_leather_pants", R("recipe_leather_pants", "Leather Pants", "leather_pants", 1, 50,  3,  I("slime_goo",12), I("bean_pod",5)) },

            // ── Armor — Iron ──────────────────────────────────────────────────────
            { "recipe_iron_helm",      R("recipe_iron_helm",      "Iron Helm",      "iron_helm",      1, 180,  13, I("iron_ore",8),   I("stone_shard",6)) },
            { "recipe_iron_chestplate",R("recipe_iron_chestplate","Iron Chestplate","iron_chestplate",1, 400,  13, I("iron_ore",18),  I("stone_shard",10)) },
            { "recipe_iron_greaves",   R("recipe_iron_greaves",   "Iron Greaves",   "iron_greaves",   1, 320,  13, I("iron_ore",14),  I("stone_shard",8)) },

            // ── Armor — Gold ──────────────────────────────────────────────────────
            { "recipe_gold_helm",      R("recipe_gold_helm",      "Gold Helm",      "gold_helm",      1, 600,  21, I("gold_ore",10),  I("rune_fragment",6)) },
            { "recipe_gold_chestplate",R("recipe_gold_chestplate","Gold Chestplate","gold_chestplate",1, 1300, 21, I("gold_ore",22),  I("rune_fragment",12)) },
            { "recipe_gold_greaves",   R("recipe_gold_greaves",   "Gold Greaves",   "gold_greaves",   1, 1000, 21, I("gold_ore",16),  I("rune_fragment",8)) },

            // ── Armor — Bog/Swamp ─────────────────────────────────────────────────
            { "recipe_bog_cowl",      R("recipe_bog_cowl",      "Bog Cowl",      "bog_cowl",      1, 1600, 39, I("swamp_log",10), I("bog_moss",14), I("venom_gland",4)) },
            { "recipe_bog_robe",      R("recipe_bog_robe",      "Bog Robe",      "bog_robe",      1, 3500, 39, I("swamp_log",20), I("bog_moss",28), I("venom_gland",10)) },
            { "recipe_bog_leggings",  R("recipe_bog_leggings",  "Bog Leggings",  "bog_leggings",  1, 2800, 39, I("swamp_log",16), I("bog_moss",20), I("venom_gland",6)) },

            // ── Armor — Frost/Ice ─────────────────────────────────────────────────
            { "recipe_frost_helm",       R("recipe_frost_helm",       "Frost Helm",       "frost_helm",       1, 4000,  51, I("frost_crystal",12), I("glacier_shard",8),  I("frozen_log",6)) },
            { "recipe_frost_chestplate", R("recipe_frost_chestplate", "Frost Chestplate", "frost_chestplate", 1, 9000,  51, I("glacier_shard",18), I("frost_crystal",20), I("mithril_ore",4)) },
            { "recipe_frost_greaves",    R("recipe_frost_greaves",    "Frost Greaves",    "frost_greaves",    1, 7500,  51, I("glacier_shard",14), I("frost_crystal",16), I("frozen_log",8)) },

            // ── Armor — Mithril ───────────────────────────────────────────────────
            { "recipe_mithril_helm",    R("recipe_mithril_helm",    "Mithril Helm",    "mithril_helm",    1, 9000,  65, I("mithril_ore",14), I("ancient_ice",8),  I("glacier_shard",10)) },
            { "recipe_mithril_plate",   R("recipe_mithril_plate",   "Mithril Plate",   "mithril_plate",   1, 20000, 65, I("mithril_ore",30), I("ancient_ice",16), I("ancient_wood",10)) },
            { "recipe_mithril_greaves", R("recipe_mithril_greaves", "Mithril Greaves", "mithril_greaves", 1, 16000, 65, I("mithril_ore",22), I("ancient_ice",12), I("glacier_shard",12)) },

            // ── Alchemy ───────────────────────────────────────────────────────────
            { "recipe_health_tonic",    R("recipe_health_tonic",    "Health Tonic",    "health_tonic",    3, 15,   1,  I("wildflower",4), I("bog_moss",2)) },
            { "recipe_strength_tonic",  R("recipe_strength_tonic",  "Strength Tonic",  "strength_tonic",  2, 40,   8,  I("stone_herb",4), I("sunleaf",3)) },
            { "recipe_swift_tonic",     R("recipe_swift_tonic",     "Swift Tonic",     "swift_tonic",     2, 45,   10, I("sunleaf",4), I("wolf_pelt",2)) },
            { "recipe_sage_tonic",      R("recipe_sage_tonic",      "Sage Tonic",      "sage_tonic",      2, 120,  20, I("runic_moss",4), I("birch_log",4)) },
            { "recipe_elixir_of_luck",  R("recipe_elixir_of_luck",  "Elixir of Luck",  "elixir_of_luck",  2, 250,  30, I("moonberry",5), I("gloomcap",3), I("poison_herb",3)) },
            { "recipe_grand_elixir",    R("recipe_grand_elixir",    "Grand Elixir",    "grand_elixir",    1, 1200, 55, I("moon_bloom",3), I("ice_crystal_herb",4), I("frost_fern",6), I("ancient_ice",2)) },
        };

        /// <summary>Retourneert de RecipeDef voor het gegeven id, of null wanneer niet gevonden.</summary>
        public static RecipeDef Get(string id) => All.TryGetValue(id, out RecipeDef def) ? def : null;

        // ── Fabriekshulpers ───────────────────────────────────────────────────────

        private static RecipeDef R(
            string id, string name, string outputItemId, int outputQty,
            int coinCost, int levelReq,
            params ItemStack[] inputs)
            => new RecipeDef
            {
                Id           = id,
                Name         = name,
                OutputItemId = outputItemId,
                OutputQty    = outputQty,
                CoinCost     = coinCost,
                LevelReq     = levelReq,
                Inputs       = new List<ItemStack>(inputs),
            };

        private static ItemStack I(string itemId, int qty)
            => new ItemStack { ItemId = itemId, Qty = qty };
    }
}
