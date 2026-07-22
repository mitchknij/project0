// NodesRepo.cs — Statische resource-node-definities (vertaling van src/data/nodes.ts).
// 1:1 met de TypeScript-bron; verbatim inclusief de originele mapId-strings.

using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Data
{
    public static class NodesRepo
    {
        public static readonly Dictionary<string, ResourceNodeDef> All = new Dictionary<string, ResourceNodeDef>
        {
            // ── Forest Chopping ──────────────────────────────────────────────────
            { "oak_tree",         N("oak_tree",         "Oak Tree",              "grass_1", HarvestSkill.Chopping, 1,  4000,  10, D("oak_log",    1.0, 1, 2)) },
            { "oak_thicket",      N("oak_thicket",      "Oak Thicket",           "grass_1",  HarvestSkill.Chopping, 5,  4200,  16, D("oak_log",    1.0, 1, 3)) },
            { "birch_tree",       N("birch_tree",       "Birch Tree",            "grass_1",  HarvestSkill.Chopping, 12, 6000,  28, D("birch_log",  1.0, 1, 2)) },

            // ── Swamp Chopping ───────────────────────────────────────────────────
            { "swamp_willow",     N("swamp_willow",     "Swamp Willow",          "grass_1", HarvestSkill.Chopping, 18, 8000,   50, D("swamp_log",   1.0, 1, 2)) },
            { "bog_willow",       N("bog_willow",       "Bog Willow",            "grass_1",   HarvestSkill.Chopping, 28, 10000,  85,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "swamp_log", Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "bog_moss",  Chance = 0.3, Min = 1, Max = 2 },
                }) },
            { "deep_swamp_tree",  N("deep_swamp_tree",  "Ancient Swamp Tree",    "grass_1", HarvestSkill.Chopping, 40, 12000, 130,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "swamp_log",    Chance = 1.0, Min = 2, Max = 3 },
                    new DropEntry { ItemId = "ancient_wood", Chance = 0.3, Min = 1, Max = 1 },
                }) },

            // ── Ice Chopping ─────────────────────────────────────────────────────
            { "ice_pine",         N("ice_pine",         "Frozen Pine",           "grass_1",   HarvestSkill.Chopping, 22, 9000,   70, D("frozen_log",  1.0, 1, 2)) },
            { "ancient_tree",     N("ancient_tree",     "Ancient Ice Tree",      "grass_1",   HarvestSkill.Chopping, 55, 16000, 260,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "ancient_wood", Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "ancient_ice",  Chance = 0.2, Min = 1, Max = 1 },
                }) },

            // ── Stone Mining ─────────────────────────────────────────────────────
            { "copper_vein",      N("copper_vein",      "Copper Vein",           "grass_1",  HarvestSkill.Mining, 1,  4000,  10,  D("copper_ore",   1.0, 1, 2)) },
            // Temporary reusable placements used while the copied map scenes receive unique resources.
            // Keep the starter definitions above map-specific so the opening map remains authored.
            { "world_oak_tree",   N("world_oak_tree",   "Oak Tree",              "*", HarvestSkill.Chopping, 1, 4000, 10, D("oak_log", 1.0, 1, 2)) },
            { "world_copper_vein", N("world_copper_vein", "Copper Vein",          "*", HarvestSkill.Mining, 1, 4000, 10, D("copper_ore", 1.0, 1, 2)) },
            { "iron_vein",        N("iron_vein",        "Iron Vein",             "grass_1",   HarvestSkill.Mining, 18, 6500,  35,  D("iron_ore",     1.0, 1, 2)) },
            { "gold_vein",        N("gold_vein",        "Gold Vein",             "grass_1",  HarvestSkill.Mining, 30, 10000, 80,  D("gold_ore",     1.0, 1, 1)) },
            { "mithril_vein",     N("mithril_vein",     "Mithril Vein",          "grass_1",   HarvestSkill.Mining, 45, 14000, 190,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "mithril_ore",   Chance = 1.0, Min = 1, Max = 1 },
                    new DropEntry { ItemId = "glacier_shard", Chance = 0.3, Min = 1, Max = 2 },
                }) },

            // ── Forest Gathering ─────────────────────────────────────────────────
            { "wildflower_patch", N("wildflower_patch", "Wildflower Patch",      "grass_1", HarvestSkill.Gathering, 1,  3500, 8,   D("wildflower", 1.0, 1, 3)) },
            { "sunleaf_cluster",  N("sunleaf_cluster",  "Sunleaf Cluster",       "grass_1",  HarvestSkill.Gathering, 8,  5000, 20,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "sunleaf",    Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "wildflower", Chance = 0.4, Min = 1, Max = 2 },
                }) },
            { "moonberry_bush",   N("moonberry_bush",   "Moonberry Bush",        "grass_1",  HarvestSkill.Gathering, 18, 7500, 45,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "moonberry", Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "sunleaf",   Chance = 0.3, Min = 1, Max = 1 },
                }) },

            // ── Stone Gathering ──────────────────────────────────────────────────
            { "stone_herb_clump", N("stone_herb_clump", "Stone Herb Clump",      "grass_1",  HarvestSkill.Gathering, 5,  4500, 14,  D("stone_herb",  1.0, 1, 2)) },
            { "runic_moss_vein",  N("runic_moss_vein",  "Runic Moss Vein",       "grass_1",   HarvestSkill.Gathering, 22, 9000, 60,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "runic_moss", Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "stone_herb", Chance = 0.3, Min = 1, Max = 1 },
                }) },

            // ── Swamp Gathering ──────────────────────────────────────────────────
            { "bog_plant_grove",  N("bog_plant_grove",  "Bog Plant Grove",       "grass_1",  HarvestSkill.Gathering, 10, 6000,  25,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "bog_plant", Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "bog_moss",  Chance = 0.4, Min = 1, Max = 2 },
                }) },
            { "poison_herb_bed",  N("poison_herb_bed",  "Poison Herb Bed",       "grass_1",    HarvestSkill.Gathering, 20, 8500,  55,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "poison_herb", Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "bog_plant",   Chance = 0.3, Min = 1, Max = 1 },
                }) },
            { "gloomcap_grove",   N("gloomcap_grove",   "Gloomcap Grove",        "grass_1", HarvestSkill.Gathering, 32, 11000, 100,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "gloomcap",    Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "poison_herb", Chance = 0.3, Min = 1, Max = 1 },
                }) },

            // ── Ice Gathering ────────────────────────────────────────────────────
            { "frost_fern_patch",       N("frost_fern_patch",       "Frost Fern Patch",        "grass_1",   HarvestSkill.Gathering, 28, 9500,  75,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "frost_fern",    Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "frost_crystal", Chance = 0.3, Min = 1, Max = 1 },
                }) },
            { "crystal_herb_deposit",   N("crystal_herb_deposit",   "Crystal Herb Deposit",    "grass_1",  HarvestSkill.Gathering, 40, 13000, 160,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "ice_crystal_herb", Chance = 1.0, Min = 1, Max = 2 },
                    new DropEntry { ItemId = "frost_fern",       Chance = 0.3, Min = 1, Max = 1 },
                }) },
            { "moon_bloom_spring",      N("moon_bloom_spring",      "Moon Bloom Spring",       "grass_1",   HarvestSkill.Gathering, 55, 17000, 280,
                new List<DropEntry>
                {
                    new DropEntry { ItemId = "moon_bloom",       Chance = 1.0, Min = 1, Max = 1 },
                    new DropEntry { ItemId = "ice_crystal_herb", Chance = 0.4, Min = 1, Max = 1 },
                }) },
        };

        /// <summary>Retourneert de ResourceNodeDef voor het gegeven id, of null wanneer niet gevonden.</summary>
        public static ResourceNodeDef Get(string id) => All.TryGetValue(id, out ResourceNodeDef def) ? def : null;

        // ── Fabriekshulpers ───────────────────────────────────────────────────────

        private static ResourceNodeDef N(
            string id, string name, string mapId, HarvestSkill skill,
            int levelReq, int baseTimeMs, int xp, List<DropEntry> drops)
            => new ResourceNodeDef
            {
                Id         = id,
                Name       = name,
                MapId      = mapId,
                Skill      = skill,
                LevelReq   = levelReq,
                BaseTimeMs = baseTimeMs,
                Xp         = xp,
                Drops      = drops,
            };

        private static ResourceNodeDef N(
            string id, string name, string mapId, HarvestSkill skill,
            int levelReq, int baseTimeMs, int xp, DropEntry singleDrop)
            => N(id, name, mapId, skill, levelReq, baseTimeMs, xp, new List<DropEntry> { singleDrop });

        private static DropEntry D(string itemId, double chance, int min, int max)
            => new DropEntry { ItemId = itemId, Chance = chance, Min = min, Max = max };
    }
}
