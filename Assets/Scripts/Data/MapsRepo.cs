// MapsRepo.cs — Statische kaart-definities (vertaling van src/data/maps.ts).
// 1:1 met de TypeScript-bron; geen extra logica.

using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Data
{
    public static class MapsRepo
    {
        // The first authored Unity scene, starter monster, and starter resource nodes
        // all live on grass_1. Starting there keeps activity validation aligned with
        // the playable world while Thornhaven remains reachable through its map link.
        public const string StartingMapId = "grass_1";

        public static readonly Dictionary<string, MapDef> All = new Dictionary<string, MapDef>
        {
            { "thornhaven", new MapDef { Id = "thornhaven", Name = "Thornhaven", RecommendedLevel = 1,
                Connections = new List<string>() } },
            { "grass_1",    new MapDef { Id = "grass_1",    Name = "Grasslands I",   RecommendedLevel = 1,
                Connections = new List<string> { "grass_2", "rock_1" } } },
            { "grass_2",    new MapDef { Id = "grass_2",    Name = "Grasslands II",  RecommendedLevel = 5,
                Connections = new List<string> { "grass_1", "grass_3", "rock_2" } } },
            { "grass_3",    new MapDef { Id = "grass_3",    Name = "Grasslands III", RecommendedLevel = 10,
                Connections = new List<string> { "grass_2", "rock_3" } } },
            { "grass_4",    new MapDef { Id = "grass_4",    Name = "Grasslands IV",  RecommendedLevel = 12,
                Connections = new List<string>() } },
            { "rock_1",     new MapDef { Id = "rock_1",     Name = "Cave I",         RecommendedLevel = 15,
                Connections = new List<string> { "grass_1", "rock_2", "dessert_1" } } },
            { "rock_2",     new MapDef { Id = "rock_2",     Name = "Cave II",        RecommendedLevel = 20,
                Connections = new List<string> { "grass_2", "rock_1", "rock_3", "dessert_2" } } },
            { "rock_3",     new MapDef { Id = "rock_3",     Name = "Cave III",       RecommendedLevel = 25,
                Connections = new List<string> { "grass_3", "rock_2", "dessert_3" } } },
            { "dessert_1",  new MapDef { Id = "dessert_1",  Name = "Dessert I",      RecommendedLevel = 30,
                Connections = new List<string> { "rock_1", "dessert_2", "factory_1" } } },
            { "dessert_2",  new MapDef { Id = "dessert_2",  Name = "Dessert II",     RecommendedLevel = 35,
                Connections = new List<string> { "rock_2", "dessert_1", "dessert_3", "factory_2" } } },
            { "dessert_3",  new MapDef { Id = "dessert_3",  Name = "Dessert III",    RecommendedLevel = 40,
                Connections = new List<string> { "rock_3", "dessert_2", "factory_3" } } },
            { "factory_1",  new MapDef { Id = "factory_1",  Name = "Factory I",      RecommendedLevel = 45,
                Connections = new List<string> { "dessert_1", "factory_2", "lava_1" } } },
            { "factory_2",  new MapDef { Id = "factory_2",  Name = "Factory II",     RecommendedLevel = 50,
                Connections = new List<string> { "dessert_2", "factory_1", "factory_3", "lava_2" } } },
            { "factory_3",  new MapDef { Id = "factory_3",  Name = "Factory III",    RecommendedLevel = 55,
                Connections = new List<string> { "dessert_3", "factory_2", "lava_3" } } },
            { "lava_1",     new MapDef { Id = "lava_1",     Name = "Lava I",         RecommendedLevel = 60,
                Connections = new List<string> { "factory_1", "lava_2" } } },
            { "lava_2",     new MapDef { Id = "lava_2",     Name = "Lava II",        RecommendedLevel = 65,
                Connections = new List<string> { "factory_2", "lava_1", "lava_3" } } },
            { "lava_3",     new MapDef { Id = "lava_3",     Name = "Lava III",       RecommendedLevel = 70,
                Connections = new List<string> { "factory_3", "lava_2" } } },
        };

        /// <summary>Retourneert de MapDef voor het gegeven id, of null wanneer het niet bestaat.</summary>
        public static MapDef Get(string id) => All.TryGetValue(id, out MapDef def) ? def : null;
    }
}
