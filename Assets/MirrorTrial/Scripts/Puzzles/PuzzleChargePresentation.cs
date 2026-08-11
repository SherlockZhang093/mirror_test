using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleChargePresentation : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] SpriteRenderer core;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] SpriteRenderer fullFlash;
        [SerializeField] SpriteRenderer[] segments;
        [SerializeField] Color emptyColor = new Color(0.22f, 0.1f, 0.035f, 0.48f);
        [SerializeField] Color fullColor = new Color(1f, 0.58f, 0.08f, 1f);
        [SerializeField] Color fullCoreColor = new Color(1f, 0.9f, 0.48f, 1f);
        [Header("Audio")]
        [SerializeField] AudioSource chargeLoopSource;
        [SerializeField] AudioSource feedbackSource;
        [SerializeField] AudioSource doorSource;
        [SerializeField] AudioClip chargeLoopClip;
        [SerializeField] AudioClip chargeTickClip;
        [SerializeField] AudioClip completedClip;
        [SerializeField] AudioClip doorOpeningClip;
        [SerializeField, Range(0f, 1f)] float chargeLoopVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] float tickVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float completedVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] float doorVolume = 1f;

        float progress;
        float nextTickTime;
        bool completed;

        public float Progress => progress;

        void Awake() => ResetPresentation();

        void Update()
        {
            if (progress <= 0f || completed) return;

            var pulse = 1f + Mathf.Sin(Time.time * Mathf.Lerp(5f, 13f, progress)) * 0.08f;
            if (core) core.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.08f, progress) * pulse;
            if (glow)
            {
                glow.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.22f, progress) * pulse;
                var color = glow.color;
                color.a = Mathf.Lerp(0.04f, 0.42f, progress);
                glow.color = color;
            }
        }

        public void SetProgress(float normalized)
        {
            if (completed) return;

            progress = Mathf.Clamp01(normalized);
            UpdateVisuals();
            if (progress <= 0f)
            {
                StopChargeAudio();
                return;
            }

            if (chargeLoopSource && chargeLoopClip)
            {
                if (!chargeLoopSource.isPlaying)
                {
                    chargeLoopSource.clip = chargeLoopClip;
                    chargeLoopSource.loop = true;
                    chargeLoopSource.Play();
                }
                chargeLoopSource.volume = chargeLoopVolume * Mathf.Lerp(0.5f, 1f, progress);
                chargeLoopSource.pitch = Mathf.Lerp(0.78f, 1.22f, progress);
            }

            if (feedbackSource && chargeTickClip && Time.time >= nextTickTime)
            {
                feedbackSource.pitch = Mathf.Lerp(0.9f, 1.32f, progress);
                feedbackSource.PlayOneShot(chargeTickClip, tickVolume);
                nextTickTime = Time.time + Mathf.Lerp(0.42f, 0.12f, progress);
            }
        }

        public void Complete()
        {
            if (completed) return;
            progress = 1f;
            completed = true;
            StopChargeAudio();
            UpdateVisuals();
            if (fullFlash) fullFlash.enabled = true;
            if (feedbackSource && completedClip)
            {
                feedbackSource.pitch = 1f;
                feedbackSource.PlayOneShot(completedClip, completedVolume);
            }
        }

        public void ResetPresentation()
        {
            completed = false;
            progress = 0f;
            nextTickTime = 0f;
            StopChargeAudio();
            if (fullFlash) fullFlash.enabled = false;
            UpdateVisuals();
        }

        public void PlayDoorOpening()
        {
            if (!doorSource || !doorOpeningClip) return;
            doorSource.clip = doorOpeningClip;
            doorSource.loop = false;
            doorSource.volume = doorVolume;
            doorSource.pitch = 1f;
            doorSource.Play();
        }

        public void StopDoorOpening()
        {
            if (doorSource) doorSource.Stop();
        }

        void UpdateVisuals()
        {
            if (core)
            {
                core.color = Color.Lerp(emptyColor, fullCoreColor, progress);
                core.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.08f, progress);
            }

            if (glow)
            {
                var glowColor = fullColor;
                glowColor.a = Mathf.Lerp(0.02f, completed ? 0.65f : 0.32f, progress);
                glow.color = glowColor;
            }

            if (segments == null) return;
            var filled = Mathf.FloorToInt(progress * segments.Length + 0.0001f);
            for (var i = 0; i < segments.Length; i++)
            {
                if (!segments[i]) continue;
                segments[i].color = i < filled ? fullColor : emptyColor;
            }
        }

        void StopChargeAudio()
        {
            if (chargeLoopSource) chargeLoopSource.Stop();
            if (feedbackSource) feedbackSource.pitch = 1f;
        }

        void OnDisable()
        {
            StopChargeAudio();
            StopDoorOpening();
        }
    }
}
