using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class AbilityTransferEffect : MonoBehaviour
    {
        [SerializeField] SpriteRenderer mainRenderer;
        [SerializeField] SpriteRenderer[] echoes;
        [SerializeField] SpriteRenderer[] sparks;
        [SerializeField] SpriteRenderer arrivalFlash;
        [SerializeField] LineRenderer beamCore;
        [SerializeField] LineRenderer beamGlow;
        [SerializeField] ParticleSystem gatherBurst;
        [SerializeField] ParticleSystem arrivalBurst;
        [SerializeField] Light2D orbLight;
        [Header("Beam Shape")]
        [SerializeField, Range(8, 32)] int beamSegments = 20;
        [SerializeField, Min(0f)] float beamArcHeight = 0.9f;
        [SerializeField, Min(0f)] float beamGlowWidth = 0.14f;
        [SerializeField, Min(0f)] float beamCoreWidth = 0.045f;
        [SerializeField] Color beamGlowColor = new Color(0.18f, 0.88f, 1f, 0.38f);
        [SerializeField] Color beamCoreColor = new Color(1f, 0.96f, 0.72f, 1f);
        [Header("Timing")]
        [SerializeField, Min(0f)] float launchPause = 0.2f;
        [SerializeField, Min(0.1f)] float travelDuration = 0.65f;
        [SerializeField, Min(0.05f)] float arrivalDuration = 0.18f;
        [SerializeField] Vector3 targetOffset = new Vector3(0f, 0.65f, 0f);
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip launchSound;
        [SerializeField] AudioClip arrivalSound;
        [SerializeField, Range(0f, 1f)] float volume = 0.8f;

        Transform target;
        Action arrived;
        Vector3 start;
        Vector3 control;
        float elapsed;
        bool travelling;
        bool finishing;

        public void Begin(Transform targetTransform, Action onArrived)
        {
            target = targetTransform;
            arrived = onArrived;
            start = transform.position;
            var destination = TargetPosition();
            var side = Mathf.Sign(destination.x - start.x);
            if (Mathf.Approximately(side, 0f)) side = 1f;
            control = Vector3.Lerp(start, destination, 0.5f) +
                      Vector3.up * beamArcHeight - Vector3.right * side * 0.28f;
            elapsed = 0f;
            travelling = true;
            finishing = false;
            if (arrivalFlash) arrivalFlash.enabled = false;
            ConfigureBeamAppearance();
            SetBeamVisible(true);
            UpdateBeam(0f, 0f);
            if (gatherBurst) gatherBurst.Play(true);
            if (audioSource && launchSound) audioSource.PlayOneShot(launchSound, volume);
        }

        void Update()
        {
            if (!travelling) return;
            var delta = Time.unscaledDeltaTime;
            elapsed += delta;

            if (!finishing)
            {
                if (elapsed < launchPause)
                {
                    var gather = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, launchPause));
                    transform.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.3f, gather);
                    UpdateBeam(0f, gather * 0.35f);
                    AnimateSparks(gather);
                    return;
                }

                var t = Mathf.Clamp01((elapsed - launchPause) / travelDuration);
                var oneMinusT = 1f - t;
                var destination = TargetPosition();
                transform.position = oneMinusT * oneMinusT * start +
                                     2f * oneMinusT * t * control + t * t * destination;
                transform.Rotate(0f, 0f, 360f * delta);
                transform.localScale = Vector3.one * (0.2f + Mathf.Sin(t * Mathf.PI) * 0.13f);
                UpdateBeam(t, Mathf.Sin(t * Mathf.PI));
                UpdateEchoes(delta, false);
                AnimateSparks(t);

                if (t < 1f) return;
                finishing = true;
                elapsed = 0f;
                if (arrivalFlash) arrivalFlash.enabled = true;
                if (beamCore) beamCore.enabled = false;
                if (arrivalBurst) arrivalBurst.Play(true);
                if (audioSource && arrivalSound) audioSource.PlayOneShot(arrivalSound, volume);
                arrived?.Invoke();
                arrived = null;
                return;
            }

            transform.position = TargetPosition();
            var arrivalT = Mathf.Clamp01(elapsed / arrivalDuration);
            if (mainRenderer)
            {
                var color = mainRenderer.color;
                color.a = 1f - arrivalT;
                mainRenderer.color = color;
            }
            if (orbLight) orbLight.intensity = Mathf.Lerp(1.5f, 0f, arrivalT);
            if (arrivalFlash)
            {
                var color = arrivalFlash.color;
                color.a = 1f - arrivalT;
                arrivalFlash.color = color;
                arrivalFlash.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.65f, arrivalT);
            }
            UpdateArrivalRing(arrivalT);
            UpdateEchoes(delta, true);
            AnimateSparks(1f + arrivalT);
            if (arrivalT >= 1f) Destroy(gameObject);
        }

        void SetBeamVisible(bool visible)
        {
            if (beamCore) beamCore.enabled = visible;
            if (beamGlow) beamGlow.enabled = visible;
        }

        void UpdateBeam(float pathProgress, float energy)
        {
            UpdateBeamLine(beamGlow, pathProgress, Mathf.Lerp(0f, beamGlowWidth, energy));
            UpdateBeamLine(beamCore, pathProgress, Mathf.Lerp(0f, beamCoreWidth, energy));
        }

        void UpdateBeamLine(LineRenderer line, float pathProgress, float width)
        {
            if (!line) return;
            line.loop = false;
            line.positionCount = beamSegments;
            var destination = TargetPosition();
            for (var i = 0; i < beamSegments; i++)
            {
                var t = pathProgress * i / (beamSegments - 1f);
                var oneMinusT = 1f - t;
                line.SetPosition(i, oneMinusT * oneMinusT * start +
                                    2f * oneMinusT * t * control + t * t * destination);
            }
            line.startWidth = width;
            line.endWidth = width * 0.25f;
        }

        void ConfigureBeamAppearance()
        {
            var effectMaterial = mainRenderer ? mainRenderer.sharedMaterial : null;
            if (effectMaterial)
            {
                if (beamGlow) beamGlow.sharedMaterial = effectMaterial;
                if (beamCore) beamCore.sharedMaterial = effectMaterial;
                SetParticleMaterial(gatherBurst, effectMaterial);
                SetParticleMaterial(arrivalBurst, effectMaterial);
            }
            SetLineColor(beamGlow, beamGlowColor);
            SetLineColor(beamCore, beamCoreColor);
            if (mainRenderer)
            {
                mainRenderer.color = beamCoreColor;
                mainRenderer.transform.localScale = Vector3.one * 0.62f;
            }
            if (orbLight) orbLight.color = beamGlowColor;
        }

        static void SetParticleMaterial(ParticleSystem system, Material material)
        {
            if (!system) return;
            var particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer) particleRenderer.sharedMaterial = material;
        }

        void UpdateArrivalRing(float progress)
        {
            if (!beamGlow) return;
            const int segments = 40;
            var center = TargetPosition();
            var radius = Mathf.Lerp(0.28f, 0.9f, progress);
            beamGlow.enabled = true;
            beamGlow.loop = true;
            beamGlow.positionCount = segments;
            beamGlow.startWidth = beamGlow.endWidth = Mathf.Lerp(0.07f, 0.015f, progress);
            for (var i = 0; i < segments; i++)
            {
                var angle = Mathf.PI * 2f * i / segments;
                beamGlow.SetPosition(i, center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.28f,
                    0f));
            }
            var color = beamGlowColor;
            color.a *= 1f - progress;
            SetLineColor(beamGlow, color);
        }

        static void SetLineColor(LineRenderer line, Color color)
        {
            if (!line) return;
            line.startColor = color;
            line.endColor = color;
        }

        void UpdateEchoes(float delta, bool fade)
        {
            if (echoes == null) return;
            for (var i = 0; i < echoes.Length; i++)
            {
                var echo = echoes[i];
                if (!echo) continue;
                var followSpeed = i == 0 ? 13f : 7f;
                echo.transform.position = Vector3.Lerp(echo.transform.position, transform.position,
                    1f - Mathf.Exp(-followSpeed * delta));
                if (!fade) continue;
                var color = echo.color;
                color.a = Mathf.MoveTowards(color.a, 0f, delta * 3f);
                echo.color = color;
            }
        }

        void AnimateSparks(float progress)
        {
            if (sparks == null) return;
            for (var i = 0; i < sparks.Length; i++)
            {
                var spark = sparks[i];
                if (!spark) continue;
                var angle = Time.unscaledTime * (5f + i) + i * Mathf.PI * 0.66f;
                var radius = Mathf.Lerp(0.24f, 0.1f, Mathf.Clamp01(progress));
                spark.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
            }
        }

        Vector3 TargetPosition()
        {
            return target ? target.position + targetOffset : transform.position;
        }
    }
}
