using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class AirflowVfx : MonoBehaviour
    {
        [SerializeField] ParticleSystem particles;
        [SerializeField] Color lowerColor = new Color(0.25f, 0.9f, 1f, 0.15f);
        [SerializeField] Color upperColor = new Color(1f, 0.85f, 0.3f, 0.75f);

        void Awake()
        {
            if (!particles) particles = GetComponent<ParticleSystem>();
            if (!particles) particles = gameObject.AddComponent<ParticleSystem>();
            Configure();
        }

        void Configure()
        {
            var main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = new ParticleSystem.MinMaxGradient(lowerColor, upperColor);

            var emission = particles.emission;
            emission.rateOverTime = 48f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Rectangle;
            shape.scale = new Vector3(1.25f, 0.05f, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.6f;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(upperColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.18f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 70;
        }
    }
}
