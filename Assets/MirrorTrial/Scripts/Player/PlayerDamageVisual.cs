using System.Collections;
using UnityEngine;

namespace MirrorTrial.Player
{
    public class PlayerDamageVisual : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] renderers;

        Color[] originalColors;
        Coroutine hitTintRoutine;
        bool visualOverrideActive;

        void Awake()
        {
            ResolveRenderers();
        }

        public void PlayHitTint(Color tintColor, float duration)
        {
            FinishHitTint();
            RestoreOriginalColors();
            ResolveRenderers();
            CaptureOriginalColors();

            if (renderers.Length == 0 || duration <= 0f)
                return;

            visualOverrideActive = true;
            hitTintRoutine = StartCoroutine(HitTintRoutine(tintColor, duration));
        }

        public IEnumerator Blink(float duration, float interval)
        {
            FinishHitTint();
            RestoreOriginalColors();
            ResolveRenderers();
            CaptureOriginalColors();

            if (duration <= 0f)
                yield break;

            if (renderers.Length == 0 || interval <= 0f)
            {
                yield return new WaitForSecondsRealtime(duration);
                yield break;
            }

            visualOverrideActive = true;
            var elapsed = 0f;
            var visible = true;
            while (elapsed < duration)
            {
                visible = !visible;
                SetAlphaVisible(visible);
                var step = Mathf.Min(interval, duration - elapsed);
                yield return new WaitForSecondsRealtime(step);
                elapsed += step;
            }

            RestoreOriginalColors();
            visualOverrideActive = false;
        }

        public void ResetVisual()
        {
            FinishHitTint();
            RestoreOriginalColors();
            visualOverrideActive = false;
        }

        IEnumerator HitTintRoutine(Color tintColor, float duration)
        {
            ApplyTint(tintColor, 0f);

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyTint(tintColor, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            RestoreOriginalColors();
            visualOverrideActive = false;
            hitTintRoutine = null;
        }

        void FinishHitTint()
        {
            if (hitTintRoutine == null)
                return;

            StopCoroutine(hitTintRoutine);
            hitTintRoutine = null;
            RestoreOriginalColors();
            visualOverrideActive = false;
        }

        void ResolveRenderers()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (originalColors == null || originalColors.Length != renderers.Length)
                originalColors = new Color[renderers.Length];
        }

        void CaptureOriginalColors()
        {
            for (var i = 0; i < renderers.Length; i++)
                originalColors[i] = renderers[i] ? renderers[i].color : Color.white;
        }

        void ApplyTint(Color tintColor, float recovery)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                var target = renderers[i];
                if (!target) continue;

                var original = originalColors[i];
                var tint = new Color(tintColor.r, tintColor.g, tintColor.b, original.a);
                target.color = Color.Lerp(tint, original, recovery);
            }
        }

        void SetAlphaVisible(bool visible)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                var target = renderers[i];
                if (!target) continue;

                var color = originalColors[i];
                color.a = visible ? originalColors[i].a : 0f;
                target.color = color;
            }
        }

        void RestoreOriginalColors()
        {
            if (!visualOverrideActive || renderers == null || originalColors == null)
                return;

            var count = Mathf.Min(renderers.Length, originalColors.Length);
            for (var i = 0; i < count; i++)
            {
                if (renderers[i])
                    renderers[i].color = originalColors[i];
            }
        }

        void OnDisable()
        {
            ResetVisual();
        }
    }
}
