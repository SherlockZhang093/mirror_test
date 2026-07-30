using UnityEngine;

namespace MirrorTrial.Combat
{
    public sealed class PixelCombatVfx : MonoBehaviour
    {
        enum Mode { Charge, Hit }
        SpriteRenderer spriteRenderer;
        Mode mode;
        float age;
        float lifetime;
        float baseScale;
        float scaleMultiplier = 1f;
        float chargeNormalized;
        float particleTimer;
        ParticleSystem chargeParticles;
        float chargeRotationSpeed = 55f;
        float chargePulseSpeed = 18f;
        float chargePulseAmount = 0.12f;
        float chargeStartScale = 0.9f;
        float chargeFullScale = 1.8f;
        float chargeStartAlpha = 0.55f;
        float chargeFullAlpha = 1f;
        float chargeParticleIntensity = 1f;

        public static GameObject SpawnCharge(Transform parent)
        {
            return Create("Charge Pixel VFX", "Effects/pixel_charge_ring", parent.position, parent, Mode.Charge, 1f);
        }

        public static GameObject SpawnHit(Vector3 position)
        {
            return Create("Hit Pixel VFX", "Effects/pixel_hit_burst", position, null, Mode.Hit, 0.15f);
        }

        public static void SetCharge(GameObject effect, float normalized)
        {
            if (!effect) return;
            var vfx = effect.GetComponent<PixelCombatVfx>();
            if (vfx) vfx.ApplyCharge(Mathf.Clamp01(normalized));
        }

        public static void SetScale(GameObject effect, float scale)
        {
            if (!effect) return;
            var vfx = effect.GetComponent<PixelCombatVfx>();
            if (vfx) vfx.scaleMultiplier *= Mathf.Max(0.1f, scale);
            else effect.transform.localScale *= Mathf.Max(0.1f, scale);
        }

        public static void ConfigureCharge(GameObject effect, float rotationSpeed, float pulseSpeed,
            float pulseAmount, float startScale, float fullScale, float startAlpha, float fullAlpha,
            float particleIntensity)
        {
            if (!effect) return;
            var vfx = effect.GetComponent<PixelCombatVfx>();
            if (!vfx) return;
            vfx.chargeRotationSpeed = rotationSpeed;
            vfx.chargePulseSpeed = Mathf.Max(0f, pulseSpeed);
            vfx.chargePulseAmount = Mathf.Clamp01(pulseAmount);
            vfx.chargeStartScale = Mathf.Max(0.01f, startScale);
            vfx.chargeFullScale = Mathf.Max(0.01f, fullScale);
            vfx.chargeStartAlpha = Mathf.Clamp01(startAlpha);
            vfx.chargeFullAlpha = Mathf.Clamp01(fullAlpha);
            vfx.chargeParticleIntensity = Mathf.Clamp(particleIntensity, 0f, 2f);
            vfx.ApplyCharge(vfx.chargeNormalized);
        }
        static GameObject Create(string name, string resourcePath, Vector3 position, Transform parent, Mode mode, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Resources.Load<Sprite>(resourcePath);
            renderer.sortingOrder = 100;
            var vfx = go.AddComponent<PixelCombatVfx>();
            vfx.spriteRenderer = renderer;
            vfx.mode = mode;
            vfx.lifetime = lifetime;
            vfx.baseScale = mode == Mode.Hit ? 0.9f : 0.95f;
            go.transform.localScale = Vector3.one * vfx.baseScale;
            return go;
        }

        void Awake()
        {
            if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
            if (baseScale <= 0f) baseScale = 0.95f;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            if (mode == Mode.Charge)
            {
                EnsureChargeParticles();
                EmitChargeParticles();
                var pulse = 1f + Mathf.Sin(Time.unscaledTime * chargePulseSpeed) * chargePulseAmount;
                transform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * chargeRotationSpeed);
                transform.localScale = Vector3.one * baseScale * scaleMultiplier * pulse;
                return;
            }

            var t = Mathf.Clamp01(age / lifetime);
            transform.localScale = Vector3.one * baseScale * scaleMultiplier * Mathf.Lerp(0.55f, 1.25f, t);
            var color = spriteRenderer.color;
            color.a = 1f - t;
            spriteRenderer.color = color;
            if (t >= 1f) Destroy(gameObject);
        }

        void ApplyCharge(float normalized)
        {
            chargeNormalized = normalized;
            baseScale = Mathf.Lerp(chargeStartScale, chargeFullScale, normalized);
            var color = spriteRenderer.color;
            color.a = Mathf.Lerp(chargeStartAlpha, chargeFullAlpha, normalized);
            spriteRenderer.color = color;
        }

        void EnsureChargeParticles()
        {
            if (!chargeParticles)
                chargeParticles = PixelParticleUtility.Create(transform, 105);
        }

        void EmitChargeParticles()
        {
            if (chargeParticleIntensity <= 0f) return;
            particleTimer -= Time.unscaledDeltaTime;
            if (particleTimer > 0f) return;
            particleTimer = Mathf.Lerp(0.07f, 0.018f, chargeNormalized) / chargeParticleIntensity;
            var count = Mathf.Max(1, Mathf.RoundToInt((chargeNormalized > 0.65f ? 3f : 2f) * chargeParticleIntensity));
            var radius = baseScale * scaleMultiplier * 0.58f;
            for (var i = 0; i < count; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var position = transform.position + (Vector3)(radial * radius);
                var tangent = new Vector2(-radial.y, radial.x) * Random.Range(-0.45f, 0.45f);
                var velocity = -radial * Mathf.Lerp(1.1f, 2.4f, chargeNormalized) + tangent;
                var particleColor = Color.Lerp(new Color(0.42f, 0.68f, 1f, 0.85f), new Color(0.95f, 0.98f, 1f, 1f), Random.Range(0.35f, 0.95f));
                PixelParticleUtility.Emit(chargeParticles, position, velocity, Random.Range(0.045f, 0.11f) * Mathf.Lerp(0.8f, 1.35f, chargeNormalized), Random.Range(0.22f, 0.42f), particleColor);
            }
        }
    }
}
