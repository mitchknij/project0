// CraftingPanel.cs — Toont alle recepten met craft-knop; ongeldige recepten tonen reden en zijn disabled.

using System.Linq;
using UnityEngine;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class CraftingPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public RectTransform listContainer;

        public void Refresh()
        {
            if (listContainer == null) return;
            foreach (Transform t in listContainer) Destroy(t.gameObject);

            var gm = GameManager.Instance;
            var ch = gm?.GetSelectedCharacter();
            if (ch == null) return;
            var bank = gm.Account?.Bank;
            if (bank == null) return;

            var itemDefs = new System.Collections.Generic.Dictionary<string, ItemDef>(RuntimeContent.Items);
            foreach (var recipe in RuntimeContent.Recipes.Values.OrderBy(r => r.LevelReq))
            {
                var (ok, reason) = Crafting.CanCraft(bank, ch, recipe, ch.Level, itemDefs);

                RuntimeContent.Items.TryGetValue(recipe.OutputItemId, out ItemDef outDef);
                string outputName = outDef?.Name ?? recipe.OutputItemId;

                string inputs = string.Join(", ", recipe.Inputs.Select(stack =>
                {
                    RuntimeContent.Items.TryGetValue(stack.ItemId, out ItemDef inDef);
                    string inName = inDef?.Name ?? stack.ItemId;
                    return $"{inName} x{stack.Qty}";
                }));
                if (recipe.CoinCost > 0)
                    inputs += $"  +  {recipe.CoinCost} gold";

                string label = $"{outputName} x{recipe.OutputQty}  (Lv.{recipe.LevelReq})  -  {inputs}";
                if (!ok) label += $"  [{reason}]";

                string recipeId = recipe.Id;
                var btn = ItemSlotWidget.Create(listContainer, label, 1, "Craft", UIHelpers.AccentGreen,
                    () => { GameManager.Instance.CraftRecipe(recipeId); Refresh(); });
                btn.interactable = ok;
            }
        }

        public void Show()  { gameObject.SetActive(true); Refresh(); }
        public void Hide()  => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;
    }
}
