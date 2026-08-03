using MirrorTrial.Player;
using MirrorTrial.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            if (!other.GetComponentInParent<PlayerInputReader>())
                return;

            loading = true;
            if (GlobalAudioFeedback.IsAvailable)
                GlobalAudioFeedback.PlayComplete();
            else
                PlayerAudioFeedback.PlayComplete();
            SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
    }
}
