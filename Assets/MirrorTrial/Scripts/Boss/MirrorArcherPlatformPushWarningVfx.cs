using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Sprite-authored platform ward. The artwork is fixed on prefab children; code only
    /// controls visibility, brightness and the small warning expansion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MirrorArcherPlatformPushWarningVfx : MonoBehaviour
    {
        [Header("Prefab children")]
        [SerializeField] Transform animatedRoot;
        [SerializeField] SpriteRenderer backRenderer;
        [SerializeField] SpriteRenderer frontRenderer;

        [Header("Adjustable size")]
        [SerializeField] Vector3 compactScale = Vector3.one;
        [SerializeField] Vector3 expandedScale = new Vector3(1.22f, 1.12f, 1f);

        [Header("Appearance")]
        [SerializeField, Range(0f, 1f)] float backAlpha = 0.58f;
        [SerializeField, Range(0f, 1f)] float frontAlpha = 0.96f;
        [SerializeField, Range(0.1f, 1f)] float dimBrightness = 0.48f;
        [SerializeField, Range(0.1f, 1f)] float dimAlphaMultiplier = 0.72f;

        [Header("Blocked hit feedback")]
        [SerializeField, Min(0.02f)] float blockedHitDuration = 0.1f;
        [SerializeField, Min(1f)] float blockedHitBrightness = 1.35f;

        float warningDuration;
        float warningAge;
        float blockedHitRemaining;
        float currentBrightness = 1f;
        float currentAlphaMultiplier = 1f;
        bool initialized;
        bool visible;
        bool warningPlaying;

        void Awake()
        {
            EnsureInitialized();
            StopImmediate();
        }

        void Update()
        {
            if (warningPlaying)
            {
                warningAge += Time.deltaTime;
                var normalized = Mathf.Clamp01(warningAge / warningDuration);
                ApplyWarning(normalized);
                if (normalized >= 1f)
                {
                    warningPlaying = false;
                    ShowExpandedImmediate();
                }
            }

            if (blockedHitRemaining > 0f)
            {
                blockedHitRemaining = Mathf.Max(0f, blockedHitRemaining - Time.deltaTime);
                var pulse = Mathf.Sin((blockedHitRemaining / blockedHitDuration) * Mathf.PI);
                ApplyRenderedColors(Mathf.Max(currentBrightness,
                    Mathf.Lerp(1f, blockedHitBrightness, pulse)), currentAlphaMultiplier);
            }

            if (!warningPlaying && blockedHitRemaining <= 0f)
                enabled = false;
        }

        public void ShowCompactImmediate()
        {
            EnsureInitialized();
            visible = true;
            warningPlaying = false;
            warningAge = 0f;
            SetScale(compactScale);
            ApplyColors(1f, 1f);
            SetRenderersEnabled(true);
            enabled = blockedHitRemaining > 0f;
        }

        public void ShowExpandedImmediate()
        {
            EnsureInitialized();
            visible = true;
            warningPlaying = false;
            warningAge = warningDuration;
            SetScale(expandedScale);
            ApplyColors(1f, 1f);
            SetRenderersEnabled(true);
            enabled = blockedHitRemaining > 0f;
        }

        /// <summary>
        /// Bright, dim, bright, then modestly expand. The ward never disappears during it.
        /// </summary>
        public void PlayWarning(float duration)
        {
            EnsureInitialized();
            warningDuration = Mathf.Max(0.3f, duration);
            warningAge = 0f;
            blockedHitRemaining = 0f;
            visible = true;
            warningPlaying = true;
            SetScale(compactScale);
            SetRenderersEnabled(true);
            ApplyWarning(0f);
            enabled = true;
        }

        public void PlayBlockedHit()
        {
            EnsureInitialized();
            if (!visible) return;
            blockedHitRemaining = blockedHitDuration;
            enabled = true;
        }

        public void StopImmediate()
        {
            EnsureInitialized();
            visible = false;
            warningPlaying = false;
            warningAge = 0f;
            blockedHitRemaining = 0f;
            SetScale(compactScale);
            ApplyColors(1f, 1f);
            SetRenderersEnabled(false);
            enabled = false;
        }

        public void ConfigureSorting(int sortingLayerId, int bossSortingOrder)
        {
            EnsureInitialized();
            ConfigureRenderer(backRenderer, sortingLayerId, bossSortingOrder - 1);
            ConfigureRenderer(frontRenderer, sortingLayerId, bossSortingOrder + 1);
        }

        void ApplyWarning(float normalized)
        {
            float brightness;
            float alphaMultiplier;

            if (normalized < 0.22f)
            {
                var flash = Mathf.Sin(Mathf.InverseLerp(0f, 0.22f, normalized) * Mathf.PI);
                brightness = Mathf.Lerp(1f, 1.3f, flash);
                alphaMultiplier = 1f;
            }
            else if (normalized < 0.46f)
            {
                brightness = dimBrightness;
                alphaMultiplier = dimAlphaMultiplier;
            }
            else if (normalized < 0.7f)
            {
                var flash = Mathf.Sin(Mathf.InverseLerp(0.46f, 0.7f, normalized) * Mathf.PI);
                brightness = Mathf.Lerp(1f, 1.35f, flash);
                alphaMultiplier = 1f;
            }
            else
            {
                var expansion = Smooth01(Mathf.InverseLerp(0.7f, 1f, normalized));
                SetScale(Vector3.Lerp(compactScale, expandedScale, expansion));
                brightness = Mathf.Lerp(1.22f, 1f, expansion);
                alphaMultiplier = 1f;
            }

            ApplyColors(brightness, alphaMultiplier);
        }

        void ApplyColors(float brightness, float alphaMultiplier)
        {
            currentBrightness = brightness;
            currentAlphaMultiplier = alphaMultiplier;
            ApplyRenderedColors(brightness, alphaMultiplier);
        }

        void ApplyRenderedColors(float brightness, float alphaMultiplier)
        {
            ApplyColor(backRenderer, brightness, backAlpha * alphaMultiplier);
            ApplyColor(frontRenderer, brightness, frontAlpha * alphaMultiplier);
        }

        void EnsureInitialized()
        {
            if (initialized) return;
            if (!animatedRoot) animatedRoot = transform;
            initialized = true;
        }

        void SetScale(Vector3 scale)
        {
            if (animatedRoot) animatedRoot.localScale = scale;
        }

        void SetRenderersEnabled(bool value)
        {
            if (backRenderer) backRenderer.enabled = value;
            if (frontRenderer) frontRenderer.enabled = value;
        }

        static void ApplyColor(SpriteRenderer target, float brightness, float alpha)
        {
            if (!target) return;
            target.color = new Color(
                Mathf.Min(1f, brightness),
                Mathf.Min(1f, brightness),
                Mathf.Min(1f, brightness),
                Mathf.Clamp01(alpha));
        }

        static void ConfigureRenderer(SpriteRenderer target, int layerId, int order)
        {
            if (!target) return;
            target.sortingLayerID = layerId;
            target.sortingOrder = order;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
