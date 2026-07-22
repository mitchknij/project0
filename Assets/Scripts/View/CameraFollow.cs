using UnityEngine;

namespace IdleCloud.View
{
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("Transform the camera follows. Assigned by SceneBootstrap at runtime if left unassigned.")]
        public Transform target;
        [Tooltip("Approximate time (seconds) for the camera to catch up to the target. Smaller is snappier.")]
        [Range(0.01f, 2f)]
        public float smoothTime = 0.15f;
        [Tooltip("Constant offset from the target's XY, in Unity units. Z is pinned to this value regardless of the target's depth.")]
        public Vector3 offset = new Vector3(0f, 0f, -10f);

        private Vector3 _velocity;

        // TransparencySortMode and transparencySortAxis are configured by SceneBootstrap
        // using the grid's derived ZStep, so there is no hardcoded axis value here.

        void LateUpdate()
        {
            if (target == null) return;
            // Build desired XY from target, but pin Z to offset.z so the camera
            // never drifts when the player's transform.z changes for depth sorting.
            var desired = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                offset.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }
    }
}
