using System.Collections;
using MirrorTrial.Boss;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Level
{
    public sealed class MirrorBattleVisuals : MonoBehaviour
    {
        const string SceneName = "Level_Mirror_01";
        const string BackdropResource = "Environment/mirror_realm_inverted_v1";
        Camera targetCamera;
        SpriteRenderer backdrop;
        Vector3 cameraOrigin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!SceneManager.GetActiveScene().name.Equals(SceneName, System.StringComparison.OrdinalIgnoreCase)) return;
            if (FindObjectOfType<MirrorBattleVisuals>() != null) return;
            new GameObject("MirrorBattleVisuals_鍊掓偓闀滅晫").AddComponent<MirrorBattleVisuals>();
        }

        IEnumerator Start()
        {
            targetCamera = Camera.main;
            if (targetCamera == null) yield break;
            cameraOrigin = targetCamera.transform.position;
            if (backdrop == null) backdrop = transform.Find("InvertedRuinsBackdrop")?.GetComponent<SpriteRenderer>();
            if (backdrop == null) CreateBackdrop();
            CreateParticles("AscendingMirrorShards", new Color(0.48f, 0.82f, 1f, 0.34f), 2.8f, 5.5f, 0.055f, 0.16f, -32);
            CreateParticles("ReverseDust", new Color(0.38f, 0.58f, 0.74f, 0.18f), 14f, 7.5f, 0.018f, 0.05f, -38);
            CreateTopFog();

            while (true)
            {
                MirrorBossActorV2 boss = FindObjectOfType<MirrorBossActorV2>();
                if (boss != null)
                {
                    AddBossAccents(boss.gameObject);
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        void LateUpdate()
        {
            if (targetCamera == null || backdrop == null || !targetCamera.orthographic) return;
            transform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, 8f);
            Fit(backdrop, 1.08f);
            Vector3 delta = targetCamera.transform.position - cameraOrigin;
            backdrop.transform.localPosition = new Vector3(-delta.x * 0.035f, -delta.y * 0.015f, 0f);
            ResizeParticleVolumes();
        }

        void CreateBackdrop()
        {
            Texture2D texture = Resources.Load<Texture2D>(BackdropResource);
            if (texture == null)
            {
                Debug.LogWarning("[MirrorBattleVisuals] Missing inverted mirror realm backdrop.", this);
                return;
            }
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            GameObject layer = new GameObject("InvertedRuinsBackdrop");
            layer.transform.SetParent(transform, false);
            backdrop = layer.AddComponent<SpriteRenderer>();
            backdrop.sprite = sprite;
            backdrop.sortingOrder = -100;
            backdrop.color = new Color(0.82f, 0.9f, 1f, 1f);
            Fit(backdrop, 1.08f);
        }

        void CreateTopFog()
        {
            ParticleSystem fog = CreateParticles("CeilingMist", new Color(0.19f, 0.35f, 0.5f, 0.07f), 1.2f, 9f, 0.9f, 2.3f, -45);
            var velocity = fog.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(0.05f);
            var noise = fog.noise;
            noise.strength = 0.12f;
            noise.frequency = 0.14f;
        }

        ParticleSystem CreateParticles(string name, Color color, float rate, float lifetime, float minSize, float maxSize, int order)
        {
            GameObject layer = new GameObject(name);
            layer.transform.SetParent(transform, false);
            ParticleSystem ps = layer.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            main.startColor = color;
            main.maxParticles = name == "ReverseDust" ? 140 : 42;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.165f);
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0.7f, 0.72f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystemRenderer renderer = layer.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = order;
            renderer.material = CreateParticleMaterial(name, name.Contains("Shard"));
            ps.Play();
            return ps;
        }

        static Material CreateParticleMaterial(string name, bool shard)
        {
            Shader shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader) { name = name + "Material" };
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float alpha = shard
                    ? Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(nx) * 0.72f + Mathf.Abs(ny) * 0.45f, Mathf.Abs(nx + ny * 0.25f)))
                    : Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            material.mainTexture = texture;
            return material;
        }

        void ResizeParticleVolumes()
        {
            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
            {
                bool fog = ps.name == "CeilingMist";
                ps.transform.localPosition = new Vector3(0f, fog ? height * 0.27f : -height * 0.42f, 0f);
                var shape = ps.shape;
                shape.scale = new Vector3(width * (fog ? 1.15f : 0.95f), height * (fog ? 0.34f : 0.25f), 0.1f);
            }
        }

        void Fit(SpriteRenderer renderer, float overscan)
        {
            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;
            Vector2 size = renderer.sprite.bounds.size;
            float scale = Mathf.Max(width / size.x, height / size.y) * overscan;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        static void AddBossAccents(GameObject boss)
        {
            SpriteRenderer source = boss.GetComponentInChildren<SpriteRenderer>();
            if (source == null || source.transform.Find("MirrorEdgeGlow") != null) return;
            GameObject glowObject = new GameObject("MirrorEdgeGlow");
            glowObject.transform.SetParent(source.transform, false);
            glowObject.transform.localScale = new Vector3(1.055f, 1.055f, 1f);
            SpriteRenderer glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = source.sprite;
            glow.color = new Color(0.2f, 0.8f, 1f, 0.16f);
            glow.sortingLayerID = source.sortingLayerID;
            glow.sortingOrder = source.sortingOrder - 1;
            glow.flipX = source.flipX;
            glow.flipY = source.flipY;
            glowObject.AddComponent<MirrorBossGlowFollower>().Bind(source, glow);
        }
    }

    public sealed class MirrorBossGlowFollower : MonoBehaviour
    {
        SpriteRenderer source;
        SpriteRenderer glow;
        public void Bind(SpriteRenderer sourceRenderer, SpriteRenderer glowRenderer) { source = sourceRenderer; glow = glowRenderer; }
        void LateUpdate()
        {
            if (source == null || glow == null) return;
            glow.sprite = source.sprite;
            glow.flipX = source.flipX;
            glow.flipY = source.flipY;
        }
    }
}


