using System.IO;
using System.Linq;
using MirrorTrial.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MirrorTrial.Editor.Enemies
{
    public static class EnemyLaunchPresentationBuilder
    {
        const string ArtRoot = "Assets/MirrorTrial/Art/Effects/EnemyLaunch/PhysicalV1";
        const string PrefabRoot = "Assets/MirrorTrial/Prefabs/VFX/EnemyLaunch/PhysicalV1";
        const string AnimationRoot = "Assets/MirrorTrial/Animations/Enemies/SharedLaunch";
        const string ProfilePath = "Assets/MirrorTrial/Enemies/Shared/EnemyLaunchPresentation_PhysicalV1.asset";

        [InitializeOnLoadMethod]
        static void BuildOnNextEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                Build();
            };
        }

        [MenuItem("Mirror Trial/Build/重建公共击飞表现 V1")]
        public static void Build()
        {
            EnsureFolder(PrefabRoot);
            EnsureFolder(AnimationRoot);
            EnsureFolder(Path.GetDirectoryName(ProfilePath)?.Replace('\\', '/'));

            var takeoff = BuildPrefab("TakeoffDust", 0.18f, new Vector2(0.8f, 1.05f), 111);
            var airborne = BuildPrefab("AirborneStreaks", 0.22f, new Vector2(1f, 1f), 109);
            var landing = BuildPrefab("LandingImpact", 0.28f, new Vector2(0.72f, 1.12f), 115);
            var slide = BuildPrefab("SlideDust", 0.2f, new Vector2(0.9f, 1.05f), 108);

            var profile = AssetDatabase.LoadAssetAtPath<EnemyLaunchPresentationProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<EnemyLaunchPresentationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.takeoffPrefab = takeoff;
            profile.airbornePrefab = airborne;
            profile.landingPrefab = landing;
            profile.slidePrefab = slide;

            BuildAnimationSet(profile);
            EditorUtility.SetDirty(profile);
            BindProfileToEnemies(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = profile;
            Debug.Log("公共击飞表现 V1 已生成并绑定到敌兵 Profile。", profile);
        }

        static GameObject BuildPrefab(string name, float lifetime, Vector2 scaleRange, int sortingOrder)
        {
            var texturePath = $"{ArtRoot}/EnemyLaunch_{name}_v1.png";
            ConfigureTexture(texturePath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (!sprite) return null;

            var go = new GameObject($"EnemyLaunch_{name}_VFX");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            var instance = go.AddComponent<EnemyLaunchVfxInstance>();
            instance.Configure(lifetime, true, scaleRange);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabRoot}/EnemyLaunch_{name}_VFX.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void ConfigureTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static void BuildAnimationSet(EnemyLaunchPresentationProfile profile)
        {
            ConfigureCharacterSheet("Assets/MirrorTrial/Art/Characters/SharedLaunchV1/Launch.png", "Launch", 8);
            ConfigureCharacterSheet("Assets/MirrorTrial/Art/Characters/SharedLaunchV1/GetUp.png", "GetUp", 6);
            BuildSlicedSpriteClip("Launch", "Assets/MirrorTrial/Art/Characters/SharedLaunchV1/Launch.png", 12f);
            BuildSlicedSpriteClip("LaunchGetUp", "Assets/MirrorTrial/Art/Characters/SharedLaunchV1/GetUp.png", 10f);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/MirrorTrial/Animations/Player/Player_MirrorTrial.controller");
            if (!controller) return;
            var stateMachine = controller.layers[0].stateMachine;
            AddStateIfMissing(controller, stateMachine, profile.launchAnimation);
            AddStateIfMissing(controller, stateMachine, profile.getUpAnimation);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        static void ConfigureCharacterSheet(string path, string prefix, int frameCount)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var sprites = new SpriteMetaData[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                sprites[i] = new SpriteMetaData
                {
                    name = $"{prefix}_{i}",
                    rect = new Rect(i * 96f, 0f, 96f, 84f),
                    alignment = (int)SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f)
                };
            }
#pragma warning disable CS0618
            importer.spritesheet = sprites;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        static void BuildSlicedSpriteClip(string clipName, string sheetPath, float frameRate)
        {
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => FrameIndex(sprite.name))
                .ThenBy(sprite => sprite.name)
                .ToArray();
            if (sprites.Length == 0)
            {
                Debug.LogError($"No sliced sprites found in {sheetPath}");
                return;
            }

            var targetPath = $"{AnimationRoot}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            if (!clip)
            {
                clip = new AnimationClip { name = clipName };
                AssetDatabase.CreateAsset(clip, targetPath);
            }

            clip.frameRate = frameRate;
            foreach (var oldBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, oldBinding, null);

            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (var i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = sprites[i] };

            AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            }, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        static int FrameIndex(string spriteName)
        {
            var separator = spriteName.LastIndexOf('_');
            return separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), out var index)
                ? index
                : int.MaxValue;
        }

        static void AddStateIfMissing(AnimatorController controller, AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (var child in stateMachine.states)
                if (child.state.name == stateName) return;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationRoot}/{stateName}.anim");
            if (!clip) return;
            var state = controller.AddMotion(clip);
            state.name = stateName;
            EditorUtility.SetDirty(state);
        }

        static void BindProfileToEnemies(EnemyLaunchPresentationProfile profile)
        {
            BindProfileToAssets(profile, AssetDatabase.FindAssets("t:EnemyAIProfile"));
            BindProfileToAssets(profile, AssetDatabase.FindAssets("t:MirrorBossSimpleProfile"));
        }

        static void BindProfileToAssets(EnemyLaunchPresentationProfile profile, string[] guids)
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                var serialized = new SerializedObject(asset);
                var property = serialized.FindProperty("launchSettings")?.FindPropertyRelative("presentation");
                if (property == null) continue;
                property.objectReferenceValue = profile;
                var getUpDuration = serialized.FindProperty("launchSettings")?.FindPropertyRelative("getUpDuration");
                if (getUpDuration != null) getUpDuration.floatValue = 0.6f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
