using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Presentation-only landing warning for the ground-phase rockfall. The wine-red
    /// decorated ripple frames are copied verbatim from <see cref="MirrorArcherAttackTelegraph"/>
    /// so the two attacks read as the same visual language. This component owns NO combat
    /// timing or damage: it only draws a ripple at a fixed world point and ramps its
    /// intensity toward release. The rock projectile drives the timing and the hit.
    /// </summary>
    public sealed class MirrorRockfallTelegraph : MonoBehaviour
    {
        const int RippleFrameCount = 6;
        const int RippleCanvasWidth = 33;
        const int RippleCanvasHeight = 17;
        const int RippleLayers = 2;

        static readonly int[] RippleRadiusX = { 4, 6, 8, 10, 12, 15 };
        static readonly int[] RippleRadiusY = { 2, 3, 4, 5, 6, 7 };
        static readonly Color RippleColor = new Color(0.78f, 0.27f, 0.24f, 1f);

        static Sprite[] rippleFrames;

        readonly SpriteRenderer[] ripples = new SpriteRenderer[RippleLayers];

        float radiusScale = 1f;
        float progress;

        /// <summary>
        /// Spawns a landing telegraph at <paramref name="groundPoint"/>. The ripple scale is
        /// matched to the impact <paramref name="effectRadius"/>. Call <see cref="SetProgress"/>
        /// each frame with the normalized windup progress, then <see cref="Cancel"/> when done.
        /// </summary>
        public static MirrorRockfallTelegraph Create(Vector2 groundPoint, float effectRadius, int sortingOrder = 84)
        {
            var go = new GameObject("MirrorRockfall_Telegraph");
            go.transform.position = groundPoint + Vector2.up * 0.035f;
            var telegraph = go.AddComponent<MirrorRockfallTelegraph>();
            telegraph.Initialize(effectRadius, sortingOrder);
            return telegraph;
        }

        void Initialize(float effectRadius, int sortingOrder)
        {
            // The archer ripple sprite is authored around a ~0.8u footprint at 32 PPU,
            // so scale it to the requested effect radius for a readable danger zone.
            radiusScale = Mathf.Max(0.4f, effectRadius / 0.8f);
            EnsureRuntimeSprites();
            for (var layer = 0; layer < RippleLayers; layer++)
            {
                var go = new GameObject("Ripple_" + layer);
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = rippleFrames[0];
                renderer.sortingOrder = sortingOrder + layer;
                renderer.color = RippleColor;
                ripples[layer] = renderer;
            }
        }

        public void SetProgress(float normalized)
        {
            progress = Mathf.Clamp01(normalized);
        }

        public void Cancel()
        {
            if (this) Destroy(gameObject);
        }

        void Update()
        {
            var period = Mathf.Lerp(0.78f, 0.42f, progress);
            var progressAlpha = Mathf.Lerp(0.82f, 1.12f, progress);
            for (var layer = 0; layer < RippleLayers; layer++)
            {
                var renderer = ripples[layer];
                if (!renderer) continue;
                var phase = Mathf.Repeat(Time.unscaledTime / period + layer * 0.5f, 1f);
                var frame = Mathf.Min(RippleFrameCount - 1, Mathf.FloorToInt(phase * RippleFrameCount));
                var envelope = Mathf.Sin(phase * Mathf.PI);
                var alpha = Mathf.Lerp(0.24f, 0.6f, envelope) * progressAlpha;
                if (layer == 1) alpha *= 0.78f;
                renderer.sprite = rippleFrames[frame];
                renderer.color = new Color(RippleColor.r, RippleColor.g, RippleColor.b, alpha);
                renderer.transform.localScale = Vector3.one * radiusScale;
            }
        }

        // ----- runtime sprite authoring (verbatim ripple generator from the archer telegraph) -----

        static void EnsureRuntimeSprites()
        {
            if (rippleFrames != null && rippleFrames.Length == RippleFrameCount) return;
            rippleFrames = new Sprite[RippleFrameCount];
            for (var frame = 0; frame < RippleFrameCount; frame++)
                rippleFrames[frame] = CreateRippleFrame(frame);
        }

        static Sprite CreateRippleFrame(int frame)
        {
            var radiusX = RippleRadiusX[frame];
            var radiusY = RippleRadiusY[frame];
            var centerX = RippleCanvasWidth / 2;
            var centerY = RippleCanvasHeight / 2;
            var texture = new Texture2D(RippleCanvasWidth, RippleCanvasHeight, TextureFormat.RGBA32, false)
            {
                name = "MirrorRockfall_Ripple_" + frame + "_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[RippleCanvasWidth * RippleCanvasHeight];
            for (var y = 0; y < RippleCanvasHeight; y++)
            for (var x = 0; x < RippleCanvasWidth; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                var ringPixel = IsEllipseOutline(dx, dy, radiusX, radiusY) &&
                                !IsCardinalGap(dx, dy, radiusX, radiusY);
                var shardPixel = IsShardPixel(dx, dy, radiusX, radiusY, frame == 0);
                pixels[y * RippleCanvasWidth + x] = (shardPixel || ringPixel)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture,
                new Rect(0f, 0f, RippleCanvasWidth, RippleCanvasHeight),
                new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            sprite.name = "MirrorRockfall_Ripple_" + frame;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static bool IsEllipseOutline(int x, int y, int radiusX, int radiusY)
        {
            var outerX = x / (float)radiusX;
            var outerY = y / (float)radiusY;
            if (outerX * outerX + outerY * outerY > 1f) return false;
            var innerRadiusX = Mathf.Max(1f, radiusX - 1.15f);
            var innerRadiusY = Mathf.Max(0.6f, radiusY - 1.05f);
            var innerX = x / innerRadiusX;
            var innerY = y / innerRadiusY;
            return innerX * innerX + innerY * innerY >= 1f;
        }

        static bool IsCardinalGap(int x, int y, int radiusX, int radiusY)
        {
            var horizontalGap = Mathf.Abs(y) <= 1 && Mathf.Abs(Mathf.Abs(x) - radiusX) <= 1;
            var verticalGap = Mathf.Abs(x) <= 1 && Mathf.Abs(Mathf.Abs(y) - radiusY) <= 1;
            return horizontalGap || verticalGap;
        }

        static bool IsShardPixel(int x, int y, int radiusX, int radiusY, bool compact)
        {
            var size = compact ? 0 : 1;
            return ManhattanDistance(x, y, radiusX, 0) <= size ||
                   ManhattanDistance(x, y, -radiusX, 0) <= size ||
                   ManhattanDistance(x, y, 0, radiusY) <= size ||
                   ManhattanDistance(x, y, 0, -radiusY) <= size;
        }

        static int ManhattanDistance(int x, int y, int targetX, int targetY)
        {
            return Mathf.Abs(x - targetX) + Mathf.Abs(y - targetY);
        }
    }
}
