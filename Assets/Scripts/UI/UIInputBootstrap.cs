using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace IdleCloud.UI
{
    /// <summary>
    /// Provides the one scene-level EventSystem required by all uGUI canvases.
    /// The method is idempotent so authored scenes remain the source of truth.
    /// </summary>
    public static class UIInputBootstrap
    {
        public static EventSystem EnsureEventSystem()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                if (existing.GetComponent<InputSystemUIInputModule>() == null)
                    existing.gameObject.AddComponent<InputSystemUIInputModule>();
                return existing;
            }

            var eventSystemObject = new GameObject("EventSystem");
            var eventSystem = eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
            return eventSystem;
        }
    }
}
