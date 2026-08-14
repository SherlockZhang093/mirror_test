using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MirrorTrial.HealthResources
{
    [DisallowMultipleComponent]
    public sealed class HealthResourcePlantGlow : MonoBehaviour
    {
        [SerializeField] HealthResourceNode node;
        [SerializeField] Light2D glowLight;
        [SerializeField] SpriteRenderer[] motes;
        [SerializeField] ParticleSystem[] sparkleParticles;
        [SerializeField, Min(0f)] float baseIntensity = 0.58f;
        [SerializeField, Min(0.1f)] float pulseSpeed = 1.6f;
        [SerializeField, Min(0f)] float moteDrift = 0.08f;

        Vector3[] moteOrigins;
        float visibleStrength = 1f;
        bool particlesStopped;

        void Awake()
        {
            if (!node) node = GetComponentInParent<HealthResourceNode>();
            if (!glowLight) glowLight = GetComponent<Light2D>();
            if (motes == null || motes.Length == 0)
                motes = GetComponentsInChildren<SpriteRenderer>(true);
            if (sparkleParticles == null || sparkleParticles.Length == 0)
                sparkleParticles = GetComponentsInChildren<ParticleSystem>(true);
            moteOrigins = new Vector3[motes.Length];
            for (var i = 0; i < motes.Length; i++)
                if (motes[i]) moteOrigins[i] = motes[i].transform.localPosition;
        }

        void Update()
        {
            var durability = node && node.IsInitialized && node.MaxDurability > 0
                ? node.CurrentDurability / (float)node.MaxDurability
                : 1f;
            var targetStrength = node && node.IsDestroyed ? 0f : Mathf.Lerp(0.35f, 1f, durability);
            visibleStrength = Mathf.MoveTowards(visibleStrength, targetStrength, Time.deltaTime * 4f);
            var pulse = 0.86f + Mathf.Sin(Time.time * pulseSpeed) * 0.14f;
            if (glowLight) glowLight.intensity = baseIntensity * visibleStrength * pulse;

            var shouldStopParticles = node && node.IsDestroyed;
            if (shouldStopParticles != particlesStopped)
            {
                particlesStopped = shouldStopParticles;
                for (var i = 0; i < sparkleParticles.Length; i++)
                {
                    var particles = sparkleParticles[i];
                    if (!particles) continue;
                    if (particlesStopped)
                        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    else if (!particles.isPlaying)
                        particles.Play(true);
                }
            }

            for (var i = 0; i < motes.Length; i++)
            {
                var mote = motes[i];
                if (!mote) continue;
                var phase = Time.time * (0.65f + i * 0.13f) + i * 2.1f;
                mote.transform.localPosition = moteOrigins[i] + new Vector3(
                    Mathf.Sin(phase * 0.7f) * moteDrift,
                    Mathf.Sin(phase) * moteDrift,
                    0f);
                var c = mote.color;
                c.a = visibleStrength * (0.22f + Mathf.Sin(phase) * 0.08f);
                mote.color = c;
                mote.transform.localScale = Vector3.one * (0.12f + Mathf.Sin(phase * 1.2f) * 0.025f);
            }
        }
    }
}
