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

        [Header("Receiver Pulse")]
        [SerializeField] bool enableReceiverPulse;
        [SerializeField, Min(0.1f)] float idlePulsePeriod = 1.2f;
        [SerializeField, Min(0.1f)] float chargingPulsePeriod = 0.35f;
        [SerializeField, Min(0.01f)] float hitFlashDuration = 0.16f;
        [SerializeField, Min(0.01f)] float firstCompletionFlashDuration = 0.12f;
        [SerializeField, Min(0f)] float completionFlashGap = 0.08f;
        [SerializeField, Min(0.01f)] float secondCompletionFlashDuration = 0.16f;
        [SerializeField, Min(0.1f)] float idleFlashMinimumScale = 0.95f;
        [SerializeField, Min(0.1f)] float idleFlashMaximumScale = 2.6f;

        float progress;
        float nextTickTime;
        bool completed;
        bool receivingLight;
        float hitFlashStartedAt = float.NegativeInfinity;
        float completionFlashStartedAt = float.NegativeInfinity;
        Vector3 coreBaseScale = Vector3.one;
        Vector3 glowBaseScale = Vector3.one;
        Vector3 fullFlashBaseScale = Vector3.one;

        public float Progress => progress;

        void Awake()
        {
            if (core) coreBaseScale = core.transform.localScale;
            if (glow) glowBaseScale = glow.transform.localScale;
            if (fullFlash) fullFlashBaseScale = fullFlash.transform.localScale;
            ResetPresentation();
        }

        void Update()
        {
            if (enableReceiverPulse)
            {
                UpdateReceiverAnimation();
                return;
            }

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

        public void SetReceivingLight(bool receiving)
        {
            if (!enableReceiverPulse) return;
            if (receiving && !receivingLight)
                hitFlashStartedAt = Time.time;
            receivingLight = receiving;
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
            completionFlashStartedAt = Time.time;
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
            receivingLight = false;
            progress = 0f;
            nextTickTime = 0f;
            hitFlashStartedAt = float.NegativeInfinity;
            completionFlashStartedAt = float.NegativeInfinity;
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
                core.transform.localScale = coreBaseScale * Mathf.Lerp(0.72f, 1.08f, progress);
            }

            if (glow)
            {
                var glowColor = fullColor;
                glowColor.a = Mathf.Lerp(0.02f, completed ? 0.65f : 0.32f, progress);
                glow.color = glowColor;
            }

            if (segments == null) return;
            var filled = enableReceiverPulse && progress > 0f
                ? Mathf.CeilToInt(progress * segments.Length)
                : Mathf.FloorToInt(progress * segments.Length + 0.0001f);
            for (var i = 0; i < segments.Length; i++)
            {
                if (!segments[i]) continue;
                segments[i].color = i < filled ? fullColor : emptyColor;
            }
        }

        void UpdateReceiverAnimation()
        {
            var idle = !completed && !receivingLight && progress <= 0.001f;
            var period = receivingLight ? chargingPulsePeriod : idlePulsePeriod;
            var phase = Mathf.Repeat(Time.time, period) / period;
            var sine = Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f));
            var pulse = completed ? 0f : Mathf.Pow(sine, receivingLight ? 4f : 10f);

            if (core)
            {
                var pulseBrightness = receivingLight ? 0.78f : 1f;
                var brightness = completed ? 1f : Mathf.Max(progress, pulse * pulseBrightness);
                core.color = Color.Lerp(emptyColor, fullCoreColor, brightness);
                var progressScale = Mathf.Lerp(0.72f, 1.08f, progress);
                var pulseScale = receivingLight ? 0.16f : 0.24f;
                core.transform.localScale = coreBaseScale * progressScale * (1f + pulse * pulseScale);
            }

            if (glow)
            {
                var colour = fullColor;
                var baseAlpha = completed ? 0.52f : Mathf.Lerp(0.025f, 0.34f, progress);
                colour.a = Mathf.Clamp01(baseAlpha + pulse * (receivingLight ? 0.38f : 0.72f));
                glow.color = colour;
                var glowScale = Mathf.Lerp(0.86f, 1.24f, progress);
                glowScale *= idle
                    ? Mathf.Lerp(1.15f, 1.85f, pulse)
                    : 1f + pulse * 0.08f;
                glow.transform.localScale = glowBaseScale * glowScale;
            }

            var hitFlash = completed
                ? 0f
                : FlashEnvelope(Time.time - hitFlashStartedAt, hitFlashDuration);
            var completionFlash = completed
                ? CompletionFlashEnvelope(Time.time - completionFlashStartedAt)
                : 0f;
            var idleFlash = idle ? pulse * 0.72f : 0f;
            var flash = Mathf.Max(idleFlash, Mathf.Max(hitFlash * 0.72f, completionFlash));

            if (fullFlash)
            {
                fullFlash.enabled = flash > 0.001f;
                var colour = Color.Lerp(fullColor, Color.white, 0.38f);
                colour.a = flash;
                fullFlash.color = colour;
                var flashScale = idle
                    ? Mathf.Lerp(idleFlashMinimumScale, idleFlashMaximumScale, pulse)
                    : Mathf.Lerp(hitFlash > completionFlash ? 0.58f : 0.92f, 1.16f, flash);
                fullFlash.transform.localScale = fullFlashBaseScale * flashScale;
            }
        }

        float CompletionFlashEnvelope(float elapsed)
        {
            if (elapsed < 0f) return 0f;
            if (elapsed <= firstCompletionFlashDuration)
                return FlashEnvelope(elapsed, firstCompletionFlashDuration);

            elapsed -= firstCompletionFlashDuration + completionFlashGap;
            if (elapsed < 0f || elapsed > secondCompletionFlashDuration) return 0f;
            return FlashEnvelope(elapsed, secondCompletionFlashDuration) * 0.78f;
        }

        static float FlashEnvelope(float elapsed, float duration)
        {
            if (elapsed < 0f || elapsed > duration || duration <= 0f) return 0f;
            return Mathf.Sin(elapsed / duration * Mathf.PI);
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
