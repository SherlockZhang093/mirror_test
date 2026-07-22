using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor
{
    internal static class HunyuanFloatingTempleSetup
    {
        private const string ModelPath = "Assets/MirrorTrial/Art/413bd3f62e0e0581f298dd521fcf9f7e.fbx";
        private const string MaterialFolder = "Assets/MirrorTrial/Art/Materials";
        private const string MaterialPath = MaterialFolder + "/MAT_HunyuanFloatingTemple_Background.mat";
        private const string SceneObjectName = "Background_FloatingTemple_Hunyuan";
        private static void PlaceFloatingTemple()
        {
            ConfigureStaticModelImport();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"Hunyuan model not found: {ModelPath}");
                return;
            }

            GameObject instance = FindExistingInstance(model);
            if (instance == null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(model, SceneManager.GetActiveScene());
                Undo.RegisterCreatedObjectUndo(instance, "Place Hunyuan Floating Temple");
                instance.transform.position = new Vector3(-3f, 0f, 3.5f);
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * 356.5f;
            }
            else
            {
                Undo.RecordObject(instance.transform, "Configure Hunyuan Floating Temple");
                Vector3 position = instance.transform.position;
                position.z = 3.5f;
                instance.transform.position = position;
                instance.transform.rotation = Quaternion.identity;
            }

            instance.name = SceneObjectName;
            instance.isStatic = true;
            TryParentUnderTerrain(instance.transform);

            Material backgroundMaterial = GetOrCreateBackgroundMaterial();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(renderer, "Assign Hunyuan Background Material");
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = backgroundMaterial;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtility.SetDirty(renderer);
            }

            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            EditorSceneManager.SaveScene(instance.scene);
            SceneView.FrameLastActiveSceneView();
            Debug.Log("Placed Hunyuan floating temple as an unlit background prop at Z = 3.5.");
        }

        private static void ConfigureStaticModelImport()
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer))
            {
                return;
            }

            bool changed = importer.animationType != ModelImporterAnimationType.None ||
                           importer.importAnimation || importer.importCameras || importer.importLights;
            if (!changed)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
        }

        private static GameObject FindExistingInstance(GameObject model)
        {
            foreach (GameObject candidate in Object.FindObjectsOfType<GameObject>(true))
            {
                if (!candidate.scene.IsValid())
                {
                    continue;
                }

                Object source = PrefabUtility.GetCorrespondingObjectFromSource(candidate);
                if (source == model || candidate.name == SceneObjectName || candidate.name == Path.GetFileNameWithoutExtension(ModelPath))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static void TryParentUnderTerrain(Transform instance)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform terrain = FindDescendant(root.transform, "\u5730\u5f62");
                if (terrain != null)
                {
                    instance.SetParent(terrain, true);
                    return;
                }
            }
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root.name == targetName)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform result = FindDescendant(child, targetName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private static Material GetOrCreateBackgroundMaterial()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/MirrorTrial/Art", "Materials");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(shader) { name = "MAT_HunyuanFloatingTemple_Background" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = Shader.Find("Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", new Color(0.16f, 0.24f, 0.34f, 1f));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
