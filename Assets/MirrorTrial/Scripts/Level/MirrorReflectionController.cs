using UnityEngine;

namespace MirrorTrial.Level
{
    [DisallowMultipleComponent]
    public sealed class MirrorReflectionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] SpriteRenderer reflectionRenderer;
        [SerializeField] SpriteRenderer mirrorIntactRenderer;

        [Header("Visibility")]
        [SerializeField, Min(0.1f)] float visibleDistance = 6f;
        [SerializeField, Min(0.1f)] float fullVisibilityDistance = 2.5f;
        [SerializeField, Range(0f, 1f)] float maximumAlpha = 0.68f;
        [SerializeField, Min(0.01f)] float fadeSpeed = 5f;

        [Header("Reflection Look")]
        [SerializeField] Color reflectionTint = new Color(0.46f, 0.55f, 0.70f, 1f);
        [SerializeField, Range(0.1f, 1.5f)] float reflectionScale = 0.62f;
        [SerializeField, Range(0f, 1f)] float horizontalResponse = 0.12f;
        [SerializeField, Range(0f, 1f)] float verticalResponse = 0.34f;
        [SerializeField] Vector2 centerOffset = new Vector2(-0.02f, -0.12f);

        Transform player;
        SpriteRenderer playerRenderer;
        float currentAlpha;
        float nextResolveTime;

        void Awake()
        {
            ResolvePlayer();
            ApplyAlpha(0f);
        }

        void LateUpdate()
        {
            if (!reflectionRenderer || !mirrorIntactRenderer)
                return;

            if ((!player || !playerRenderer) && Time.unscaledTime >= nextResolveTime)
                ResolvePlayer();

            float targetAlpha = 0f;
            if (player && playerRenderer && mirrorIntactRenderer.enabled && playerRenderer.enabled)
            {
                float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
                float proximity = 1f - Mathf.InverseLerp(fullVisibilityDistance, visibleDistance, horizontalDistance);
                targetAlpha = Mathf.Clamp01(proximity) * maximumAlpha;

                SyncSprite();
                UpdateReflectionTransform();
            }

            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            ApplyAlpha(currentAlpha);
        }

        void ResolvePlayer()
        {
            nextResolveTime = Time.unscaledTime + 0.5f;
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (!playerObject) return;

            player = playerObject.transform;
            Transform visual = player.Find("Visual");
            playerRenderer = visual ? visual.GetComponent<SpriteRenderer>() : player.GetComponentInChildren<SpriteRenderer>();
        }

        void SyncSprite()
        {
            reflectionRenderer.sprite = playerRenderer.sprite;
            reflectionRenderer.flipX = !playerRenderer.flipX;
            reflectionRenderer.flipY = playerRenderer.flipY;
        }

        void UpdateReflectionTransform()
        {
            Bounds glassBounds = mirrorIntactRenderer.bounds;
            Vector3 playerDelta = player.position - transform.position;

            float xLimit = glassBounds.extents.x * 0.38f;
            float yLimit = glassBounds.extents.y * 0.30f;
            float xOffset = Mathf.Clamp(playerDelta.x * horizontalResponse, -xLimit, xLimit);
            float yOffset = Mathf.Clamp(playerDelta.y * verticalResponse, -yLimit, yLimit);

            Vector3 target = glassBounds.center;
            target.x += xOffset + centerOffset.x;
            target.y += yOffset + centerOffset.y;
            target.z = reflectionRenderer.transform.position.z;
            reflectionRenderer.transform.position = target;

            Vector3 sourceScale = playerRenderer.transform.lossyScale;
            Vector3 parentScale = reflectionRenderer.transform.parent
                ? reflectionRenderer.transform.parent.lossyScale
                : Vector3.one;
            reflectionRenderer.transform.localScale = new Vector3(
                SafeScale(sourceScale.x, parentScale.x) * reflectionScale,
                SafeScale(sourceScale.y, parentScale.y) * reflectionScale,
                1f);
        }

        static float SafeScale(float source, float parent)
        {
            return Mathf.Abs(parent) > 0.0001f ? source / parent : source;
        }

        void ApplyAlpha(float alpha)
        {
            if (!reflectionRenderer) return;
            Color color = reflectionTint;
            color.a *= alpha;
            reflectionRenderer.color = color;
            reflectionRenderer.enabled = alpha > 0.001f;
        }
    }
}
