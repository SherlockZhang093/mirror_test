using UnityEngine;

namespace MirrorTrial.Audio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-990)]
    public sealed class GlobalAudioFeedback : MonoBehaviour
    {
        [SerializeField] GameAudioPalette palette;
        [SerializeField] AudioSource uiSource;
        [SerializeField, Range(0f, 1f)] float uiVolume = 0.75f;

        static GlobalAudioFeedback instance;
        public static bool IsAvailable => instance && instance.enabled;

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            if (!palette) palette = GameAudioPalette.LoadDefault();
            if (!uiSource)
            {
                Debug.LogError("Global Audio Feedback needs its preconfigured UI Audio Source.", this);
                enabled = false;
                return;
            }

            uiSource.playOnAwake = false;
            uiSource.loop = false;
            uiSource.spatialBlend = 0f;
            uiSource.dopplerLevel = 0f;
            uiSource.ignoreListenerPause = true;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void PlaySelect() => Play(instance && instance.palette ? instance.palette.uiSelect : null, instance && instance.palette ? instance.palette.uiSelectVolume : 1f);
        public static void PlayCancel() => Play(instance && instance.palette ? instance.palette.uiCancel : null, instance && instance.palette ? instance.palette.uiCancelVolume : 1f);
        public static void PlayComplete() => Play(instance && instance.palette ? instance.palette.uiComplete : null, instance && instance.palette ? instance.palette.uiCompleteVolume : 1f);

        public static bool TryPlayMirrorImpact(bool shatter)
        {
            if (!instance || !instance.palette || !instance.uiSource)
                return false;

            var clip = shatter ? instance.palette.mirrorShatter : instance.palette.mirrorHit;
            var volume = shatter ? instance.palette.mirrorShatterVolume : instance.palette.mirrorHitVolume;
            if (!clip)
                return false;

            instance.uiSource.PlayOneShot(clip, instance.palette.ScaleVolume(1f, volume));
            return true;
        }

        static void Play(AudioClip clip, float audioVolume)
        {
            if (instance && instance.uiSource && clip)
                instance.uiSource.PlayOneShot(clip, instance.palette.ScaleVolume(instance.uiVolume, audioVolume));
        }
    }
}
