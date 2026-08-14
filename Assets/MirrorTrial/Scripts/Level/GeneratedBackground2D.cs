using UnityEngine;

namespace MirrorTrial.Level
{
    [ExecuteAlways]
    public sealed class GeneratedBackground2D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer skyRenderer;
        [SerializeField] private SpriteRenderer ruinsRenderer;
        [SerializeField] private SpriteRenderer farFogRenderer;
        [SerializeField] private SpriteRenderer midgroundRenderer;
        [SerializeField] private SpriteRenderer nearFogRenderer;
        [SerializeField] private SpriteRenderer foregroundRenderer;
        [SerializeField, Range(0f, 0.3f)] private float ruinsParallax = 0.08f;
        [SerializeField, Range(0f, 0.4f)] private float midgroundParallax = 0.16f;
        [SerializeField, Range(0f, 0.7f)] private float foregroundParallax = 0.38f;
        [SerializeField, Min(1f)] private float skyOverscan = 1.03f;
        [SerializeField, Min(1f)] private float ruinsOverscan = 1.18f;
        [SerializeField, Min(1f)] private float midgroundOverscan = 1.28f;
        [SerializeField, Min(1f)] private float foregroundOverscan = 1.18f;

        private Vector3 origin;
        private Vector3 cameraOrigin;
        private Vector3 ruinsOrigin;
        private Vector3 farFogOrigin;
        private Vector3 midgroundOrigin;
        private Vector3 nearFogOrigin;
        private Vector3 foregroundOrigin;
        private ParticleSystem ambientDust;
        private ParticleSystem driftingMist;
        private ParticleSystem cyanMotes;

        public void Configure(Camera camera, SpriteRenderer sky, SpriteRenderer ruins,
            SpriteRenderer farFog, SpriteRenderer midground, SpriteRenderer nearFog,
            SpriteRenderer foreground)
        {
            targetCamera = camera;
            skyRenderer = sky;
            ruinsRenderer = ruins;
            farFogRenderer = farFog;
            midgroundRenderer = midground;
            nearFogRenderer = nearFog;
            foregroundRenderer = foreground;
            CaptureOrigin();
            if (Application.isPlaying) EnsureParticleEffects();
            Refresh();
        }

        private void OnEnable()
        {
            CaptureOrigin();
            if (Application.isPlaying) EnsureParticleEffects();
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void CaptureOrigin()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            origin = transform.position;
            if (targetCamera != null) cameraOrigin = targetCamera.transform.position;
            ruinsOrigin = GetLocalPosition(ruinsRenderer);
            farFogOrigin = GetLocalPosition(farFogRenderer);
            midgroundOrigin = GetLocalPosition(midgroundRenderer);
            nearFogOrigin = GetLocalPosition(nearFogRenderer);
            foregroundOrigin = GetLocalPosition(foregroundRenderer);
        }

        private void Refresh()
        {
            if (targetCamera == null || !targetCamera.orthographic) return;

            Vector3 cameraPosition = targetCamera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, origin.z);
            FitToCamera(skyRenderer, skyOverscan);
            FitToCamera(ruinsRenderer, ruinsOverscan);
            FitToCamera(farFogRenderer, 1.22f);
            FitToCamera(midgroundRenderer, midgroundOverscan);
            FitToCamera(nearFogRenderer, 1.3f);
            FitToCamera(foregroundRenderer, foregroundOverscan);

