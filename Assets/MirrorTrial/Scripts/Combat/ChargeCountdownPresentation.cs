using UnityEngine;

namespace MirrorTrial.Combat
{
    public sealed class ChargeCountdownPresentation : MonoBehaviour
    {
        [SerializeField, Range(8, 32)] int segmentCount = 16;
        [SerializeField, Min(0.1f)] float radius = 0.55f;
        [SerializeField] Vector2 segmentScale = new Vector2(0.11f, 0.035f);
        [SerializeField] Color chargingColor = new Color(0.25f, 0.85f, 1f, 0.95f);
        [SerializeField] Color countdownColor = new Color(1f, 0.72f, 0.18f, 1f);
        [SerializeField] Color emptyColor = new Color(0.08f, 0.12f, 0.16f, 0.38f);
        [SerializeField] int sortingOrder = 120;

        SpriteRenderer[] segments;
        Texture2D runtimeTexture;
        Sprite runtimeSprite;
        Vector2 localOffset;
        bool active;

        public static ChargeCountdownPresentation Ensure(GameObject owner)
        {
            if (!owner) return null;
            var child = owner.transform.Find("ChargeCountdownUI");
            if (!child)
            {
                var childObject = new GameObject("ChargeCountdownUI");
                childObject.transform.SetParent(owner.transform, false);
                child = childObject.transform;
            }
            var presentation = child.GetComponent<ChargeCountdownPresentation>();
            if (!presentation) presentation = child.gameObject.AddComponent<ChargeCountdownPresentation>();
            return presentation;
        }

        public void Begin(Vector2 offset, bool facingRight)
        {
            localOffset = offset;
            active = true;
            EnsureVisuals();
            SetProgress(0f, false, facingRight);
        }

        public void SetProgress(float normalized, bool countingDown, bool facingRight)
        {
            if (!active) return;
            EnsureVisuals();
            transform.localPosition = new Vector3(facingRight ? localOffset.x : -localOffset.x, localOffset.y, 0f);
            var filled = Mathf.CeilToInt(Mathf.Clamp01(normalized) * segments.Length);
            var filledColor = countingDown ? countdownColor : chargingColor;
            for (var i = 0; i < segments.Length; i++)
            {
                if (!segments[i]) continue;
                segments[i].enabled = true;
                segments[i].color = i < filled ? filledColor : emptyColor;
            }
        }

        public void End()
        {
            active = false;
            if (segments == null) return;
            foreach (var segment in segments)
                if (segment) segment.enabled = false;
        }

        void Awake()
        {
            EnsureVisuals();
            End();
        }

        void EnsureVisuals()
        {
            var count = Mathf.Clamp(segmentCount, 8, 32);
            if (segments != null && segments.Length == count) return;

            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "ChargeCountdownPixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            runtimeTexture.SetPixel(0, 0, Color.white);
            runtimeTexture.Apply();
            runtimeSprite = Sprite.Create(runtimeTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 16f);
            runtimeSprite.name = "ChargeCountdownSegment";

            segments = new SpriteRenderer[count];
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.PI * 2f * i / count + Mathf.PI * 0.5f;
                var segmentObject = new GameObject("Segment_" + (i + 1));
                segmentObject.transform.SetParent(transform, false);
                segmentObject.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
                segmentObject.transform.localEulerAngles = new Vector3(0f, 0f, angle * Mathf.Rad2Deg + 90f);
                segmentObject.transform.localScale = new Vector3(segmentScale.x, segmentScale.y, 1f);
                var renderer = segmentObject.AddComponent<SpriteRenderer>();
                renderer.sprite = runtimeSprite;
                renderer.sortingOrder = sortingOrder;
                segments[i] = renderer;
            }
        }

        void OnDestroy()
        {
            if (runtimeSprite) Destroy(runtimeSprite);
            if (runtimeTexture) Destroy(runtimeTexture);
        }
    }
}
