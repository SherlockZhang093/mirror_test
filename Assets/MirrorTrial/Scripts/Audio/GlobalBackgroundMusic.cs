using System.Collections;
using UnityEngine;

namespace MirrorTrial.Audio
{
    /// <summary>
    /// Keeps the configured scene music alive across scene changes. Two serialized
    /// audio sources overlap the clip's quiet ending and beginning.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class GlobalBackgroundMusic : MonoBehaviour
    {
        [Header("Music")]
        [SerializeField] AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.32f;

        [Header("Loop Transition")]
        [SerializeField, Min(0f)] float startupFadeSeconds = 2.5f;
        [SerializeField, Min(0.1f)] float crossfadeSeconds = 8f;

        [Header("Preconfigured Sources")]
        [SerializeField] AudioSource primarySource;
        [SerializeField] AudioSource secondarySource;

        static GlobalBackgroundMusic instance;

        readonly AudioSource[] sources = new AudioSource[2];
        int activeSourceIndex;
        bool crossfading;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicClip == null)
            {
                Debug.LogError("Global Background Music has no Music Clip assigned.", this);
                enabled = false;
                return;
            }

            if (primarySource == null || secondarySource == null || primarySource == secondarySource)
            {
                Debug.LogError("Global Background Music needs two different preconfigured Audio Sources.", this);
                enabled = false;
                return;
            }

            sources[0] = primarySource;
            sources[1] = secondarySource;
            ConfigureSource(sources[0]);
            ConfigureSource(sources[1]);

            activeSourceIndex = 0;
            sources[activeSourceIndex].Play();
            StartCoroutine(FadeIn(sources[activeSourceIndex], startupFadeSeconds));
        }

        void Update()
        {
            if (musicClip == null || crossfading)
                return;

            var activeSource = sources[activeSourceIndex];
            var crossfadeDuration = Mathf.Min(Mathf.Max(0.1f, crossfadeSeconds), musicClip.length * 0.25f);
            if (!activeSource.isPlaying || activeSource.time < musicClip.length - crossfadeDuration)
                return;

            StartCoroutine(Crossfade(crossfadeDuration));
        }

        void ConfigureSource(AudioSource source)
        {
            source.clip = musicClip;
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0f;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 0;
            source.ignoreListenerPause = true;
        }

        IEnumerator FadeIn(AudioSource source, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = musicVolume * Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            source.volume = musicVolume;
        }

        IEnumerator Crossfade(float duration)
        {
            crossfading = true;

            var previousIndex = activeSourceIndex;
            var nextIndex = 1 - previousIndex;
            var previousSource = sources[previousIndex];
            var nextSource = sources[nextIndex];

            nextSource.time = 0f;
            nextSource.volume = 0f;
            nextSource.Play();

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var angle = progress * Mathf.PI * 0.5f;

                previousSource.volume = musicVolume * Mathf.Cos(angle);
                nextSource.volume = musicVolume * Mathf.Sin(angle);
                yield return null;
            }

            previousSource.Stop();
            previousSource.volume = 0f;
            nextSource.volume = musicVolume;
            activeSourceIndex = nextIndex;
            crossfading = false;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
