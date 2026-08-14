using UnityEngine;

namespace MirrorTrial.Boss
{
    [DisallowMultipleComponent]
    public sealed class BossInvincibilityBlinkVisual : MonoBehaviour
    {
        SpriteRenderer[] renderers;
        float[] visibleAlphas;
        float interval;
        float nextToggleAt;
        bool blinking;
        bool visible = true;

        public void BeginBlink(float blinkInterval)
        {
            interval = Mathf.Max(0.02f, blinkInterval);
            if (!blinking)
            {
                ResolveRenderers();
                CaptureVisibleAlphas();
                blinking = true;
                visible = true;
            }

            nextToggleAt = Time.unscaledTime + interval;
            ApplyVisibility(true);
        }

        public void EndBlink()
        {
            if (!blinking) return;
            visible = true;
            ApplyVisibility(true);
            blinking = false;
        }

        void Update()
        {
            if (!blinking) return;

            // Re-apply hidden alpha every frame so phase tint changes cannot reveal the boss
            // in the middle of an off-frame. RGB values are always left untouched.
            if (!visible)
                ApplyVisibility(false);

            if (Time.unscaledTime < nextToggleAt) return;
            visible = !visible;
            ApplyVisibility(visible);
            nextToggleAt = Time.unscaledTime + interval;
        }

        void ResolveRenderers()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            visibleAlphas = new float[renderers.Length];
        }

        void CaptureVisibleAlphas()
        {
            for (var i = 0; i < renderers.Length; i++)
                visibleAlphas[i] = renderers[i] ? renderers[i].color.a : 1f;
        }

        void ApplyVisibility(bool show)
        {
            if (renderers == null || visibleAlphas == null) return;
            var count = Mathf.Min(renderers.Length, visibleAlphas.Length);
            for (var i = 0; i < count; i++)
            {
                var target = renderers[i];
                if (!target) continue;
                var color = target.color;
                color.a = show ? visibleAlphas[i] : 0f;
                target.color = color;
            }
        }

        void OnDisable()
        {
            EndBlink();
        }
    }
}
