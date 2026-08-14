using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class UpdraftWindVfx : MonoBehaviour
    {
        [Header("Playback")]
        [SerializeField] bool playOnEnable = true;
        [SerializeField, Range(0.25f, 2f)] float intensity = 1f;

        [Header("Particle Layers")]
        [SerializeField] ParticleSystem primaryWindLines;
        [SerializeField] ParticleSystem shortWindWisps;
        [SerializeField] ParticleSystem liftedLeaves;
        [SerializeField] ParticleSystem floatingMotes;
        [SerializeField] ParticleSystem baseDust;

        ParticleSystem[] layers;
        float appliedIntensity = 1f;

        public float Intensity => intensity;

        void Awake()
        {
            CacheLayers();
            ApplyIntensity();
        }

        void OnEnable()
        {
            CacheLayers();
            ApplyIntensity();
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            if (layers == null) return;
            foreach (var layer in layers)
                if (layer) layer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnValidate()
        {
            intensity = Mathf.Clamp(intensity, 0.25f, 2f);
            CacheLayers();
            ApplyIntensity();
        }

        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp(value, 0.25f, 2f);
            ApplyIntensity();
        }

        public void Play()
        {
            CacheLayers();
            foreach (var layer in layers)
                if (layer && !layer.isPlaying) layer.Play(true);
        }

        public void Stop(bool clear = true)
        {
            CacheLayers();
            var behavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;
            foreach (var layer in layers)
                if (layer) layer.Stop(true, behavior);
        }

        public void AssignLayers(
            ParticleSystem primary,
            ParticleSystem wisps,
            ParticleSystem leaves,
            ParticleSystem motes,
            ParticleSystem dust)
        {
            primaryWindLines = primary;
            shortWindWisps = wisps;
            liftedLeaves = leaves;
            floatingMotes = motes;
            baseDust = dust;
            CacheLayers();
            ApplyIntensity();
        }

        void CacheLayers()
        {
            layers = new[]
            {
                primaryWindLines,
                shortWindWisps,
                liftedLeaves,
                floatingMotes,
                baseDust
            };
        }

        void ApplyIntensity()
        {
            if (layers == null) return;
            var previousIntensity = Mathf.Max(0.0001f, appliedIntensity);
            foreach (var layer in layers)
            {
                if (!layer) continue;
                var emission = layer.emission;
                emission.rateOverTimeMultiplier =
                    emission.rateOverTimeMultiplier / previousIntensity * intensity;
            }
            appliedIntensity = intensity;
        }
    }
}
