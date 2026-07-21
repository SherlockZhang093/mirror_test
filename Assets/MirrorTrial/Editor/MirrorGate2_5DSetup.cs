#if UNITY_EDITOR
using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MirrorTrial.Editor
{
    [InitializeOnLoad]
    public static class MirrorGate2_5DSetup
    {
        const string PrefabPath = "Assets/MirrorTrial/Prefabs/Level/MirrorGate.prefab";
        const string NichePath = "Assets/MirrorTrial/Art/MirrorGate2_5D/mirror_gate_castle_niche_v1.png";
        const string GlassMaterialPath = "Assets/MirrorTrial/Materials/MirrorGlass2D.mat";
        const string PerspectiveMaterialPath = "Assets/MirrorTrial/Materials/MirrorPlanePerspective2D.mat";
        const string GlassShaderName = "MirrorTrial/Mirror Glass 2D";
        const string PerspectiveShaderName = "MirrorTrial/Mirror Plane Perspective 2D";

        static MirrorGate2_5DSetup()
        {
            EditorApplication.delayCall += TryAutomaticInstall;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        }

        [MenuItem("Mirror Trial/Environment/Install 2.5D Mirror Gate")]
        public static void InstallFromMenu()
        {
            Install(true);
        }

        static void TryAutomaticInstall()
        {
            Install(false);
        }

        static void OnPrefabStageClosing(PrefabStage stage)
        {
            if (stage != null && stage.assetPath == PrefabPath)
                EditorApplication.delayCall += TryAutomaticInstall;
        }

        static void Install(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PrefabStage currentStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentStage != null && currentStage.assetPath == PrefabPath) return;

            Sprite nicheSprite = ImportSprite(NichePath);
            Shader glassShader = Shader.Find(GlassShaderName);
            Shader perspectiveShader = Shader.Find(PerspectiveShaderName);
            if (!nicheSprite || !glassShader || !perspectiveShader)
            {
                EditorApplication.delayCall += TryAutomaticInstall;
                return;
            }

            EnsureFolder("Assets/MirrorTrial", "Materials");
            Material glassMaterial = LoadOrCreateMaterial(GlassMaterialPath, glassShader, "MirrorGlass2D");
            glassMaterial.SetColor("_GlassTint", new Color(0.18f, 0.24f, 0.34f, 1f));
            glassMaterial.SetFloat("_Opacity", 0.22f);
            glassMaterial.SetFloat("_HighlightStrength", 0.08f);
            glassMaterial.SetFloat("_FlowSpeed", 0.035f);
            SetPerspective(glassMaterial);

            Material perspectiveMaterial = LoadOrCreateMaterial(
                PerspectiveMaterialPath, perspectiveShader, "MirrorPlanePerspective2D");
            SetPerspective(perspectiveMaterial);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (!force && root.transform.Find("GlassAnchor") &&
                    root.GetComponent<MirrorReflectionController>())
                    return;

                Transform oldMirror = root.transform.Find("Mirror");
                Transform oldFrame = root.transform.Find("Frame");
                Transform cracks = root.transform.Find("Cracks");
                if (!oldMirror || !oldFrame || !cracks) return;

                SpriteRenderer intactRenderer = oldMirror.GetComponent<SpriteRenderer>();
                SpriteRenderer crackRenderer = cracks.GetComponent<SpriteRenderer>();
                if (!intactRenderer || !intactRenderer.sprite || !crackRenderer || !crackRenderer.sprite) return;

                Vector3 originalVisualCenter = oldMirror.localPosition;
                float originalHeight = Mathf.Max(0.01f, intactRenderer.sprite.bounds.size.y);
                float nicheScale = originalHeight / Mathf.Max(0.01f, nicheSprite.bounds.size.y);

                Transform niche = FindOrCreate(root.transform, "CastleNiche");
                niche.SetSiblingIndex(0);
                niche.localPosition = originalVisualCenter;
                niche.localRotation = Quaternion.identity;
                niche.localScale = Vector3.one * nicheScale;
                SpriteRenderer nicheRenderer = GetOrAdd<SpriteRenderer>(niche.gameObject);
                nicheRenderer.sprite = nicheSprite;
                nicheRenderer.color = new Color(0.72f, 0.80f, 0.92f, 1f);
                nicheRenderer.sortingOrder = 2;
                nicheRenderer.flipX = true;

                oldFrame.gameObject.SetActive(false);

                float nicheWidth = nicheSprite.bounds.size.x * nicheScale;
                float nicheHeight = nicheSprite.bounds.size.y * nicheScale;
                float glassWidth = nicheWidth * 0.31f;
                float glassHeight = nicheHeight * 0.65f;
                Vector3 glassCenter = originalVisualCenter + new Vector3(-nicheWidth * 0.012f, 0f, 0f);

                Transform glassAnchor = FindOrCreate(root.transform, "GlassAnchor");
                glassAnchor.localPosition = glassCenter;
                glassAnchor.localRotation = Quaternion.identity;
                glassAnchor.localScale = Vector3.one;

                Vector2 intactSize = intactRenderer.sprite.bounds.size;
                oldMirror.localPosition = glassCenter;
                oldMirror.localRotation = Quaternion.identity;
                oldMirror.localScale = new Vector3(
                    glassWidth / Mathf.Max(0.01f, intactSize.x),
                    glassHeight / Mathf.Max(0.01f, intactSize.y), 1f);
                intactRenderer.material = glassMaterial;
                intactRenderer.color = new Color(0.70f, 0.78f, 0.92f, 0.52f);
                intactRenderer.sortingOrder = 3;

                Transform maskTransform = FindOrCreate(root.transform, "ReflectionMask");
                maskTransform.localPosition = glassCenter;
                maskTransform.localRotation = Quaternion.identity;
                maskTransform.localScale = oldMirror.localScale;
                SpriteMask mask = GetOrAdd<SpriteMask>(maskTransform.gameObject);
                mask.sprite = intactRenderer.sprite;
                mask.alphaCutoff = 0.05f;
                mask.isCustomRangeActive = true;
                mask.frontSortingOrder = 4;
                mask.backSortingOrder = 3;

                Transform reflection = FindOrCreate(root.transform, "PlayerReflection");
                reflection.localPosition = glassCenter;
                reflection.localRotation = Quaternion.identity;
                SpriteRenderer reflectionRenderer = GetOrAdd<SpriteRenderer>(reflection.gameObject);
                reflectionRenderer.sprite = null;
                reflectionRenderer.material = perspectiveMaterial;
                reflectionRenderer.sortingOrder = 4;
                reflectionRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                reflectionRenderer.color = new Color(0.46f, 0.55f, 0.70f, 0f);

                Vector2 crackSize = crackRenderer.sprite.bounds.size;
                cracks.localPosition = glassCenter;
                cracks.localRotation = Quaternion.identity;
                cracks.localScale = new Vector3(
                    glassWidth / Mathf.Max(0.01f, crackSize.x),
                    glassHeight / Mathf.Max(0.01f, crackSize.y), 1f);
                crackRenderer.material = perspectiveMaterial;
                crackRenderer.sortingOrder = 5;

                MirrorReflectionController controller = GetOrAdd<MirrorReflectionController>(root);
                SerializedObject controllerObject = new SerializedObject(controller);
                controllerObject.FindProperty("reflectionRenderer").objectReferenceValue = reflectionRenderer;
                controllerObject.FindProperty("mirrorIntactRenderer").objectReferenceValue = intactRenderer;
                controllerObject.FindProperty("visibleDistance").floatValue = 6f;
                controllerObject.FindProperty("fullVisibilityDistance").floatValue = 2.5f;
                controllerObject.FindProperty("maximumAlpha").floatValue = 0.68f;
                controllerObject.FindProperty("reflectionScale").floatValue = 0.62f;
                controllerObject.FindProperty("centerOffset").vector2Value = Vector2.zero;
                controllerObject.ApplyModifiedPropertiesWithoutUndo();

                MirrorShatterEffect shatter = root.GetComponent<MirrorShatterEffect>();
                if (shatter)
                {
                    SerializedObject shatterObject = new SerializedObject(shatter);
                    shatterObject.FindProperty("intactRenderer").objectReferenceValue = intactRenderer;
                    shatterObject.FindProperty("frameRenderer").objectReferenceValue = nicheRenderer;
                    shatterObject.FindProperty("crackRenderer").objectReferenceValue = crackRenderer;
                    shatterObject.FindProperty("spawnArea").vector2Value = new Vector2(glassWidth, glassHeight);
                    shatterObject.FindProperty("spawnCenterOffset").vector2Value = new Vector2(glassCenter.x, glassCenter.y);
                    shatterObject.FindProperty("perspectiveEdgeScale").vector2Value = new Vector2(1.08f, 0.90f);
                    shatterObject.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Installed left-facing 2.5D mirror gate with perspective-matched effects.");
        }

        static Material LoadOrCreateMaterial(string path, Shader shader, string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SetPerspective(Material material)
        {
            material.SetFloat("_LeftEdgeScale", 1.08f);
            material.SetFloat("_RightEdgeScale", 0.90f);
            material.SetFloat("_VerticalSkew", -0.05f);
            EditorUtility.SetDirty(material);
        }

        static Sprite ImportSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !importer.alphaIsTransparency || importer.mipmapEnabled ||
                           importer.spritePixelsPerUnit != 100f;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Transform FindOrCreate(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child) return child;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component ? component : go.AddComponent<T>();
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
