using System.Collections.Generic;
using MirrorTrial.Feedback;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    /// <summary>
    /// Controller for the authored EnemyDeathBurst prefab. The prefab is the shared effect
    /// entry point; each instance builds debris from the enemy's currently displayed sprite.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EnemyDeathBurstEffect : MonoBehaviour
    {
        public const float DefaultLifetime = 0.56f;
        const string ResourcePath = "Effects/EnemyDeathBurst";
        const float BreakDelay = 0.035f;
        const float MaxWorldFragmentSize = 0.16f;
        const float FragmentShrinkDuration = 0.065f;

        static EnemyDeathBurstEffect cachedPrefab;

        [SerializeField] MeshFilter fragmentMeshFilter;
        [SerializeField] MeshRenderer fragmentMeshRenderer;
        [SerializeField] SpriteRenderer impactFlash;
        [SerializeField, Range(3, 12)] int fragmentColumns = 7;
        [SerializeField, Range(3, 10)] int fragmentRows = 5;
        [SerializeField, Min(0.05f)] float lifetime = DefaultLifetime;
        [SerializeField, Min(1f)] float burstSizeMultiplier = 1.5f;
        [SerializeField] Vector2 angularSpeed = new Vector2(100f, 310f);
        [SerializeField, Range(0f, 1f)] float cameraShakePower = 0.14f;
        [SerializeField, Min(0f)] float hitStopDuration = 0.03f;

        readonly List<Fragment> fragments = new List<Fragment>();
        Mesh fragmentMesh;
        Material fragmentMaterial;
        Vector3[] vertices;
        Color32[] colors;
        Color32 sourceColor;
        Vector2 inverseEffectScale = Vector2.one;
        Vector2 sourceVisualCenter;
        float maxFlashWorldScale = 1f;
        float age;
        bool playing;

        struct Fragment
        {
            public Vector2 center;
            public Vector2 halfSize;
            public Vector2 uvMin;
            public Vector2 uvMax;
            public Vector2 expansionOffset;
            public float targetScale;
            public float angularVelocity;
            public float delay;
        }

        public static bool TrySpawn(SpriteRenderer sourceRenderer)
        {
            if (!sourceRenderer || !sourceRenderer.sprite) return false;
            if (!cachedPrefab)
                cachedPrefab = Resources.Load<EnemyDeathBurstEffect>(ResourcePath);
            if (!cachedPrefab) return false;

            var instance = Instantiate(cachedPrefab, sourceRenderer.transform.position,
                sourceRenderer.transform.rotation);
            instance.transform.localScale = sourceRenderer.transform.lossyScale;
            if (!instance.Play(sourceRenderer))
            {
                Destroy(instance.gameObject);
                return false;
            }
            return true;
        }

        public void Configure(MeshFilter meshFilter, MeshRenderer meshRenderer, SpriteRenderer flash,
            int columns, int rows, float duration, float sizeMultiplier, Vector2 spin,
            float shakePower, float hitStop)
        {
            fragmentMeshFilter = meshFilter;
            fragmentMeshRenderer = meshRenderer;
            impactFlash = flash;
            fragmentColumns = Mathf.Clamp(columns, 3, 12);
            fragmentRows = Mathf.Clamp(rows, 3, 10);
            lifetime = Mathf.Max(0.05f, duration);
            burstSizeMultiplier = Mathf.Max(1f, sizeMultiplier);
            angularSpeed = new Vector2(Mathf.Max(0f, spin.x), Mathf.Max(spin.x, spin.y));
            cameraShakePower = Mathf.Clamp01(shakePower);
            hitStopDuration = Mathf.Max(0f, hitStop);
        }

        bool Play(SpriteRenderer sourceRenderer)
        {
            if (!BuildFragments(sourceRenderer)) return false;

            age = 0f;
            playing = true;
            if (impactFlash)
            {
                impactFlash.transform.localPosition = sourceVisualCenter;
                impactFlash.color = Color.white;
                impactFlash.sortingLayerID = sourceRenderer.sortingLayerID;
                impactFlash.sortingOrder = sourceRenderer.sortingOrder + 3;
                SetFlashScale(0.24f);
            }

            if (hitStopDuration > 0f)
                HitStopService.Request(hitStopDuration);
            if (cameraShakePower > 0f)
                CameraShakeService.Shake(Vector2.up, cameraShakePower);
            return true;
        }

        bool BuildFragments(SpriteRenderer sourceRenderer)
        {
            var sprite = sourceRenderer.sprite;
            var texture = sprite.texture;
            if (!texture) return false;

            if (!fragmentMeshFilter) fragmentMeshFilter = GetComponent<MeshFilter>();
            if (!fragmentMeshRenderer) fragmentMeshRenderer = GetComponent<MeshRenderer>();
            if (!fragmentMeshFilter || !fragmentMeshRenderer) return false;

            fragmentMesh = new Mesh { name = $"{sprite.name} Death Fragments" };
            fragmentMesh.MarkDynamic();
            fragmentMeshFilter.sharedMesh = fragmentMesh;

            var shader = Shader.Find("Sprites/Default");
            if (!shader) return false;
            fragmentMaterial = new Material(shader)
            {
                name = $"{sprite.name} Death Fragment Material",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture
            };
            fragmentMeshRenderer.sharedMaterial = fragmentMaterial;
            fragmentMeshRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            fragmentMeshRenderer.sortingOrder = sourceRenderer.sortingOrder + 2;

            var spriteRect = sprite.rect;
            var pixelsPerUnit = Mathf.Max(1f, sprite.pixelsPerUnit);
            var random = new System.Random(sprite.GetInstanceID() ^ Time.frameCount);
            var spriteVertices = sprite.vertices;
            var spriteTriangles = sprite.triangles;
            if (!TryGetVisibleBounds(spriteVertices, out var nativeVisibleMin, out var nativeVisibleMax))
                return false;

            var nativeVisibleSize = nativeVisibleMax - nativeVisibleMin;
            var renderedVisibleMin = nativeVisibleMin;
            var renderedVisibleMax = nativeVisibleMax;
            if (sourceRenderer.drawMode != SpriteDrawMode.Simple)
            {
                var rendererSize = sourceRenderer.size;
                var normalizedPivot = new Vector2(
                    sprite.pivot.x / Mathf.Max(1f, spriteRect.width),
                    sprite.pivot.y / Mathf.Max(1f, spriteRect.height));
                renderedVisibleMin = -Vector2.Scale(normalizedPivot, rendererSize);
                renderedVisibleMax = renderedVisibleMin + rendererSize;
            }
            var renderedVisibleSize = renderedVisibleMax - renderedVisibleMin;
            if (renderedVisibleSize.x <= 0.0001f || renderedVisibleSize.y <= 0.0001f)
                return false;

            var effectScale = transform.lossyScale;
            var nativeCellSize = new Vector2(
                nativeVisibleSize.x / fragmentColumns,
                nativeVisibleSize.y / fragmentRows);
            var renderedVisibleCenter = (renderedVisibleMin + renderedVisibleMax) * 0.5f;
            sourceVisualCenter = renderedVisibleCenter;
            if (sourceRenderer.flipX) sourceVisualCenter.x = -sourceVisualCenter.x;
            if (sourceRenderer.flipY) sourceVisualCenter.y = -sourceVisualCenter.y;
            inverseEffectScale = new Vector2(
                1f / Mathf.Max(0.0001f, Mathf.Abs(effectScale.x)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(effectScale.y)));
            var absoluteEffectScale = new Vector2(Mathf.Abs(effectScale.x), Mathf.Abs(effectScale.y));
            if (impactFlash && impactFlash.sprite)
            {
                var targetWorldSize = Vector2.Scale(renderedVisibleSize, absoluteEffectScale) * burstSizeMultiplier;
                var flashSize = impactFlash.sprite.bounds.size;
                maxFlashWorldScale = Mathf.Min(
                    targetWorldSize.x / Mathf.Max(0.0001f, flashSize.x),
                    targetWorldSize.y / Mathf.Max(0.0001f, flashSize.y));
            }

            fragments.Clear();
            for (var row = 0; row < fragmentRows; row++)
            for (var column = 0; column < fragmentColumns; column++)
            {
                var nativeMin = nativeVisibleMin + Vector2.Scale(new Vector2(column, row), nativeCellSize);
                var nativeMax = nativeMin + nativeCellSize;
                if (!CellTouchesSprite(nativeMin, nativeMax, spriteVertices, spriteTriangles)) continue;

                var normalizedMin = new Vector2(
                    (nativeMin.x - nativeVisibleMin.x) / nativeVisibleSize.x,
                    (nativeMin.y - nativeVisibleMin.y) / nativeVisibleSize.y);
                var normalizedMax = new Vector2(
                    (nativeMax.x - nativeVisibleMin.x) / nativeVisibleSize.x,
                    (nativeMax.y - nativeVisibleMin.y) / nativeVisibleSize.y);
                var renderedMin = renderedVisibleMin + Vector2.Scale(normalizedMin, renderedVisibleSize);
                var renderedMax = renderedVisibleMin + Vector2.Scale(normalizedMax, renderedVisibleSize);

                var sourceCenter = (renderedMin + renderedMax) * 0.5f;
                var center = sourceCenter;
                var fromCenter = sourceCenter - renderedVisibleCenter;
                var halfSize = (renderedMax - renderedMin) * 0.5f;
                if (sourceRenderer.flipX) center.x = -center.x;
                if (sourceRenderer.flipY) center.y = -center.y;
                if (sourceRenderer.flipX) fromCenter.x = -fromCenter.x;
                if (sourceRenderer.flipY) fromCenter.y = -fromCenter.y;
                var worldFragmentSize = Vector2.Scale(renderedMax - renderedMin, absoluteEffectScale);
                var largestWorldSide = Mathf.Max(worldFragmentSize.x, worldFragmentSize.y);
                var targetScale = Mathf.Min(1f, MaxWorldFragmentSize / Mathf.Max(0.0001f, largestWorldSide));
                var targetHalfSize = halfSize * targetScale;
                var targetExtent = renderedVisibleSize * (burstSizeMultiplier * 0.5f);
                var targetOffset = fromCenter * burstSizeMultiplier;
                targetOffset.x = Mathf.Clamp(targetOffset.x,
                    -targetExtent.x + targetHalfSize.x, targetExtent.x - targetHalfSize.x);
                targetOffset.y = Mathf.Clamp(targetOffset.y,
                    -targetExtent.y + targetHalfSize.y, targetExtent.y - targetHalfSize.y);
                var expansionStrength = Mathf.Lerp(0.78f, 1f, NextFloat(random));
                var expansionOffset = (targetOffset - fromCenter) * expansionStrength;

                var pixelMin = nativeMin * pixelsPerUnit + sprite.pivot;
                var pixelMax = nativeMax * pixelsPerUnit + sprite.pivot;
                var uvMin = new Vector2(
                    (spriteRect.xMin + pixelMin.x) / texture.width,
                    (spriteRect.yMin + pixelMin.y) / texture.height);
                var uvMax = new Vector2(
                    (spriteRect.xMin + pixelMax.x) / texture.width,
                    (spriteRect.yMin + pixelMax.y) / texture.height);

                fragments.Add(new Fragment
                {
                    center = center,
                    halfSize = halfSize,
                    uvMin = uvMin,
                    uvMax = uvMax,
                    expansionOffset = expansionOffset,
                    targetScale = targetScale,
                    angularVelocity = Mathf.Lerp(angularSpeed.x, angularSpeed.y, NextFloat(random)) *
                                      (random.Next(0, 2) == 0 ? -1f : 1f),
                    delay = BreakDelay + NextFloat(random) * 0.035f
                });
            }

            if (fragments.Count == 0) return false;

            vertices = new Vector3[fragments.Count * 4];
            var uvs = new Vector2[fragments.Count * 4];
            colors = new Color32[fragments.Count * 4];
            var triangles = new int[fragments.Count * 6];
            sourceColor = sourceRenderer.color;

            for (var i = 0; i < fragments.Count; i++)
            {
                var fragment = fragments[i];

                SetFragmentVertices(fragment, fragment.center, 0f, 1f, i);
                var vertex = i * 4;
                uvs[vertex] = new Vector2(fragment.uvMin.x, fragment.uvMin.y);
                uvs[vertex + 1] = new Vector2(fragment.uvMax.x, fragment.uvMin.y);
                uvs[vertex + 2] = new Vector2(fragment.uvMax.x, fragment.uvMax.y);
                uvs[vertex + 3] = new Vector2(fragment.uvMin.x, fragment.uvMax.y);
                if (sourceRenderer.flipX)
                    Swap(ref uvs[vertex], ref uvs[vertex + 1], ref uvs[vertex + 2], ref uvs[vertex + 3], true);
                if (sourceRenderer.flipY)
                    Swap(ref uvs[vertex], ref uvs[vertex + 1], ref uvs[vertex + 2], ref uvs[vertex + 3], false);

                colors[vertex] = colors[vertex + 1] = colors[vertex + 2] = colors[vertex + 3] = sourceColor;
                var triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            fragmentMesh.vertices = vertices;
            fragmentMesh.uv = uvs;
            fragmentMesh.colors32 = colors;
            fragmentMesh.triangles = triangles;
            fragmentMesh.RecalculateBounds();
            return true;
        }

        void Update()
        {
            if (!playing) return;

            age += Time.unscaledDeltaTime;
            var normalized = Mathf.Clamp01(age / Mathf.Max(0.05f, lifetime));
            var alpha = (byte)Mathf.RoundToInt(255f *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, normalized))));

            for (var i = 0; i < fragments.Count; i++)
            {
                var fragment = fragments[i];
                var moveAge = Mathf.Max(0f, age - fragment.delay);
                var expansionDuration = Mathf.Max(0.05f, lifetime - fragment.delay);
                var expansionT = EaseOut(Mathf.Clamp01(moveAge / expansionDuration));
                var center = fragment.center + fragment.expansionOffset * expansionT;
                var shrinkT = EaseOut(Mathf.Clamp01(moveAge / FragmentShrinkDuration));
                var fragmentScale = Mathf.Lerp(1f, fragment.targetScale, shrinkT);
                SetFragmentVertices(fragment, center, fragment.angularVelocity * moveAge, fragmentScale, i);
                var vertex = i * 4;
                var color = sourceColor;
                color.a = (byte)(sourceColor.a * alpha / 255);
                colors[vertex] = colors[vertex + 1] = colors[vertex + 2] = colors[vertex + 3] = color;
            }

            fragmentMesh.vertices = vertices;
            fragmentMesh.colors32 = colors;
            fragmentMesh.RecalculateBounds();
            AnimateFlash(normalized);

            if (normalized >= 1f)
                Destroy(gameObject);
        }

        void SetFragmentVertices(Fragment fragment, Vector2 center, float rotationDegrees, float scale, int index)
        {
            var radians = rotationDegrees * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            var vertex = index * 4;
            var halfSize = fragment.halfSize * scale;
            vertices[vertex] = center + Rotate(new Vector2(-halfSize.x, -halfSize.y), cosine, sine);
            vertices[vertex + 1] = center + Rotate(new Vector2(halfSize.x, -halfSize.y), cosine, sine);
            vertices[vertex + 2] = center + Rotate(new Vector2(halfSize.x, halfSize.y), cosine, sine);
            vertices[vertex + 3] = center + Rotate(new Vector2(-halfSize.x, halfSize.y), cosine, sine);
        }

        void AnimateFlash(float normalized)
        {
            if (!impactFlash) return;
            var flashT = Mathf.Clamp01(normalized * 4.8f);
            SetFlashScale(Mathf.Lerp(0.24f, 1f, EaseOut(flashT)));
            var color = Color.white;
            color.a = 1f - flashT;
            impactFlash.color = color;
        }

        void SetFlashScale(float normalizedScale)
        {
            var worldScale = maxFlashWorldScale * normalizedScale;
            impactFlash.transform.localScale = new Vector3(
                worldScale * inverseEffectScale.x,
                worldScale * inverseEffectScale.y,
                1f);
        }

        void OnDestroy()
        {
            if (fragmentMesh) Destroy(fragmentMesh);
            if (fragmentMaterial) Destroy(fragmentMaterial);
        }

        static bool CellTouchesSprite(Vector2 min, Vector2 max, Vector2[] spriteVertices, ushort[] triangles)
        {
            var center = (min + max) * 0.5f;
            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = spriteVertices[triangles[i]];
                var b = spriteVertices[triangles[i + 1]];
                var c = spriteVertices[triangles[i + 2]];
                if (PointInTriangle(center, a, b, c) ||
                    PointInTriangle(new Vector2(min.x, min.y), a, b, c) ||
                    PointInTriangle(new Vector2(max.x, min.y), a, b, c) ||
                    PointInTriangle(new Vector2(max.x, max.y), a, b, c) ||
                    PointInTriangle(new Vector2(min.x, max.y), a, b, c) ||
                    PointInRect(a, min, max) || PointInRect(b, min, max) || PointInRect(c, min, max))
                    return true;
            }
            return false;
        }

        static bool TryGetVisibleBounds(Vector2[] spriteVertices, out Vector2 min, out Vector2 max)
        {
            min = Vector2.zero;
            max = Vector2.zero;
            if (spriteVertices == null || spriteVertices.Length == 0) return false;

            min = spriteVertices[0];
            max = spriteVertices[0];
            for (var i = 1; i < spriteVertices.Length; i++)
            {
                min = Vector2.Min(min, spriteVertices[i]);
                max = Vector2.Max(max, spriteVertices[i]);
            }

            return max.x - min.x > 0.0001f && max.y - min.y > 0.0001f;
        }

        static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Cross(point - b, a - b);
            var d2 = Cross(point - c, b - c);
            var d3 = Cross(point - a, c - a);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool PointInRect(Vector2 point, Vector2 min, Vector2 max) =>
            point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;

        static void Swap(ref Vector2 bottomLeft, ref Vector2 bottomRight, ref Vector2 topRight,
            ref Vector2 topLeft, bool horizontal)
        {
            if (horizontal)
            {
                var temp = bottomLeft;
                bottomLeft = bottomRight;
                bottomRight = temp;
                temp = topLeft;
                topLeft = topRight;
                topRight = temp;
            }
            else
            {
                var temp = bottomLeft;
                bottomLeft = topLeft;
                topLeft = temp;
                temp = bottomRight;
                bottomRight = topRight;
                topRight = temp;
            }
        }

        static Vector2 Rotate(Vector2 point, float cosine, float sine) =>
            new Vector2(point.x * cosine - point.y * sine, point.x * sine + point.y * cosine);

        static float NextFloat(System.Random random) => (float)random.NextDouble();

        static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
