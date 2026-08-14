using UnityEngine;

namespace MirrorTrial.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerDoubleJumpFootVfx : MonoBehaviour
    {
        [SerializeField] SpriteRenderer echoRenderer;
        [SerializeField, Min(0.05f)] float lifetime = 0.32f;
        [SerializeField] Vector2 startScale = new Vector2(0.48f, 0.72f);
        [SerializeField] Vector2 burstScale = new Vector2(1.12f, 0.96f);
        [SerializeField] Vector2 endScale = new Vector2(1.02f, 0.78f);
        [SerializeField, Range(0.05f, 0.8f)] float settleStart = 0.52f;
        [SerializeField, Min(0f)] float dropDistance = 0.035f;
        [SerializeField, Min(0f)] float echoDelay = 0.045f;
        [SerializeField, Range(0f, 1f)] float echoAlpha = 0.26f;
        [SerializeField, Min(0f)] float echoDropDistance = 0.055f;

        SpriteRenderer spriteRenderer;
        Color initialColor;
        Color echoInitialColor;
        Vector3 initialPosition;
        float age;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            initialColor = spriteRenderer.color;
            initialPosition = transform.position;
            transform.localScale = ToScale(startScale);

            if (echoRenderer)
            {
                echoInitialColor = echoRenderer.color;
                SetAlpha(echoRenderer, 0f);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / lifetime);
            var burstT = EaseOutCubic(t);
            var settleT = Smooth01(Mathf.InverseLerp(settleStart, 1f, t));
            var scale = Vector2.LerpUnclamped(startScale, burstScale, burstT);
            scale = Vector2.LerpUnclamped(scale, endScale, settleT);
            transform.localScale = ToScale(scale);
            transform.position = initialPosition + Vector3.down * (dropDistance * Smooth01(t));

            var color = initialColor;
            color.a *= 1f - Smooth01(Mathf.InverseLerp(0.38f, 1f, t));
            spriteRenderer.color = color;

            AnimateEcho();

            if (t >= 1f)
                Destroy(gameObject);
        }

        public void ConfigureSorting(int sortingLayerId, int sortingOrder)
        {
            spriteRenderer.sortingLayerID = sortingLayerId;
            spriteRenderer.sortingOrder = sortingOrder;
            if (!echoRenderer)
                return;

            echoRenderer.sortingLayerID = sortingLayerId;
            echoRenderer.sortingOrder = sortingOrder - 1;
        }

        void AnimateEcho()
        {
            if (!echoRenderer || age < echoDelay)
                return;

            var echoT = Mathf.Clamp01((age - echoDelay) / Mathf.Max(0.01f, lifetime - echoDelay));
            var echoEase = EaseOutCubic(echoT);
            echoRenderer.transform.localScale = new Vector3(
                Mathf.Lerp(0.96f, 1.18f, echoEase),
                Mathf.Lerp(0.92f, 0.68f, echoEase),
                1f);
            echoRenderer.transform.localPosition = Vector3.down * (echoDropDistance * echoEase);

            var echoColor = echoInitialColor;
            echoColor.a *= echoAlpha * (1f - Smooth01(echoT));
            echoRenderer.color = echoColor;
        }

        static float EaseOutCubic(float value) => 1f - Mathf.Pow(1f - value, 3f);
        static float Smooth01(float value) => value * value * (3f - 2f * value);

        static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            var color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }

        static Vector3 ToScale(Vector2 scale) => new Vector3(scale.x, scale.y, 1f);
    }
}
