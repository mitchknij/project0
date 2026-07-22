#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using IdleCloud.Core;
using IdleCloud.Managers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace IdleCloud.Editor
{
    /// <summary>
    /// Exports the Manager-bound pure content definitions as browser-ready JSON.
    /// It deliberately never serializes ScriptableObject references or Unity GUIDs.
    /// </summary>
    public static class WebContentSnapshotExporter
    {
        private const string RegistryResourceName = "ContentRegistry";
        private const string OutputRelativePath = "webapp/src/content/generated/idlecloud-content.json";

        [MenuItem("IdleCloud/Web/Export Content Snapshot")]
        public static void Export()
        {
            ContentRegistryAsset registry = Resources.Load<ContentRegistryAsset>(RegistryResourceName);
            if (registry == null)
                throw new InvalidOperationException("web_export_registry_missing:" + RegistryResourceName);

            var provider = new ContentRegistryProvider(registry);

            var snapshot = new WebContentSnapshot
            {
                SchemaVersion = 1,
                ContentVersion = provider.ConfigurationVersion,
                GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
                Bundle = new WebContentBundle
                {
                    Monsters = ExportMonsters(provider.Monsters),
                    Nodes = ExportNodes(provider.Nodes),
                },
            };

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputRelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(snapshot, Formatting.Indented, JsonSettings));
            AssetDatabase.Refresh();
            Debug.Log("[WebContentSnapshotExporter] Exported " + path);
        }

        private static Dictionary<string, WebMonster> ExportMonsters(IReadOnlyDictionary<string, MonsterDef> source)
        {
            var result = new Dictionary<string, WebMonster>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                MonsterDef monster = pair.Value;
                result[pair.Key] = new WebMonster
                {
                    Id = monster.Id,
                    Name = monster.Name,
                    Xp = monster.Xp,
                    CoinsMin = monster.Coins?.Min ?? 0,
                    CoinsMax = monster.Coins?.Max ?? 0,
                    Drops = ExportDrops(monster.Drops),
                    DropTable = ExportDropTable(monster.Drops),
                };
            }
            return result;
        }

        private static Dictionary<string, WebNode> ExportNodes(IReadOnlyDictionary<string, ResourceNodeDef> source)
        {
            var result = new Dictionary<string, WebNode>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                ResourceNodeDef node = pair.Value;
                result[pair.Key] = new WebNode
                {
                    Id = node.Id,
                    Name = node.Name,
                    Skill = node.Skill.ToString(),
                    Xp = node.Xp,
                    Drops = ExportDrops(node.Drops),
                };
            }
            return result;
        }

        private static List<WebDropEntry> ExportDrops(DropTable table) => ExportDrops(table?.Tertiary);

        private static List<WebDropEntry> ExportDrops(List<DropEntry> source)
        {
            var result = new List<WebDropEntry>();
            foreach (DropEntry entry in source ?? new List<DropEntry>())
                if (entry != null) result.Add(new WebDropEntry
                {
                    ItemId = entry.ItemId,
                    Chance = entry.Chance,
                    Min = entry.Min,
                    Max = entry.Max,
                });
            return result;
        }

        private static WebDropTable ExportDropTable(DropTable source)
        {
            if (source == null) return null;
            var result = new WebDropTable
            {
                Always = new List<WebDropItem>(),
                Tertiary = ExportDrops(source.Tertiary),
            };
            foreach (DropItem item in source.Always ?? new List<DropItem>())
                if (item != null) result.Always.Add(new WebDropItem { ItemId = item.ItemId, Min = item.Min, Max = item.Max });
            if (source.Main != null)
            {
                result.Main = new WebWeightedTable { Rolls = source.Main.Rolls, Slots = new List<WebWeightedSlot>() };
                foreach (WeightedSlot slot in source.Main.Slots ?? new List<WeightedSlot>())
                    if (slot != null) result.Main.Slots.Add(new WebWeightedSlot
                    {
                        Weight = slot.Weight,
                        Nothing = slot.Nothing,
                        ItemId = slot.ItemId,
                        Min = slot.Min,
                        Max = slot.Max,
                    });
            }
            return result;
        }

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        private sealed class WebContentSnapshot
        {
            public int SchemaVersion;
            public string ContentVersion;
            public string GeneratedAtUtc;
            public WebContentBundle Bundle;
        }

        private sealed class WebContentBundle
        {
            public Dictionary<string, WebMonster> Monsters;
            public Dictionary<string, WebNode> Nodes;
        }

        private sealed class WebMonster
        {
            public string Id;
            public string Name;
            public int Xp;
            public int CoinsMin;
            public int CoinsMax;
            public List<WebDropEntry> Drops;
            public WebDropTable DropTable;
        }

        private sealed class WebNode
        {
            public string Id;
            public string Name;
            public string Skill;
            public int Xp;
            public List<WebDropEntry> Drops;
        }

        private sealed class WebDropTable
        {
            public List<WebDropItem> Always;
            public WebWeightedTable Main;
            public List<WebDropEntry> Tertiary;
        }

        private sealed class WebDropItem { public string ItemId; public int Min; public int Max; }
        private sealed class WebDropEntry { public string ItemId; public double Chance; public int Min; public int Max; }
        private sealed class WebWeightedTable { public int Rolls; public List<WebWeightedSlot> Slots; }
        private sealed class WebWeightedSlot { public int Weight; public bool Nothing; public string ItemId; public int Min; public int Max; }
    }
}
#endif
