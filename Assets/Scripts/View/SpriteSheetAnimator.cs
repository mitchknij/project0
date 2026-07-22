using UnityEngine;

namespace IdleCloud.View
{
    public class SpriteSheetAnimator : MonoBehaviour
    {
        [Tooltip("Frames played in sequence, looping.")]
        public Sprite[] frames;
        [Tooltip("Playback speed, in frames per second.")]
        [Min(1f)]
        public float fps = 20f;

        private SpriteRenderer _renderer;
        private float _timer;
        private int _frame;

        void Awake() => _renderer = GetComponent<SpriteRenderer>();

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            _timer += Time.deltaTime;
            if (_timer >= 1f / fps)
            {
                _timer -= 1f / fps;
                _frame = (_frame + 1) % frames.Length;
                _renderer.sprite = frames[_frame];
            }
        }

        public void SetFrames(Sprite[] newFrames, float? newFps = null)
        {
            if (newFps.HasValue) fps = newFps.Value;
            if (newFrames == frames) return;
            frames = newFrames;
            _frame = 0;
            _timer = 0f;
            if (_renderer != null && frames != null && frames.Length > 0)
                _renderer.sprite = frames[0];
        }
    }
}
