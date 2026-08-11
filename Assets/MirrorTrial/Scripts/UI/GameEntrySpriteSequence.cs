using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.UI
{
    [DisallowMultipleComponent]
    public sealed class GameEntrySpriteSequence : MonoBehaviour
    {
        [SerializeField] Image target;
        [SerializeField] Sprite[] idleFrames;
        [SerializeField] Sprite[] walkFrames;
        [SerializeField] float idleFrameRate = 10f;
        [SerializeField] float walkFrameRate = 9f;
        [SerializeField] bool walking;

        float elapsed;
        int displayedFrame = -1;

        public void Configure(Image image, Sprite[] idle, Sprite[] walk, float idleRate, float walkRate)
        {
            target = image;
            idleFrames = idle;
            walkFrames = walk;
            idleFrameRate = Mathf.Max(1f, idleRate);
            walkFrameRate = Mathf.Max(1f, walkRate);
            SetWalking(false);
        }

        public void SetWalking(bool value)
        {
            walking = value;
            elapsed = 0f;
            displayedFrame = -1;
            ApplyFrame(0);
        }

        void OnEnable()
        {
            elapsed = 0f;
            displayedFrame = -1;
            ApplyFrame(0);
        }

        void Update()
        {
            var frames = GetActiveFrames();
            if (!target || frames == null || frames.Length == 0)
                return;

            elapsed += Time.unscaledDeltaTime;
            var frameRate = walking ? walkFrameRate : idleFrameRate;
            ApplyFrame(Mathf.FloorToInt(elapsed * frameRate) % frames.Length);
        }

        Sprite[] GetActiveFrames()
        {
            if (walking && walkFrames != null && walkFrames.Length > 0)
                return walkFrames;
            return idleFrames != null && idleFrames.Length > 0 ? idleFrames : walkFrames;
        }

        void ApplyFrame(int index)
        {
            var frames = GetActiveFrames();
            if (!target || frames == null || frames.Length == 0 || index == displayedFrame)
                return;
            displayedFrame = index;
            target.sprite = frames[index];
        }
    }
}
