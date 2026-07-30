using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MirrorTrial.HealthResources
{
    public sealed class HealthResourceTransferEffect : MonoBehaviour
    {
        PlayerHealthReserve reserve;
        SpriteRenderer mainRenderer;
        SpriteRenderer[] echoes;
        Light2D orbLight;
        Vector3 start;
        Vector3 control;
        float duration;
        float elapsed;
        Color color;
        bool arrived;

        public static bool TrySpawn(Vector3 origin, PlayerHealthReserve targetReserve,
            Sprite sprite, Color orbColor, float travelDuration)
        {
            if (!targetReserve || !sprite) return false;

            var root = new GameObject("生命光传递");
            root.transform.position = origin;
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = orbColor;
            renderer.sortingOrder = 60;

            var light = root.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = orbColor;
            light.intensity = 1.1f;
            light.pointLightInnerRadius = 0.08f;
            light.pointLightOuterRadius = 0.48f;
            light.falloffIntensity = 0.7f;

            var effect = root.AddComponent<HealthResourceTransferEffect>();
            effect.Configure(targetReserve, renderer, light, sprite, orbColor, travelDuration);
            return true;
        }

        void Configure(PlayerHealthReserve targetReserve, SpriteRenderer renderer,
            Light2D light, Sprite sprite, Color orbColor, float travelDuration)
        {
            reserve = targetReserve;
            mainRenderer = renderer;
            orbLight = light;
            color = orbColor;
            duration = Mathf.Max(0.1f, travelDuration);
            start = transform.position;
            var target = TargetPosition();
            var side = Mathf.Sign(target.x - start.x);
            if (Mathf.Approximately(side, 0f)) side = 1f;
            control = Vector3.Lerp(start, target, 0.5f) + Vector3.up * 0.8f - Vector3.right * side * 0.25f;
            transform.localScale = Vector3.one * 0.22f;

            echoes = new SpriteRenderer[2];
            for (var i = 0; i < echoes.Length; i++)
            {
                var echo = new GameObject($"生命光残影 {i + 1}");
                echo.transform.position = start;
                echo.transform.localScale = Vector3.one * (0.16f - i * 0.04f);
                echoes[i] = echo.AddComponent<SpriteRenderer>();
                echoes[i].sprite = sprite;
                echoes[i].sortingOrder = 59 - i;
                echoes[i].color = new Color(color.r, color.g, color.b, i == 0 ? 0.34f : 0.16f);
            }
        }

        void Update()
        {
            var delta = Time.unscaledDeltaTime;
            if (!reserve)
            {
                Destroy(gameObject);
                return;
            }

            if (!arrived)
            {
                elapsed += delta;
                var t = Mathf.Clamp01(elapsed / duration);
                var oneMinusT = 1f - t;
                var target = TargetPosition();
                transform.position = oneMinusT * oneMinusT * start + 2f * oneMinusT * t * control + t * t * target;
                transform.Rotate(0f, 0f, 300f * delta);
                var scale = Mathf.Sin(t * Mathf.PI) * 0.22f + 0.18f;
                transform.localScale = Vector3.one * scale;
                UpdateEchoes(delta);

                if (t >= 1f)
                {
                    arrived = true;
                    elapsed = 0f;
                }
                return;
            }

            elapsed += delta;
            transform.position = TargetPosition();
            var arrivalT = Mathf.Clamp01(elapsed / 0.18f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 0.85f, arrivalT);
            mainRenderer.color = new Color(color.r, color.g, color.b, 1f - arrivalT);
            orbLight.intensity = Mathf.Lerp(1.4f, 0f, arrivalT);
            UpdateEchoes(delta);
            if (arrivalT >= 1f) Destroy(gameObject);
        }

        void UpdateEchoes(float delta)
        {
            if (echoes == null) return;
            for (var i = 0; i < echoes.Length; i++)
            {
                if (!echoes[i]) continue;
                var followSpeed = i == 0 ? 12f : 7f;
                echoes[i].transform.position = Vector3.Lerp(
                    echoes[i].transform.position,
                    transform.position,
                    1f - Mathf.Exp(-followSpeed * delta));
                if (!arrived) continue;
                var c = echoes[i].color;
                c.a = Mathf.MoveTowards(c.a, 0f, delta * 3f);
                echoes[i].color = c;
            }
        }

        Vector3 TargetPosition()
        {
            return reserve ? reserve.transform.position + Vector3.up * 0.65f : transform.position;
        }

        void OnDestroy()
        {
            if (echoes == null) return;
            for (var i = 0; i < echoes.Length; i++)
                if (echoes[i]) Destroy(echoes[i].gameObject);
        }
    }
}
