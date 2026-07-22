// UIBootstrapper.cs — Runtime-bedrading die in de hiërarchie zelf leeft, zodat zowel
// de code-gebouwde UI als de gebakken prefab dezelfde registratie doorloopt.
// Awake draait vóór GameFlowController.Start (FlowLoop) — volgorde is gegarandeerd.

using UnityEngine;

namespace IdleCloud.UI
{
    public class UIBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            // A baked UI prefab intentionally does not own the scene EventSystem.
            // Ensure one exists for scenes that have not authored it yet.
            UIInputBootstrap.EnsureEventSystem();

            var flow = GetComponent<GameFlowController>();
            var pm   = GetComponent<PanelManager>();
            var rd   = GetComponent<UIRefreshDriver>();
            if (flow == null || pm == null || rd == null)
            {
                Debug.LogError("[UIBootstrapper] Ontbrekende component op UIController: " +
                    $"flow={(flow != null)}, panelManager={(pm != null)}, refreshDriver={(rd != null)}");
                return;
            }

            var hud = flow.mainHudPanel;
            if (flow.titlePanel == null || flow.charSelectPanel == null ||
                flow.charCreatePanel == null || hud == null)
            {
                Debug.LogError("[UIBootstrapper] Ontbrekende panel-referentie op GameFlowController: " +
                    $"title={(flow.titlePanel != null)}, charSelect={(flow.charSelectPanel != null)}, " +
                    $"charCreate={(flow.charCreatePanel != null)}, hud={(hud != null)}");
                return;
            }
            if (hud.activityPanel == null || hud.travelPanel == null || hud.inventoryPanel == null ||
                hud.equipmentPanel == null || hud.bankPanel == null || hud.craftingPanel == null ||
                hud.talentsPanel == null || hud.skillsPanel == null || hud.skillBarPanel == null)
            {
                Debug.LogError("[UIBootstrapper] Ontbrekende sub-panel-referentie op MainHudPanel — " +
                    "controleer de prefab-bedrading (Activity/Travel/Inventory/Equipment/Bank/Crafting/Talents/Skills/SkillBar).");
                return;
            }

            pm.Register(new IPanelView[] { flow.titlePanel, flow.charSelectPanel, flow.charCreatePanel, hud });
            rd.Register(new IPanelView[] { flow.titlePanel, flow.charSelectPanel, flow.charCreatePanel, hud,
                                           hud.activityPanel, hud.travelPanel, hud.inventoryPanel,
                                           hud.equipmentPanel, hud.bankPanel, hud.craftingPanel,
                                           hud.talentsPanel, hud.skillsPanel, hud.skillBarPanel });

            // Beginstatus: alles verborgen; FlowLoop toont daarna het juiste panel.
            pm.HideAll();

            // Sub-panels expliciet verbergen — HideAll dekt alleen de flow-panels,
            // en een per ongeluk actief gelaten sub-panel in de prefab moet ook dicht.
            hud.activityPanel.Hide();
            hud.travelPanel.Hide();
            hud.inventoryPanel.Hide();
            hud.equipmentPanel.Hide();
            hud.bankPanel.Hide();
            hud.craftingPanel.Hide();
            hud.talentsPanel.Hide();
            hud.skillsPanel.Hide();
            hud.skillBarPanel.Hide();

            if (flow.offlineReportPanel != null) flow.offlineReportPanel.Hide();

            BindCloseButton(hud.activityPanel);
            BindCloseButton(hud.travelPanel);
            BindCloseButton(hud.inventoryPanel);
            BindCloseButton(hud.equipmentPanel);
            BindCloseButton(hud.bankPanel);
            BindCloseButton(hud.craftingPanel);
            BindCloseButton(hud.talentsPanel);
            BindCloseButton(hud.skillsPanel);
        }

        private static void BindCloseButton(IPanelView panel)
        {
            if (panel is not MonoBehaviour behaviour) return;

            foreach (var button in behaviour.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button.gameObject.name != "X") continue;
                button.onClick.AddListener(panel.Hide);
                return;
            }
        }
    }
}
