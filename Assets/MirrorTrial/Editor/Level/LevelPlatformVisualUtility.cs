using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    public static class LevelPlatformVisualUtility
    {
        public const int PlatformSpriteCount = 13;

        const string PlatformSpriteFolder = "Assets/Art/pingtai/slices";
        const string PlatformVisualName = "PlatformVisual";

        public static Sprite GetPlatformSprite(int index)
        {
            if (index < 1 || index > PlatformSpriteCount) return null;

            var path = GetPlatformSpritePath(index);
            EnsureSpriteImportSettings(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static IReadOnlyList<Sprite> GetPlatformSprites()
        {
            var sprites = new List<Sprite>(PlatformSpriteCount);
            for (var i = 1; i <= PlatformSpriteCount; i++)
            {
                var sprite = GetPlatformSprite(i);
                if (sprite) sprites.Add(sprite);
            }
            return sprites;
        }

        public static bool ApplyPlatformVisual(GameObject platform, int spriteIndex)
        {
            return ApplyPlatformVisual(platform, GetPlatformSprite(spriteIndex));
        }

        public static bool ApplyPlatformVisual(GameObject platform, Sprite visualSprite)
        {
            if (!platform || !visualSprite) return false;

            var size = GetPlatformSize(platform);
            var visual = GetOrCreateVisual(platform.transform);
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (!renderer) renderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);

            Undo.RecordObject(visual, "Apply Platform Visual");
            Undo.RecordObject(renderer, "Apply Platform Visual");

            renderer.sprite = visualSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 1;
            renderer.drawMode = SpriteDrawMode.Simple;

            var spriteSize = visualSprite.bounds.size;
            if (spriteSize.x > 0f && spriteSize.y > 0f)
            {
                var scale = size.x / spriteSize.x;
                visual.localScale = new Vector3(scale, scale, 1f);
                var visualHeight = spriteSize.y * scale;
                visual.localPosition = new Vector3(0f, size.y * 0.5f - visualHeight * 0.5f, -0.01f);
            }

            EditorUtility.SetDirty(platform);
            EditorUtility.SetDirty(visual.gameObject);
            return true;
        }

        public static bool RemovePlatformVisual(GameObject platform)
        {
            if (!platform) return false;
            var visual = platform.transform.Find(PlatformVisualName);
            if (!visual) return false;
            Undo.DestroyObjectImmediate(visual.gameObject);
            EditorUtility.SetDirty(platform);
            return true;
        }

        public static bool LooksLikePlatform(GameObject go)
        {
            if (!go) return false;
            if (go.name.StartsWith("Platform_")) return true;
            return go.GetComponent<BoxCollider2D>() && go.GetComponent<SpriteRenderer>();
        }

        static string GetPlatformSpritePath(int index)
        {
            return $"{PlatformSpriteFolder}/pingtai_{index:00}.png";
        }

        static void EnsureSpriteImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return;

            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (importer.spritePixelsPerUnit != 100f)
            {
                importer.spritePixelsPerUnit = 100f;
                changed = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        static Transform GetOrCreateVisual(Transform platform)
        {
            var visual = platform.Find(PlatformVisualName);
            if (visual) return visual;

            var go = new GameObject(PlatformVisualName);
            Undo.RegisterCreatedObjectUndo(go, "Create Platform Visual");
            go.transform.SetParent(platform, false);
            return go.transform;
        }

        static Vector2 GetPlatformSize(GameObject platform)
        {
            var collider = platform.GetComponent<BoxCollider2D>();
            if (collider) return collider.size;

            var renderer = platform.GetComponent<SpriteRenderer>();
            if (renderer && renderer.drawMode != SpriteDrawMode.Simple) return renderer.size;
            if (renderer && renderer.sprite) return renderer.sprite.bounds.size;

            return Vector2.one;
        }
    }
}
