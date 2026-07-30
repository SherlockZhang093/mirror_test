using UnityEngine;

namespace MirrorTrial.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyLaunchVfxInstance : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] float lifetime = 0.18f;
        [SerializeField] bool fadeOut = true;
        [SerializeField] Vector2 scaleRange = new Vector2(0.75f, 1.08f);

        SpriteRenderer spriteRenderer;
        Color initialColor;
        float age;

        public void Configure(float nextLifetime, bool nextFadeOut, Vector2 nextScaleRange)
        {
            lifetime = Mathf.Max(0.01f, nextLifetime);
            fadeOut = nextFadeOut;
            scaleRange = nextScaleRange;
        }

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer) initialColor = spriteRenderer.color;
        }

        void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / lifetime);
            var scale = Mathf.Lerp(scaleRange.x, scaleRange.y, t);
            transform.localScale = Vector3.one * scale;
            if (fadeOut && spriteRenderer)
            {
                var color = initialColor;
                color.a *= 1f - t;
                spriteRenderer.color = color;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
