using IdleCloud.Core;
using IdleCloud.Managers;
using IdleCloud.View;
using UnityEngine;

namespace IdleCloud.UI
{
    /// <summary>
    /// World interaction point for a city service. Add a sprite/model freely; the trigger only
    /// owns interaction and opens the matching existing HUD panel when available.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CityServiceStation : MonoBehaviour
    {
        [SerializeField] private HubServiceKind serviceKind = HubServiceKind.Bank;
        [SerializeField] private bool openOnPlayerEnter = true;

        private bool _activatedForCurrentEntry;

        public HubServiceKind ServiceKind => serviceKind;

        private void Reset()
        {
            CircleCollider2D trigger = GetComponent<CircleCollider2D>();
            if (trigger == null) trigger = gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!openOnPlayerEnter || _activatedForCurrentEntry ||
                other.GetComponentInParent<PlayerController>() == null) return;
            Activate();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                _activatedForCurrentEntry = false;
        }

        public void Activate()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null) return;

            manager.OpenHubService(serviceKind);
            _activatedForCurrentEntry = true;

            MainHudPanel hud = FindFirstObjectByType<MainHudPanel>();
            if (hud == null) return;

            IPanelView panel = ResolvePanel(hud, serviceKind);
            if (panel != null)
            {
                hud.OpenSubPanel(panel);
                return;
            }

            Debug.Log($"[CityServiceStation] {serviceKind} station is ready for its future service UI.", this);
        }

        /// <summary>Maps services with existing UI to their HUD panel; other services deliberately await their own UI.</summary>
        private static IPanelView ResolvePanel(MainHudPanel hud, HubServiceKind kind)
        {
            if (hud == null) return null;
            return kind switch
            {
                HubServiceKind.Bank => hud.bankPanel,
                HubServiceKind.Crafting => hud.craftingPanel,
                HubServiceKind.Teleport => hud.travelPanel,
                _ => null,
            };
        }

        private void OnValidate()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, trigger is CircleCollider2D circle ? circle.radius : 0.6f);
        }
    }
}
