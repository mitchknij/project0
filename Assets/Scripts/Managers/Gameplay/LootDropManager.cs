using System;
using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Managers
{
    public sealed class LootDropConfig
    {
        public long DespawnMs;
        public long AutoVacuumDelayMs;

        public LootDropConfig Clone() => new LootDropConfig
        {
            DespawnMs = DespawnMs,
            AutoVacuumDelayMs = AutoVacuumDelayMs,
        };
    }

    public sealed class GroundLootRecord
    {
        public string DropId;
        public string MapId;
        public CombatTileCoordinate Tile;
        public int Floor;
        public List<ItemStack> Stacks = new List<ItemStack>();
        public long SpawnedAtMs;

        public GroundLootRecord Clone() => new GroundLootRecord
        {
            DropId = DropId,
            MapId = MapId,
            Tile = Tile,
            Floor = Floor,
            Stacks = CloneStacks(Stacks),
            SpawnedAtMs = SpawnedAtMs,
        };

        private static List<ItemStack> CloneStacks(List<ItemStack> source)
        {
            var copy = new List<ItemStack>();
            if (source == null) return copy;
            foreach (ItemStack stack in source)
            {
                if (stack == null) continue;
                copy.Add(new ItemStack { ItemId = stack.ItemId, Qty = stack.Qty });
            }
            return copy;
        }
    }

    public sealed class LootPickupResult
    {
        public string DropId;
        public Account Account;
        public List<ItemStack> PickedStacks = new List<ItemStack>();
        public List<ItemStack> RemainingStacks = new List<ItemStack>();
    }

    public sealed class LootSpawnedEvent
    {
        public string DropId;
        public GroundLootRecord Record;
    }

    public sealed class LootPickedUpEvent
    {
        public string DropId;
        public string CharacterId;
        public bool Vacuum;
        public List<ItemStack> PickedStacks = new List<ItemStack>();
        public List<ItemStack> RemainingStacks = new List<ItemStack>();
    }

    public sealed class LootExpiredEvent
    {
        public string DropId;
        public List<ItemStack> Stacks = new List<ItemStack>();
    }

    /// <summary>
    /// Runtime-only registry for physical loot bags. Account mutations are returned
    /// as new snapshots so GameManager remains the authoritative commit owner.
    /// </summary>
    public sealed class LootDropManager
    {
        private readonly Dictionary<string, ItemDef> _items;
        private readonly Dictionary<string, GroundLootRecord> _records =
            new Dictionary<string, GroundLootRecord>();
        private readonly Dictionary<string, long> _vacuumAttemptAtMs = new Dictionary<string, long>();
        private readonly LootDropConfig _config;

        public event Action<LootSpawnedEvent> LootSpawned;
        public event Action<LootPickedUpEvent> LootPickedUp;
        public event Action<LootExpiredEvent> LootExpired;

        public LootDropManager(IReadOnlyDictionary<string, ItemDef> items, LootDropConfig config)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.DespawnMs < 0) throw new ArgumentOutOfRangeException(nameof(config.DespawnMs));
            if (config.AutoVacuumDelayMs < 0) throw new ArgumentOutOfRangeException(nameof(config.AutoVacuumDelayMs));

            _items = new Dictionary<string, ItemDef>();
            foreach (KeyValuePair<string, ItemDef> pair in items)
                _items[pair.Key] = pair.Value;
            _config = config.Clone();
        }

        public string Spawn(
            string mapId,
            CombatTileCoordinate tile,
            int floor,
            List<ItemStack> stacks,
            long nowMs)
        {
            if (string.IsNullOrWhiteSpace(mapId)) throw new ArgumentException("Map ID is required.", nameof(mapId));
            if (stacks == null) throw new ArgumentNullException(nameof(stacks));

            List<ItemStack> copiedStacks = ClonePositiveStacks(stacks);
            if (copiedStacks.Count == 0) return null;

            string dropId = Guid.NewGuid().ToString("N");
            var record = new GroundLootRecord
            {
                DropId = dropId,
                MapId = mapId,
                Tile = tile,
                Floor = floor,
                Stacks = copiedStacks,
                SpawnedAtMs = nowMs,
            };
            _records.Add(dropId, record);
            LootSpawned?.Invoke(new LootSpawnedEvent
            {
                DropId = dropId,
                Record = record.Clone(),
            });
            return dropId;
        }

        public LootPickupResult TryPickup(Account account, string characterId, string dropId)
            => TryPickup(account, characterId, dropId, vacuum: false);

        public LootPickupResult TryPickup(Account account, string characterId, string dropId, bool vacuum)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (string.IsNullOrWhiteSpace(characterId)) throw new ArgumentException("Character ID is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(dropId)) throw new ArgumentException("Drop ID is required.", nameof(dropId));

            var result = new LootPickupResult
            {
                DropId = dropId,
                Account = account.Clone(),
            };
            if (!_records.TryGetValue(dropId, out GroundLootRecord record)) return result;

            Character character = FindCharacter(account, characterId);
            if (character == null)
            {
                result.RemainingStacks = ClonePositiveStacks(record.Stacks);
                return result;
            }

            Character updatedCharacter = character;
            foreach (ItemStack stack in record.Stacks)
            {
                if (stack == null || stack.Qty <= 0) continue;
                if (!_items.TryGetValue(stack.ItemId, out ItemDef item) || item == null)
                {
                    result.RemainingStacks.Add(CloneStack(stack));
                    continue;
                }

                var added = Inventory.AddToInventory(updatedCharacter, stack, item);
                updatedCharacter = added.Character;
                int picked = stack.Qty - added.Overflow;
                if (picked > 0)
                    result.PickedStacks.Add(new ItemStack { ItemId = stack.ItemId, Qty = picked });
                if (added.Overflow > 0)
                    result.RemainingStacks.Add(new ItemStack { ItemId = stack.ItemId, Qty = added.Overflow });
            }

            result.Account = AccountHelper.UpdateCharacter(account, characterId, _ => updatedCharacter);
            if (result.RemainingStacks.Count == 0)
            {
                _records.Remove(dropId);
                _vacuumAttemptAtMs.Remove(dropId);
            }
            else
                record.Stacks = ClonePositiveStacks(result.RemainingStacks);

            // Event only on actual transfer: a failed attempt (full inventory) would otherwise
            // spam the feed on every vacuum retry; callers see failures via the returned result.
            if (result.PickedStacks.Count > 0)
                LootPickedUp?.Invoke(new LootPickedUpEvent
                {
                    DropId = dropId,
                    CharacterId = characterId,
                    Vacuum = vacuum,
                    PickedStacks = ClonePositiveStacks(result.PickedStacks),
                    RemainingStacks = ClonePositiveStacks(result.RemainingStacks),
                });
            return result;
        }

        public List<string> CollectDue(long nowMs, bool autoLootEnabled)
        {
            var due = new List<string>();
            var expired = new List<GroundLootRecord>();
            foreach (GroundLootRecord record in _records.Values)
            {
                long elapsed = nowMs >= record.SpawnedAtMs ? nowMs - record.SpawnedAtMs : 0;
                if (elapsed >= _config.DespawnMs)
                {
                    expired.Add(record);
                    continue;
                }
                if (autoLootEnabled && elapsed >= _config.AutoVacuumDelayMs)
                {
                    // Failed pickups (full inventory) leave the record behind; re-attempt at most
                    // once per vacuum delay instead of every call.
                    if (!_vacuumAttemptAtMs.TryGetValue(record.DropId, out long lastAttempt) ||
                        nowMs - lastAttempt >= _config.AutoVacuumDelayMs)
                    {
                        _vacuumAttemptAtMs[record.DropId] = nowMs;
                        due.Add(record.DropId);
                    }
                }
            }

            foreach (GroundLootRecord record in expired)
            {
                _records.Remove(record.DropId);
                _vacuumAttemptAtMs.Remove(record.DropId);
                LootExpired?.Invoke(new LootExpiredEvent
                {
                    DropId = record.DropId,
                    Stacks = ClonePositiveStacks(record.Stacks),
                });
            }
            return due;
        }

        public int ClearMap(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId)) return 0;
            var removed = new List<string>();
            foreach (GroundLootRecord record in _records.Values)
                if (record.MapId == mapId) removed.Add(record.DropId);
            foreach (string dropId in removed)
            {
                _records.Remove(dropId);
                _vacuumAttemptAtMs.Remove(dropId);
            }
            return removed.Count;
        }

        private static Character FindCharacter(Account account, string characterId)
        {
            foreach (Character character in account.Characters ?? new List<Character>())
                if (character != null && character.Id == characterId) return character;
            return null;
        }

        private static ItemStack CloneStack(ItemStack stack) => new ItemStack
        {
            ItemId = stack.ItemId,
            Qty = stack.Qty,
        };

        private static List<ItemStack> ClonePositiveStacks(List<ItemStack> source)
        {
            var copy = new List<ItemStack>();
            if (source == null) return copy;
            foreach (ItemStack stack in source)
            {
                if (stack == null || stack.Qty <= 0) continue;
                copy.Add(CloneStack(stack));
            }
            return copy;
        }
    }
}
