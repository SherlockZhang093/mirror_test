#if UNITY_EDITOR
using System.Linq;
using MirrorTrial.Level;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    public static class Level02JungleMirrorGateBuilder
    {
        public const string PrefabPath = "Assets/MirrorTrial/Prefabs/Level/MirrorGate_Level02_Jungle.prefab";
        const string FramePath = "Assets/MirrorTrial/Art/Level02_Jungle/MirrorGate/level02_jungle_mirror_frame.png";
        const string SurfacePath = "Assets/MirrorTrial/Art/Level02_Jungle/MirrorGate/level02_jungle_mirror_surface.png";
        const string GlassMaterialPath = "Assets/MirrorTrial/Materials/MirrorGlass2D.mat";
        const string ReflectionMaterialPath = "Assets/MirrorTrial/Materials/MirrorPlanePerspective2D.mat";
        const string Crack1Path = "Assets/Art/mirror/broken1.png";
        const string Crack2Path = "Assets/Art/mirror/broken2.png";
        const string Crack3Path = "Assets/Art/mirror/broken3.png";
        const string ShardPath = "Assets/Art/mirror/suipian.png";

        [MenuItem("Mirror Trial/Level/Build Level 02 Jungle Mirror Gate")]
        public static void BuildAndConnect()
        {
            BuildPrefab();
            LevelMirror02TrialSetup.Build();
        }

        public static GameObject BuildPrefab()
        {
            ConfigureSprite(FramePath, 100f);
            ConfigureSprite(SurfacePath, 100f);
            AssetDatabase.ImportAsset(FramePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SurfacePath, ImportAssetOptions.ForceUpdate);

            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
            var surfaceSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SurfacePath);
            if (!frameSprite || !surfaceSprite)
            {
                Debug.LogError("[Level02JungleMirrorGateBuilder] Missing generated frame or surface sprite.");
                return null;
            }

            var root = new GameObject("MirrorGate_Level02_Jungle");
            var frame = CreateRenderer(root.transform, "Frame", frameSprite, null, 5, Color.white);
            var mirror = CreateRenderer(root.transform, "Mirror", surfaceSprite,
                AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath), 2,
                new Color(0.42f, 0.94f, 0.88f, 0.72f));
            var cracks = CreateRenderer(root.transform, "Cracks",
                AssetDatabase.LoadAssetAtPath<Sprite>(Crack1Path), null, 4,
                new Color(0.35f, 1f, 0.84f, 0.92f));
            cracks.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var maskObject = new GameObject("ReflectionMask");
            maskObject.transform.SetParent(root.transform, false);
            var mask = maskObject.AddComponent<SpriteMask>();
            mask.sprite = surfaceSprite;
            mask.alphaCutoff = 0.05f;
            mask.isCustomRangeActive = true;
            mask.frontSortingOrder = 4;
            mask.backSortingOrder = 2;

            var reflection = CreateRenderer(root.transform, "PlayerReflection", null,
                AssetDatabase.LoadAssetAtPath<Material>(ReflectionMaterialPath), 3,
                new Color(0.24f, 0.82f, 0.72f, 0f));
            reflection.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var anchor = new GameObject("GlassAnchor");
            anchor.transform.SetParent(root.transform, false);

            const float artScale = 0.15f;
            frame.transform.localScale = Vector3.one * artScale;
            mirror.transform.localScale = Vector3.one * artScale;
            maskObject.transform.localScale = Vector3.one * artScale;
            cracks.transform.localScale = new Vector3(0.083f, 0.178f, 1f);
            cracks.transform.localPosition = new Vector3(0.08f, 0.03f, 0f);

            var shatter = root.AddComponent<MirrorShatterEffect>();
            var shatterData = new SerializedObject(shatter);
            shatterData.FindProperty("intactRenderer").objectReferenceValue = mirror;
            shatterData.FindProperty("frameRenderer").objectReferenceValue = frame;
            shatterData.FindProperty("crackRenderer").objectReferenceValue = cracks;
            SetCrackStages(shatterData);
            SetSpriteArray(shatterData.FindProperty("shardSprites"),
                AssetDatabase.LoadAllAssetsAtPath(ShardPath).OfType<Sprite>().ToArray());
            shatterData.FindProperty("shardTint").colorValue = new Color(0.28f, 1f, 0.82f, 0.92f);
            shatterData.FindProperty("shardCount").intValue = 38;
            shatterData.FindProperty("spawnArea").vector2Value = new Vector2(0.82f, 2.1f);
            shatterData.FindProperty("spawnCenterOffset").vector2Value = new Vector2(0.08f, 0.03f);
            shatterData.FindProperty("perspectiveEdgeScale").vector2Value = new Vector2(0.88f, 1.08f);
            shatterData.FindProperty("shardScaleRange").vector2Value = new Vector2(0.08f, 0.14f);
            shatterData.FindProperty("recommendedEnterDelay").floatValue = 0.8f;
            shatterData.ApplyModifiedPropertiesWithoutUndo();

            var reflectionController = root.AddComponent<MirrorReflectionController>();
            var reflectionData = new SerializedObject(reflectionController);
            reflectionData.FindProperty("reflectionRenderer").objectReferenceValue = reflection;
            reflectionData.FindProperty("mirrorIntactRenderer").objectReferenceValue = mirror;
            reflectionData.FindProperty("visibleDistance").floatValue = 6f;
            reflectionData.FindProperty("fullVisibilityDistance").floatValue = 2.5f;
            reflectionData.FindProperty("maximumAlpha").floatValue = 0.72f;
            reflectionData.FindProperty("reflectionTint").colorValue = new Color(0.22f, 0.86f, 0.72f, 1f);
            reflectionData.FindProperty("reflectionScale").floatValue = 0.62f;
            reflectionData.FindProperty("horizontalResponse").floatValue = 0.12f;
            reflectionData.FindProperty("verticalResponse").floatValue = 0.34f;
            reflectionData.FindProperty("centerOffset").vector2Value = new Vector2(0.08f, 0.03f);
            reflectionData.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Level02JungleMirrorGateBuilder] Built " + PrefabPath, prefab);
            return prefab;
        }

        static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, Material material,
            int sortingOrder, Color color)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (material) renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
            return renderer;
        }

        static void SetCrackStages(SerializedObject data)
        {
            var sprites = new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>(Crack1Path),
                AssetDatabase.LoadAssetAtPath<Sprite>(Crack2Path),
                AssetDatabase.LoadAssetAtPath<Sprite>(Crack3Path)
            };
            var stages = data.FindProperty("crackStages");
            stages.arraySize = sprites.Length;
            for (var i = 0; i < sprites.Length; i++)
            {
                var stage = stages.GetArrayElementAtIndex(i);
                stage.FindPropertyRelative("showAfterHit").intValue = i + 1;
                stage.FindPropertyRelative("sprite").objectReferenceValue = sprites[i];
            }
        }

        static void SetSpriteArray(SerializedProperty property, Sprite[] sprites)
        {
            property.arraySize = sprites.Length;
            for (var i = 0; i < sprites.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        static void ConfigureSprite(string path, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
#endif
