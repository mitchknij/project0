using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using TMPro;
using UnityEngine;

namespace IdleCloud.UI
{
    /// <summary>Small runtime feed for pickup, inventory-full, and expiry notices.</summary>
    public sealed class LootFeedPanel : MonoBehaviour
    {
        [HideInInspector] public RectTransform listContainer;
        [SerializeField, Min(1)] private int maxEntries = UITheme.Layout.LootFeedMaxEntries;
        [SerializeField, Min(0.1f)] private float entryDurationSeconds = 4f;

        private sealed class FeedEntry
        {
            public GameObject GameObject;
            public float ExpiresAt;
        }

        private readonly List<FeedEntry> _entries = new List<FeedEntry>();
        private GameManager _manager;

        private void Update()
        {
            GameManager current = GameManager.Instance;
            if (_manager != current)
            {
                Unsubscribe();
                _manager = current;
                if (_manager != null)
                {
                    _manager.LootPickedUp += HandleLootPickedUp;
                    _manager.LootPickupAttempted += HandleLootPickupAttempted;
                    _manager.LootExpired += HandleLootExpired;
                    _manager.ActiveGatheringResolved += HandleGatheringResolved;
                    _manager.CraftCompleted += HandleCraftCompleted;
                }
            }

            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                if (Time.unscaledTime < _entries[index].ExpiresAt) continue;
                if (_entries[index].GameObject != null) Destroy(_entries[index].GameObject);
                _entries.RemoveAt(index);
            }
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_manager == null) return;
            _manager.LootPickedUp -= HandleLootPickedUp;
            _manager.LootPickupAttempted -= HandleLootPickupAttempted;
            _manager.LootExpired -= HandleLootExpired;
            _manager.ActiveGatheringResolved -= HandleGatheringResolved;
            _manager.CraftCompleted -= HandleCraftCompleted;
            _manager = null;
        }

        private void HandleLootPickedUp(LootPickedUpEvent payload)
        {
            if (payload?.PickedStacks == null) return;
            foreach (ItemStack stack in payload.PickedStacks)
            {
                if (stack == null || stack.Qty <= 0) continue;
                AddEntry($"+{stack.Qty} {ResolveItemName(stack.ItemId)}", UITheme.Green);
            }
        }

        private void HandleLootPickupAttempted(LootPickupResult result)
        {
            if (result == null || result.PickedStacks == null || result.RemainingStacks == null) return;
            bool hasKnownRemainder = false;
            foreach (ItemStack stack in result.RemainingStacks)
            {
                if (stack == null || stack.Qty <= 0) continue;
                if (RuntimeContent.Items.ContainsKey(stack.ItemId))
                {
                    hasKnownRemainder = true;
                    continue;
                }
                AddEntry($"Unknown loot: {stack.ItemId}", UITheme.Red);
            }
            if (hasKnownRemainder)
                AddEntry("Inventory full - loot left on ground", UITheme.Red);
        }

        private void HandleGatheringResolved(ActiveGatheringTickResult result)
        {
            if (result?.Simulation?.Loot != null)
                foreach (ItemStack stack in result.Simulation.Loot)
                {
                    if (stack == null || stack.Qty <= 0) continue;
                    AddEntry($"+{stack.Qty} {ResolveItemName(stack.ItemId)}", UITheme.Green);
                }
            // Gathering has no ground bags: overflow is genuinely lost, so say so.
            foreach (ItemStack stack in result?.Overflow ?? new List<ItemStack>())
            {
                if (stack == null || stack.Qty <= 0) continue;
                AddEntry($"Inventory full - {ResolveItemName(stack.ItemId)} x{stack.Qty} lost", UITheme.Red);
            }
        }

        private void HandleCraftCompleted(ItemStack output)
        {
            if (output == null || output.Qty <= 0) return;
            AddEntry($"Crafted +{output.Qty} {ResolveItemName(output.ItemId)}", UITheme.TextGold);
        }

        private void HandleLootExpired(LootExpiredEvent payload)
        {
            if (payload == null) return;
            int stackCount = payload.Stacks?.Count ?? 0;
            AddEntry(stackCount > 0 ? $"Loot expired ({stackCount} stack{(stackCount == 1 ? "" : "s")})" : "Loot expired", UITheme.TextDim);
        }

        private void AddEntry(string message, Color color)
        {
            if (listContainer == null || string.IsNullOrWhiteSpace(message)) return;
            int limit = Mathf.Max(1, maxEntries);
            while (_entries.Count >= limit)
            {
                FeedEntry oldest = _entries[0];
                if (oldest.GameObject != null) Destroy(oldest.GameObject);
                _entries.RemoveAt(0);
            }

            TextMeshProUGUI label = UIHelpers.CreateLabel(
                listContainer,
                message,
                15,
                align: TextAlignmentOptions.Left);
            label.color = color;
            UIHelpers.AddLayout(label.gameObject, preferredH: UITheme.Layout.LootFeedEntryHeight);
            _entries.Add(new FeedEntry
            {
                GameObject = label.gameObject,
                ExpiresAt = Time.unscaledTime + Mathf.Max(0.1f, entryDurationSeconds),
            });
        }

        private static string ResolveItemName(string itemId)
        {
            if (RuntimeContent.Items.TryGetValue(itemId, out ItemDef item) && item != null &&
                !string.IsNullOrWhiteSpace(item.Name))
                return item.Name;
            return itemId;
        }
    }
}
