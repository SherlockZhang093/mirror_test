using MirrorTrial.Player;
using MirrorTrial.Audio;
using MirrorTrial.Growth;
using UnityEngine;

namespace MirrorTrial.Level
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelExit : MonoBehaviour
    {
        [SerializeField] string nextSceneName;

        bool loading;

        void Reset()
        {
            var trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (loading || string.IsNullOrEmpty(nextSceneName))
                return;

            var playerInput = other.GetComponentInParent<PlayerInputReader>();
            if (!playerInput)
                return;

            loading = true;
            if (GlobalAudioFeedback.IsAvailable)
                GlobalAudioFeedback.PlayComplete();
            else
                PlayerAudioFeedback.PlayComplete();

            if (!LevelEndGrowthController.TryShow(playerInput.gameObject, LoadNextScene))
                loading = false;
        }

        void LoadNextScene()
        {
            if (!GameFlow.LoadSceneWithTransition(nextSceneName))
                loading = false;
        }
    }
}
