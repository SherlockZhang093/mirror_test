using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor
{
    internal static class HunyuanGlbMaterialSetup
    {
        private const string Root = "Assets/MirrorTrial/Art/HunyuanFloatingTemple/Textures/";
        private const string MaterialPath = "Assets/MirrorTrial/Art/Materials/MAT_HunyuanFloatingTemple_GLB.mat";
        private const string ObjectName = "Background_FloatingTemple_Hunyuan";

        [MenuItem("Mirror Trial/Environment/Apply Hunyuan GLB Material")]
        private static void Apply()
        {
            ConfigureTexture(Root + "HunyuanTemple_BaseColor.png", false, true);
            ConfigureTexture(Root + "HunyuanTemple_Normal.png", true, false);
            ConfigureTexture(Root + "HunyuanTemple_MetallicRoughness.png", false, false);

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "HunyuanTemple_BaseColor.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "HunyuanTemple_Normal.png");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", new Color(0.34f, 0.43f, 0.58f, 1f));
            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0.02f);
            material.SetFloat("_Smoothness", 0.18f);
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);

            GameObject target = GameObject.Find(ObjectName);
            if (target == null)
            {
                Debug.LogError("Run Place Hunyuan Floating Temple first.");
                return;
            }

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(renderer, "Apply Hunyuan GLB Material");
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                EditorUtility.SetDirty(renderer);
            }

            EnsureBackgroundLight();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(target.scene);
            EditorSceneManager.SaveScene(target.scene);
            Selection.activeGameObject = target;
            Debug.Log("Applied original Hunyuan GLB base-color and normal textures with URP/Lit lighting.");
        }

        private static void ConfigureTexture(string path, bool normalMap, bool srgb)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != 4096;
            if (!changed) return;
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void EnsureBackgroundLight()
        {
            const string lightName = "Hunyuan_Background_KeyLight";
            GameObject lightObject = GameObject.Find(lightName);
            if (lightObject == null)
            {
                lightObject = new GameObject(lightName, typeof(Light));
                Undo.RegisterCreatedObjectUndo(lightObject, "Create Hunyuan Background Light");
            }

            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.52f, 0.66f, 0.9f);
            light.intensity = 0.55f;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << LayerMask.NameToLayer("Background Far");
            lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        }
    }
}

