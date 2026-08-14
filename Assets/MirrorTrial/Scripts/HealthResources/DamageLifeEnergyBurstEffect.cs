using System.Collections.Generic;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.HealthResources
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem), typeof(ParticleSystemRenderer))]
    public sealed class DamageLifeEnergyBurstEffect : MonoBehaviour
    {
        const string ResourcePath = "Effects/DamageLifeEnergyBurst";
        const int MinimumParticleCount = 6;
        const int MaximumParticleCount = 24;
        const int PaletteReadbackSize = 16;
        const int MaximumCachedPalettes = 128;
        static DamageLifeEnergyBurstEffect cachedPrefab;
        static readonly Dictionary<int, Color32[]> spritePaletteCache = new Dictionary<int, Color32[]>();
        static Texture2D paletteReadbackTexture;

        [SerializeField] ParticleSystem particles;
        [SerializeField] ParticleSystemRenderer particleRenderer;
        [SerializeField, Min(0.01f)] float burstDuration = 0.09f;
        [SerializeField, Min(0f)] float attractionStagger = 0.08f;
        [SerializeField, Min(0.05f)] float attractionDuration = 0.34f;
        [SerializeField, Min(0f)] float targetHeight = 0.65f;
        [SerializeField] Color lowEnergyColor = new Color(0.18f, 0.92f, 0.8f, 1f);
        [SerializeField] Color highEnergyColor = new Color(0.72f, 1f, 0.9f, 1f);
        [SerializeField] int sortingOrder = 123;

        readonly List<SpriteRenderer> sourceRenderers = new List<SpriteRenderer>();
        readonly List<float> rendererWeights = new List<float>();
        ParticleSystem.Particle[] liveParticles;
        ParticleMotion[] motions;
        Transform attractionTarget;
        int activeParticleCount;
        float age;
        float maximumLifetime;
        bool playing;

        struct ParticleMotion
        {
            public Vector3 start;
            public Vector3 burstEnd;
            public Vector3 control;
            public float attractionDelay;
            public float lifetime;
            public float size;
            public Color color;
        }

        public static bool TrySpawn(GameObject sourceTarget, Transform playerTarget, float energyAmount)
        {
            if (!sourceTarget || !playerTarget || energyAmount <= 0f || !IsFinite(energyAmount)) return false;
            if (!cachedPrefab)
                cachedPrefab = Resources.Load<DamageLifeEnergyBurstEffect>(ResourcePath);
            if (!cachedPrefab) return false;

            var instance = Instantiate(cachedPrefab);
            instance.ConfigureParticleSystem();
            instance.gameObject.SetActive(true);
            if (instance.Play(sourceTarget, playerTarget, energyAmount)) return true;
            Destroy(instance.gameObject);
            return false;
        }

        public void Configure(ParticleSystem system, ParticleSystemRenderer systemRenderer,
            float outwardDuration, float stagger, float pullDuration, float playerHeight,
            Color startColor, Color endColor, int order)
        {
            particles = system;
            particleRenderer = systemRenderer;
            burstDuration = Mathf.Max(0.01f, outwardDuration);
            attractionStagger = Mathf.Max(0f, stagger);
            attractionDuration = Mathf.Max(0.05f, pullDuration);
            targetHeight = Mathf.Max(0f, playerHeight);
            lowEnergyColor = startColor;
            highEnergyColor = endColor;
            sortingOrder = order;
            ConfigureParticleSystem();
        }

        bool Play(GameObject sourceTarget, Transform playerTarget, float energyAmount)
        {
            ConfigureParticleSystem();
            CollectSourceRenderers(sourceTarget);
            if (!particles || sourceRenderers.Count == 0) return false;

            attractionTarget = playerTarget;
            age = 0f;
            playing = true;
            var combinedBounds = sourceRenderers[0].bounds;
            for (var i = 1; i < sourceRenderers.Count; i++)
                combinedBounds.Encapsulate(sourceRenderers[i].bounds);

            if (!IsFinite(combinedBounds.center) || !IsFinite(combinedBounds.size)) return false;

            var area = Mathf.Max(0.25f, combinedBounds.size.x * combinedBounds.size.y);
            activeParticleCount = Mathf.Clamp(
                MinimumParticleCount + Mathf.RoundToInt(Mathf.Sqrt(area) * 2.2f + energyAmount * 2f),
                MinimumParticleCount, MaximumParticleCount);
            liveParticles = new ParticleSystem.Particle[activeParticleCount];
            motions = new ParticleMotion[activeParticleCount];
            particles.Clear(true);
            particles.Play(true);

            var random = new System.Random(sourceTarget.GetInstanceID() ^ Time.frameCount);
            var center = combinedBounds.center;
            var burstScale = Mathf.Clamp(combinedBounds.extents.magnitude * 0.16f, 0.18f, 0.75f);
            var energyScale = Mathf.Lerp(0.9f, 1.25f, Mathf.Clamp01(energyAmount / 2f));
            maximumLifetime = 0f;
            for (var i = 0; i < activeParticleCount; i++)
            {
                var start = SampleSourcePosition(random, out var sourceRenderer);
                if (!IsFinite(start)) start = center;
                var outward = (start - center).normalized;
                if (outward.sqrMagnitude < 0.01f) outward = RandomDirection(random);
                outward = (outward + RandomDirection(random) * 0.45f).normalized;
                var burstEnd = start + outward * Mathf.Lerp(0.55f, 1.15f, NextFloat(random)) * burstScale;
                var perpendicular = new Vector3(-outward.y, outward.x, 0f);
                var control = burstEnd + outward * burstScale * 0.45f +
                              perpendicular * burstScale * Mathf.Lerp(-0.55f, 0.55f, NextFloat(random));
                var delay = burstDuration + NextFloat(random) * attractionStagger;
                var lifetime = delay + attractionDuration * Mathf.Lerp(0.82f, 1.18f, NextFloat(random));
                var color = SampleSourceColor(sourceRenderer, random, energyAmount);
                var size = Mathf.Lerp(0.055f, 0.105f, NextFloat(random)) * energyScale;
                motions[i] = new ParticleMotion
                {
                    start = start, burstEnd = burstEnd, control = control,
                    attractionDelay = delay, lifetime = lifetime, size = size, color = color
                };
                maximumLifetime = Mathf.Max(maximumLifetime, lifetime);
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = start, velocity = Vector3.zero, startLifetime = lifetime + 0.05f,
                    startSize = size, startColor = color
                }, 1);
            }
            activeParticleCount = Mathf.Min(activeParticleCount, particles.GetParticles(liveParticles));
            return activeParticleCount > 0;
        }

        void Update()
        {
            if (!playing) return;
            if (!attractionTarget)
            {
                Destroy(gameObject);
                return;
            }

            age += Time.unscaledDeltaTime;
            var targetPosition = attractionTarget.position + Vector3.up * targetHeight;
            if (!IsFinite(targetPosition))
            {
                Destroy(gameObject);
                return;
            }
            var count = Mathf.Min(activeParticleCount, particles.GetParticles(liveParticles));
            for (var i = 0; i < count; i++)
            {
                var motion = motions[i];
                var particle = liveParticles[i];
                var travel = 0f;
                if (age < motion.attractionDelay)
                {
                    var burstT = Mathf.Clamp01(age / Mathf.Max(0.01f, motion.attractionDelay));
                    particle.position = Vector3.LerpUnclamped(
                        motion.start, motion.burstEnd, 1f - Mathf.Pow(1f - burstT, 3f));
                }
                else
                {
                    travel = Mathf.Clamp01((age - motion.attractionDelay) /
                                           Mathf.Max(0.05f, motion.lifetime - motion.attractionDelay));
                    particle.position = QuadraticBezier(
                        motion.burstEnd, motion.control, targetPosition, travel * travel);
                }

                var color = motion.color;
                color.a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, travel));
                particle.startColor = color;
                particle.startSize = motion.size * Mathf.Lerp(1f, 0.28f, travel);
                particle.remainingLifetime = Mathf.Max(0.02f, motion.lifetime - age);
                liveParticles[i] = particle;
            }
            particles.SetParticles(liveParticles, count);
            if (age >= maximumLifetime + 0.04f) Destroy(gameObject);
        }

        void CollectSourceRenderers(GameObject sourceTarget)
        {
            sourceRenderers.Clear();
            rendererWeights.Clear();
            var renderers = sourceTarget.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!IsEligible(renderer)) continue;
                sourceRenderers.Add(renderer);
                rendererWeights.Add(Mathf.Max(0.01f, renderer.bounds.size.x * renderer.bounds.size.y));
            }
        }

        static bool IsEligible(SpriteRenderer renderer)
        {
            if (!renderer || !renderer.enabled || !renderer.sprite ||
                !renderer.gameObject.activeInHierarchy) return false;
            var lowerName = renderer.name.ToLowerInvariant();
            if (lowerName.Contains("shadow") || lowerName.Contains("telegraph") ||
                lowerName.Contains("indicator") || lowerName.Contains("warning") ||
                lowerName.Contains("effect") || lowerName.Contains("vfx")) return false;
            return renderer.bounds.size.x > 0.01f && renderer.bounds.size.y > 0.01f;
        }

        Vector3 SampleSourcePosition(System.Random random, out SpriteRenderer selected)
        {
            var totalWeight = 0f;
            for (var i = 0; i < rendererWeights.Count; i++) totalWeight += rendererWeights[i];
            var selection = NextFloat(random) * totalWeight;
            var selectedIndex = sourceRenderers.Count - 1;
            for (var i = 0; i < rendererWeights.Count; i++)
            {
                selection -= rendererWeights[i];
                if (selection > 0f) continue;
                selectedIndex = i;
                break;
            }

            selected = sourceRenderers[selectedIndex];
            if (selected.drawMode != SpriteDrawMode.Simple)
            {
                var bounds = selected.bounds;
                return new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, NextFloat(random)),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, NextFloat(random)),
                    selected.transform.position.z);
            }

            var spriteBounds = selected.sprite.bounds;
            return selected.transform.TransformPoint(new Vector3(
                Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, NextFloat(random)),
                Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, NextFloat(random)), 0f));
        }

        Color SampleSourceColor(SpriteRenderer sourceRenderer, System.Random random, float energyAmount)
        {
            var palette = sourceRenderer && sourceRenderer.sprite
                ? GetSpritePalette(sourceRenderer.sprite)
                : null;
            var sampled = palette != null && palette.Length > 0
                ? (Color)palette[random.Next(palette.Length)]
                : Color.white;

            if (sourceRenderer)
            {
                sampled.r *= sourceRenderer.color.r;
                sampled.g *= sourceRenderer.color.g;
                sampled.b *= sourceRenderer.color.b;
            }
            else
            {
                sampled = Color.Lerp(lowEnergyColor, highEnergyColor, 0.5f);
            }

            var brightness = Mathf.Lerp(0.9f, 1.15f,
                Mathf.Clamp01(NextFloat(random) * 0.75f + energyAmount * 0.04f));
            sampled.r = Mathf.Clamp01(sampled.r * brightness);
            sampled.g = Mathf.Clamp01(sampled.g * brightness);
            sampled.b = Mathf.Clamp01(sampled.b * brightness);
            sampled.a = 1f;
            return sampled;
        }

        static Color32[] GetSpritePalette(Sprite sprite)
        {
            if (!sprite || !sprite.texture) return System.Array.Empty<Color32>();
            var key = sprite.GetInstanceID();
            if (spritePaletteCache.TryGetValue(key, out var cached)) return cached;
            if (spritePaletteCache.Count >= MaximumCachedPalettes) spritePaletteCache.Clear();

            var palette = ReadSpritePalette(sprite);
            spritePaletteCache[key] = palette;
            return palette;
        }

        static Color32[] ReadSpritePalette(Sprite sprite)
        {
            var previous = RenderTexture.active;
            RenderTexture temporary = null;
            try
            {
                var texture = sprite.texture;
                var rect = sprite.textureRect;
                temporary = RenderTexture.GetTemporary(
                    PaletteReadbackSize, PaletteReadbackSize, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                var scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
                var offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
                Graphics.Blit(texture, temporary, scale, offset);
                RenderTexture.active = temporary;
                if (!paletteReadbackTexture)
                {
                    paletteReadbackTexture = new Texture2D(
                        PaletteReadbackSize, PaletteReadbackSize, TextureFormat.RGBA32, false, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point
                    };
                }

                paletteReadbackTexture.ReadPixels(
                    new Rect(0f, 0f, PaletteReadbackSize, PaletteReadbackSize), 0, 0, false);
                paletteReadbackTexture.Apply(false, false);
                var pixels = paletteReadbackTexture.GetPixels32();
                var visible = new List<Color32>(pixels.Length);
                for (var i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a < 32) continue;
                    visible.Add(pixels[i]);
                }
                return visible.ToArray();
            }
            catch (UnityException)
            {
                return System.Array.Empty<Color32>();
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary) RenderTexture.ReleaseTemporary(temporary);
            }
        }
        void ConfigureParticleSystem()
        {
            if (!particles)
            {
                particles = GetComponent<ParticleSystem>();
                if (!particles) particles = gameObject.AddComponent<ParticleSystem>();
            }
            if (!particleRenderer) particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (!particles || !particleRenderer) return;

            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startSpeed = 0f;
            main.maxParticles = MaximumParticleCount;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = sortingOrder;
            particleRenderer.sortMode = ParticleSystemSortMode.OldestInFront;
            particleRenderer.sharedMaterial = PixelParticleUtility.GetLightDotMaterial();
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            var oneMinus = 1f - t;
            return oneMinus * oneMinus * start + 2f * oneMinus * t * control + t * t * end;
        }

        static Vector3 RandomDirection(System.Random random)
        {
            var angle = NextFloat(random) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        static float NextFloat(System.Random random)
        {
            return (float)random.NextDouble();
        }
    }
}
