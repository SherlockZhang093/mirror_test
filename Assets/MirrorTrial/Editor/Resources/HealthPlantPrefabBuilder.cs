using System;
using System.IO;
using MirrorTrial.Combat;
using MirrorTrial.HealthResources;
using MirrorTrial.Player;
using MirrorTrial.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MirrorTrial.Editor.HealthResources
{
    [InitializeOnLoad]
    public static class HealthPlantPrefabBuilder
    {
        const string ArtRoot = "Assets/MirrorTrial/Art/Level02_Jungle/LifePlants";
        const string PrefabRoot = "Assets/MirrorTrial/Prefabs/Level/Resources";
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string GlowSpritePath = "Assets/MirrorTrial/Resources/Effects/pixel_charge_star.png";
        const string RequestRelativePath = "Temp/LifePlantPrefabBuild.request";

        static readonly string[] StateNames = { "Full", "Damaged1", "Damaged2", "Depleted" };

        static HealthPlantPrefabBuilder()
        {
            EditorApplication.delayCall += TryBuildRequested;
        }

        [MenuItem("Tools/Mirror Trial/关卡/生成生命植物 Prefab")]
        public static void Build()
        {
            EnsureFolder("Assets/MirrorTrial/Prefabs/Level", "Resources");
            EnsurePlayerComponents();
            var fern = BuildPlant(
                "GlowSacFern",
                "HealthResource_GlowSacFern",
                new Vector2(1.45f, 2.15f),
                new Vector2(0f, 1.05f),
                1.2f,
                1.35f,
                256f);
            var orchid = BuildPlant(
                "DewCupOrchid",
                "HealthResource_DewCupOrchid",
                new Vector2(1.65f, 1.35f),
                new Vector2(0f, 0.66f),
                0.78f,
                1.05f,
                256f);

            HealthResourceCatalogEditorUtility.Register(fern);
            HealthResourceCatalogEditorUtility.Register(orchid);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = fern;
            Debug.Log("[HealthPlantPrefabBuilder] BUILD_COMPLETE: created GlowSacFern and DewCupOrchid health-resource prefabs.");
        }

        static GameObject BuildPlant(string artStem, string prefabName, Vector2 colliderSize,
            Vector2 colliderOffset, float glowHeight, float glowRadius, float pixelsPerUnit)
        {
            var sprites = new Sprite[StateNames.Length];
            for (var i = 0; i < StateNames.Length; i++)
            {
                var path = $"{ArtRoot}/{artStem}_{StateNames[i]}_v1.png";
                ConfigureSprite(path, pixelsPerUnit);
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprites[i]) throw new InvalidOperationException($"Could not import life-plant sprite: {path}");
            }
            var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
            if (!glowSprite) throw new InvalidOperationException($"Could not load life glow sprite: {GlowSpritePath}");

            var root = new GameObject(prefabName);
            try
            {
                var collider = root.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = colliderSize;
                collider.offset = colliderOffset;
                root.AddComponent<Hurtbox>();
                var node = root.AddComponent<HealthResourceNode>();

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[0];
                renderer.sortingOrder = 15;
                var view = visual.AddComponent<HealthResourcePlantView>();

                var glow = BuildGlow(root.transform, node, glowSprite, glowHeight, glowRadius);
                ConfigureNode(node, renderer, glowSprite, glowHeight);
                ConfigureView(view, node, renderer, sprites);
                ConfigureGlow(glow.component, node, glow.light, glow.motes);

                var prefabPath = $"{PrefabRoot}/{prefabName}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ConfigureSprite(string path, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException($"Texture importer was not found: {path}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            textureSettings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(textureSettings);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        static void ConfigureNode(HealthResourceNode node, SpriteRenderer renderer,
            Sprite rewardSprite, float rewardHeight)
        {
            var data = new SerializedObject(node);
            data.FindProperty("maxDurability").intValue = 3;
            data.FindProperty("healthReward").intValue = 3;
            data.FindProperty("countHitsInsteadOfDamage").boolValue = true;
            data.FindProperty("rewardOrbSprite").objectReferenceValue = rewardSprite;
            data.FindProperty("rewardEffectOffset").vector3Value = new Vector3(0f, rewardHeight, 0f);
            data.FindProperty("rewardOrbColor").colorValue = new Color(1f, 1f, 0.9f, 1f);
            data.FindProperty("rewardTravelDuration").floatValue = 0.58f;
            data.FindProperty("flashColor").colorValue = new Color(1f, 1f, 0.82f, 1f);
            data.FindProperty("flashDuration").floatValue = 0.09f;
            data.FindProperty("keepDepletedVisual").boolValue = false;
            data.FindProperty("deactivateDelay").floatValue = 0f;
            var renderers = data.FindProperty("flashRenderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static (HealthResourcePlantGlow component, Light2D light, SpriteRenderer[] motes) BuildGlow(
            Transform parent, HealthResourceNode node, Sprite glowSprite, float height, float radius)
        {
            var glowObject = new GameObject("生命荧光");
            glowObject.transform.SetParent(parent, false);
            glowObject.transform.localPosition = new Vector3(0f, height, 0f);

            var light = glowObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.9f, 1f, 0.55f, 1f);
            light.intensity = 0.58f;
            light.pointLightInnerRadius = radius * 0.18f;
            light.pointLightOuterRadius = radius;
            light.falloffIntensity = 0.65f;

            var offsets = new[]
            {
                new Vector3(-radius * 0.32f, radius * 0.18f, 0f),
                new Vector3(radius * 0.28f, radius * 0.30f, 0f),
                new Vector3(radius * 0.08f, -radius * 0.16f, 0f)
            };
            var motes = new SpriteRenderer[offsets.Length];
            for (var i = 0; i < offsets.Length; i++)
            {
                var moteObject = new GameObject($"荧光微粒 {i + 1}");
                moteObject.transform.SetParent(glowObject.transform, false);
                moteObject.transform.localPosition = offsets[i];
                moteObject.transform.localScale = Vector3.one * 0.12f;
                motes[i] = moteObject.AddComponent<SpriteRenderer>();
                motes[i].sprite = glowSprite;
                motes[i].sortingOrder = 16;
                motes[i].color = new Color(1f, 1f, 0.78f, 0.28f);
            }

            var component = glowObject.AddComponent<HealthResourcePlantGlow>();
            return (component, light, motes);
        }

        static void ConfigureGlow(HealthResourcePlantGlow glow, HealthResourceNode node,
            Light2D light, SpriteRenderer[] motes)
        {
            var data = new SerializedObject(glow);
            data.FindProperty("node").objectReferenceValue = node;
            data.FindProperty("glowLight").objectReferenceValue = light;
            var moteData = data.FindProperty("motes");
            moteData.arraySize = motes.Length;
            for (var i = 0; i < motes.Length; i++)
                moteData.GetArrayElementAtIndex(i).objectReferenceValue = motes[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureView(HealthResourcePlantView view, HealthResourceNode node,
            SpriteRenderer renderer, Sprite[] sprites)
        {
            var data = new SerializedObject(view);
            data.FindProperty("node").objectReferenceValue = node;
            data.FindProperty("targetRenderer").objectReferenceValue = renderer;
            var states = data.FindProperty("durabilityStates");
            states.arraySize = sprites.Length;
            for (var i = 0; i < sprites.Length; i++)
                states.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void TryBuildRequested()
        {
            var requestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RequestRelativePath));
            if (!File.Exists(requestPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryBuildRequested;
                return;
            }

            try
            {
                Build();
                File.Delete(requestPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        static void EnsurePlayerComponents()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (!root.GetComponent<PlayerHealthReserve>()) root.AddComponent<PlayerHealthReserve>();
                if (!root.GetComponent<PlayerRecoveryAbility>()) root.AddComponent<PlayerRecoveryAbility>();
                var temporaryUi = root.GetComponent<HealthReserveUI>();
                if (temporaryUi) UnityEngine.Object.DestroyImmediate(temporaryUi);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
