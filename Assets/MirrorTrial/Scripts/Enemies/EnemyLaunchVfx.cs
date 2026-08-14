using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    public sealed class EnemyLaunchVfx : MonoBehaviour
    {
        enum Kind { Burst, Trail, Landing }

        SpriteRenderer spriteRenderer;
        Kind kind;
        float age;
        float lifetime;
        float baseScale;
        bool stopping;
        ParticleSystem particles;
        Vector2 currentDirection = Vector2.up;
        float emissionTimer;

        public static GameObject SpawnBurst(Vector3 position, Vector2 direction, float scale)
        {
            var effect = Create("Launch Burst VFX", "Effects/pixel_launch_burst", position, null, Kind.Burst, 0.18f, scale);
            SetDirection(effect, direction);
            var vfx = effect ? effect.GetComponent<EnemyLaunchVfx>() : null;
            if (vfx) vfx.EmitBurst();
            return effect;
        }

        public static GameObject SpawnTrail(Transform parent, Vector3 position, Vector2 direction, float scale)
        {
            var effect = Create("Airborne Trail VFX", "Effects/pixel_launch_trail", position, parent, Kind.Trail, 0f, scale);
            SetDirection(effect, direction);
            return effect;
        }

        public static GameObject SpawnLanding(Vector3 position, float scale)
        {
            return Create("Landing Impact VFX", "Effects/pixel_launch_landing", position, null, Kind.Landing, 0.24f, scale);
        }

        public static void SetDirection(GameObject effect, Vector2 direction)
        {
            if (!effect || direction.sqrMagnitude < 0.0001f) return;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 45f;
            effect.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var vfx = effect.GetComponent<EnemyLaunchVfx>();
            if (vfx) vfx.currentDirection = direction.normalized;
        }

        public static void StopTrail(GameObject effect)
        {
            if (!effect) return;
            var vfx = effect.GetComponent<EnemyLaunchVfx>();
            if (!vfx) { Destroy(effect); return; }
            vfx.stopping = true;
            vfx.age = 0f;
            vfx.lifetime = 0.1f;
        }

        static GameObject Create(string name, string resourcePath, Vector3 position, Transform parent, Kind kind, float lifetime, float scale)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (!sprite) return null;

            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 110;
            var vfx = go.AddComponent<EnemyLaunchVfx>();
            vfx.spriteRenderer = renderer;
            vfx.kind = kind;
            vfx.lifetime = lifetime;
            vfx.baseScale = Mathf.Max(0.1f, scale);
            go.transform.localScale = Vector3.one * vfx.baseScale;
            vfx.particles = PixelParticleUtility.Create(go.transform, 116);
            if (kind == Kind.Landing) vfx.EmitLanding();
            return go;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;

            if (kind == Kind.Trail && !stopping)
            {
                EmitTrail();
                var pulse = 1f + Mathf.Sin(Time.unscaledTime * 24f) * 0.07f;
                transform.localScale = Vector3.one * baseScale * pulse;
                var trailColor = spriteRenderer.color;
                trailColor.a = 0.78f + Mathf.Sin(Time.unscaledTime * 18f) * 0.12f;
                spriteRenderer.color = trailColor;
                return;
            }

            var t = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));
            var start = kind == Kind.Landing ? 0.62f : 0.45f;
            var end = kind == Kind.Landing ? 1.18f : 1.12f;
            transform.localScale = Vector3.one * baseScale * Mathf.Lerp(start, end, t);
            var color = spriteRenderer.color;
            color.a = 1f - t;
            spriteRenderer.color = color;
            if (t >= 1f) Destroy(gameObject);
        }

        void EmitBurst()
        {
            var direction = currentDirection.sqrMagnitude > 0.001f ? currentDirection : Vector2.up;
            for (var i = 0; i < 24; i++)
            {
                var spread = Random.Range(-38f, 38f);
                var velocity = Quaternion.Euler(0f, 0f, spread) * direction * Random.Range(2.8f, 6.5f) * Mathf.Sqrt(baseScale);
                var color = Color.Lerp(new Color(0.52f, 0.78f, 1f, 1f), Color.white, Random.Range(0.45f, 1f));
                PixelParticleUtility.Emit(particles, transform.position, velocity, Random.Range(0.07f, 0.19f) * baseScale, Random.Range(0.12f, 0.3f), color);
            }
        }

        void EmitTrail()
        {
            emissionTimer -= Time.unscaledDeltaTime;
            if (emissionTimer > 0f) return;
            emissionTimer = 0.025f;
            var direction = currentDirection.sqrMagnitude > 0.001f ? currentDirection : Vector2.up;
            for (var i = 0; i < 3; i++)
            {
                var side = Vector2.Perpendicular(direction) * Random.Range(-0.28f, 0.28f) * baseScale;
                var position = transform.position - (Vector3)(direction * Random.Range(0.05f, 0.35f) * baseScale + side);
                var velocity = -direction * Random.Range(0.8f, 2.2f) + side * Random.Range(-1f, 1f);
                var color = Color.Lerp(new Color(0.46f, 0.7f, 1f, 0.9f), new Color(0.95f, 0.98f, 1f, 1f), Random.Range(0.4f, 1f));
                PixelParticleUtility.Emit(particles, position, velocity, Random.Range(0.045f, 0.13f) * baseScale, Random.Range(0.16f, 0.36f), color);
            }
        }

        void EmitLanding()
        {
            for (var i = 0; i < 30; i++)
            {
                var velocity = new Vector2(Random.Range(-4.2f, 4.2f), Random.Range(0.7f, 3.8f)) * Mathf.Sqrt(baseScale);
                var color = Color.Lerp(new Color(0.48f, 0.55f, 0.78f, 0.9f), new Color(0.9f, 0.96f, 1f, 1f), Random.Range(0.25f, 0.85f));
                PixelParticleUtility.Emit(particles, transform.position, velocity, Random.Range(0.08f, 0.24f) * baseScale, Random.Range(0.22f, 0.5f), color);
            }
        }
    }
}
