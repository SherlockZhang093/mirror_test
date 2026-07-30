using UnityEngine;

namespace MirrorTrial.Combat
{
    public static class PixelParticleUtility
    {
        static Material particleMaterial;
        static Material dotParticleMaterial;

        public static ParticleSystem Create(Transform owner, int sortingOrder = 115, bool useLightDot = false)
        {
            var particleObject = new GameObject("Pixel Particles");
            particleObject.transform.SetParent(owner, false);
            var system = particleObject.AddComponent<ParticleSystem>();

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startSpeed = 0f;
            main.startLifetime = 0.3f;
            main.startSize = 0.1f;
            main.maxParticles = 160;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;
            renderer.sortMode = ParticleSystemSortMode.OldestInFront;
            renderer.sharedMaterial = useLightDot ? GetDotParticleMaterial() : GetParticleMaterial();

            system.Play();
            return system;
        }

        public static void Emit(ParticleSystem system, Vector3 position, Vector2 velocity, float size, float lifetime, Color color)
        {
            if (!system) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = Mathf.Max(0.01f, size),
                startLifetime = Mathf.Max(0.02f, lifetime),
                startColor = color
            };
            system.Emit(emit, 1);
        }

        static Material GetParticleMaterial()
        {
            if (particleMaterial) return particleMaterial;
            var shader = Shader.Find("Sprites/Default");
            particleMaterial = new Material(shader) { name = "Runtime Pixel Particle Material" };
            var sprite = Resources.Load<Sprite>("Effects/pixel_mirror_shard");
            if (sprite) particleMaterial.mainTexture = sprite.texture;
            return particleMaterial;
        }

        static Material GetDotParticleMaterial()
        {
            if (dotParticleMaterial) return dotParticleMaterial;
            var shader = Shader.Find("Sprites/Default");
            dotParticleMaterial = new Material(shader) { name = "Runtime Light Dot Particle Material" };
            var texture = new Texture2D(5, 5, TextureFormat.RGBA32, false)
            {
                name = "Runtime Light Dot",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[25];
            for (var y = 0; y < 5; y++)
            for (var x = 0; x < 5; x++)
            {
                var dx = x - 2;
                var dy = y - 2;
                pixels[y * 5 + x] = dx * dx + dy * dy <= 4 ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            dotParticleMaterial.mainTexture = texture;
            return dotParticleMaterial;
        }
    }
}