            Vector3 delta = cameraPosition - cameraOrigin;
            ApplyParallax(ruinsRenderer, ruinsOrigin, delta, ruinsParallax, 0.35f);
            ApplyParallax(farFogRenderer, farFogOrigin, delta, 0.11f, 0.08f);
            ApplyParallax(midgroundRenderer, midgroundOrigin, delta, midgroundParallax, 0.22f);
            ApplyParallax(nearFogRenderer, nearFogOrigin, delta, 0.24f, 0.12f);
            ApplyParallax(foregroundRenderer, foregroundOrigin, delta, foregroundParallax, 0.18f);
            RefreshParticleBounds();
        }

        private void EnsureParticleEffects()
        {
            RemoveScreenSpaceEffect("AmbientDust");
            RemoveScreenSpaceEffect("DriftingMist");
            RemoveScreenSpaceEffect("CyanMotes");
            CreateLocalizedPlatformMotes();
        }

        private void RemoveScreenSpaceEffect(string effectName)
        {
            Transform existing = transform.Find(effectName);
            if (existing != null) Destroy(existing.gameObject);
        }

        private void CreateLocalizedPlatformMotes()
        {
            SpriteRenderer[] renderers = FindObjectsOfType<SpriteRenderer>();
            foreach (SpriteRenderer platformRenderer in renderers)
            {
                if (platformRenderer == null || platformRenderer.sprite == null ||
                    platformRenderer.gameObject.name != "PlatformVisual")
                    continue;

                Transform oldEffect = platformRenderer.transform.Find("LocalizedCyanMotes");
                if (oldEffect != null) Destroy(oldEffect.gameObject);

                GameObject layer = new GameObject("LocalizedCyanMotes");
                layer.transform.SetParent(platformRenderer.transform, false);
                layer.transform.localPosition = new Vector3(
                    platformRenderer.sprite.bounds.center.x,
                    platformRenderer.sprite.bounds.max.y + 0.025f,
                    0f);

                ParticleSystem system = layer.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.05f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.03f, 0.48f, 0.7f, 0.12f),
                    new Color(0.1f, 0.85f, 1f, 0.34f));
                main.maxParticles = 18;

                ParticleSystem.EmissionModule emission = system.emission;
                float platformWidth = platformRenderer.sprite.bounds.size.x;
                emission.rateOverTime = Mathf.Clamp(platformWidth * 0.24f, 0.35f, 1.1f);

                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(platformWidth * 0.82f, 0.025f, 0.05f);

                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(0f);
                velocity.y = new ParticleSystem.MinMaxCurve(0.065f);

                ParticleSystem.NoiseModule noise = system.noise;
                noise.enabled = true;
                noise.strength = 0.025f;
                noise.frequency = 0.32f;
                noise.scrollSpeed = 0.08f;
                noise.damping = true;

                ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
                fade.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.16f),
                        new GradientAlphaKey(0.62f, 0.68f), new GradientAlphaKey(0f, 1f)
                    });
                fade.color = gradient;

                ParticleSystemRenderer particleRenderer = layer.GetComponent<ParticleSystemRenderer>();
                particleRenderer.sortingLayerID = platformRenderer.sortingLayerID;
                particleRenderer.sortingOrder = platformRenderer.sortingOrder + 1;
                particleRenderer.material = CreateParticleMaterial(
                    "LocalizedCyanMotesMaterial", Color.white, 32, false);
                system.Play();
            }
        }

        private ParticleSystem CreateParticleLayer(string layerName, Color color, int sortingOrder,
            float emissionRate, float lifetime, Vector2 sizeRange, Vector2 horizontalSpeed,
            Vector2 verticalSpeed, bool softEllipse)
        {
            Transform existing = transform.Find(layerName);
            GameObject layer = existing != null ? existing.gameObject : new GameObject(layerName);
            layer.transform.SetParent(transform, false);

            ParticleSystem system = layer.GetComponent<ParticleSystem>();
            if (system == null) system = layer.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startColor = color;
            main.maxParticles = softEllipse ? 24 : 180;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = emissionRate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve((horizontalSpeed.x + horizontalSpeed.y) * 0.5f);
            velocity.y = new ParticleSystem.MinMaxCurve((verticalSpeed.x + verticalSpeed.y) * 0.5f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = softEllipse ? 0.12f : 0.035f;
            noise.frequency = softEllipse ? 0.16f : 0.28f;
            noise.scrollSpeed = 0.08f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(0.75f, 0.72f), new GradientAlphaKey(0f, 1f)
                });
            fade.color = gradient;

            ParticleSystemRenderer particleRenderer = layer.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sortingOrder = sortingOrder;
            particleRenderer.material = CreateParticleMaterial(layerName + "Material", color,
                softEllipse ? 64 : 32, softEllipse);

            if (!system.isPlaying) system.Play();
            return system;
        }

        private static Material CreateParticleMaterial(string materialName, Color color, int textureSize,
            bool ellipse)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

            Material material = new Material(shader) { name = materialName };
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = materialName + "Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float nx = (x + 0.5f) / textureSize * 2f - 1f;
                    float ny = (y + 0.5f) / textureSize * 2f - 1f;
                    if (ellipse) ny *= 2.6f;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), 1.8f);
                    pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            material.mainTexture = texture;
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            return material;
        }

        private void RefreshParticleBounds()
        {
            if (!Application.isPlaying || targetCamera == null) return;
            float cameraHeight = targetCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * targetCamera.aspect;
            SetParticleBounds(ambientDust, cameraWidth * 1.05f, cameraHeight * 1.05f, 0f);
            SetParticleBounds(driftingMist, cameraWidth * 1.15f, cameraHeight * 0.55f, -cameraHeight * 0.2f);
            SetParticleBounds(cyanMotes, cameraWidth * 0.92f, cameraHeight * 0.45f, -cameraHeight * 0.22f);
        }

        private static void SetParticleBounds(ParticleSystem system, float width, float height, float y)
        {
            if (system == null) return;
            system.transform.localPosition = new Vector3(0f, y, 0f);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.scale = new Vector3(width, height, 0.1f);
        }

        private static Vector3 GetLocalPosition(SpriteRenderer renderer)
        {
            return renderer == null ? Vector3.zero : renderer.transform.localPosition;
        }

        private static void ApplyParallax(SpriteRenderer renderer, Vector3 layerOrigin,
            Vector3 cameraDelta, float horizontalAmount, float verticalMultiplier)
        {
            if (renderer == null) return;
            renderer.transform.localPosition = layerOrigin + new Vector3(
                -cameraDelta.x * horizontalAmount,
                -cameraDelta.y * horizontalAmount * verticalMultiplier,
                0f);
        }

        private void FitToCamera(SpriteRenderer renderer, float overscan)
        {
            if (renderer == null || renderer.sprite == null) return;
            float cameraHeight = targetCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * targetCamera.aspect;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            float scale = Mathf.Max(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y) * overscan;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}