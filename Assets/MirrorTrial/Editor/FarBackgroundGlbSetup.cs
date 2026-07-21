using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor
{
    internal static class FarBackgroundGlbSetup
    {
        private const string RootName = "FarBackground_GlbIslands";
        private const string AutoRunKey = "MirrorTrial.FarBackgroundGlbSetup.AutoRun.20260720";

        private static readonly string[] AssetPaths =
        {
            "Assets/MirrorTrial/Art/far_bg/island_01.glb",
            "Assets/MirrorTrial/Art/far_bg/d0cf6bd6251b59a3a5b277dc939019c2.glb",
            "Assets/MirrorTrial/Art/far_bg/a3f6bed938f95b466457b532e4e3d6e1.glb"
        };

        private static readonly Vector3[] Positions =
        {
            new Vector3(-13.5f, 0.3f, 7.8f),
            new Vector3(-1.5f, 1.2f, 9.2f),
            new Vector3(11.5f, -0.1f, 8.5f)
        };

        private static readonly float[] TargetHeights = { 5.4f, 4.3f, 5.0f };
        private static readonly float[] YRotations = { -8f, 12f, -14f };
        private static readonly Color[] DepthTints =
        {
            new Color(0.43f, 0.54f, 0.70f, 1f),
            new Color(0.32f, 0.43f, 0.60f, 1f),
            new Color(0.38f, 0.49f, 0.66f, 1f)
        };

        [InitializeOnLoadMethod]
        private static void PlaceAfterCompilation()
        {
            if (SessionState.GetBool(AutoRunKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoRunKey, true);
            EditorApplication.delayCall += PlaceFarBackgroundIslands;
        }
        [MenuItem("Mirror Trial/Environment/Place Far Background GLB Islands")]
        private static void PlaceFarBackgroundIslands()
        {
            foreach (string path in AssetPaths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport |
                                                ImportAssetOptions.ForceUpdate);
            }

            Scene scene = SceneManager.GetActiveScene();
            Transform root = FindOrCreateRoot(scene);
            int placed = 0;

            for (int i = 0; i < AssetPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPaths[i]);
                if (prefab == null)
                {
                    Debug.LogError($"GLB was not imported as a GameObject: {AssetPaths[i]}");
                    continue;
                }

                string objectName = "FarBG_" + Path.GetFileNameWithoutExtension(AssetPaths[i]);
                Transform existing = root.Find(objectName);
                GameObject instance;
                if (existing != null)
                {
                    instance = existing.gameObject;
                    Undo.RecordObject(instance.transform, "Update far background island");
                }
                else
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "Place far background island");
                    instance.name = objectName;
                    instance.transform.SetParent(root, true);
                }

                instance.transform.position = Positions[i];
                instance.transform.rotation = Quaternion.Euler(0f, YRotations[i], 0f);
                instance.transform.localScale = Vector3.one;
                ScaleToHeight(instance, TargetHeights[i]);
                ConfigureAsBackground(instance, DepthTints[i]);
                placed++;
            }

            if (placed == 0)
            {
                Debug.LogError("No far-background GLB assets could be placed. Check the glTF importer and Console errors.");
                return;
            }

            Selection.activeGameObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SceneView.FrameLastActiveSceneView();
            Debug.Log($"Placed {placed} GLB islands under {RootName}.");
        }

        private static Transform FindOrCreateRoot(Scene scene)
        {
            GameObject rootObject = GameObject.Find(RootName);
            if (rootObject == null)
            {
                rootObject = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create far background root");
                SceneManager.MoveGameObjectToScene(rootObject, scene);
            }

            int backgroundLayer = LayerMask.NameToLayer("Background Far");
            if (backgroundLayer >= 0)
            {
                rootObject.layer = backgroundLayer;
            }
            return rootObject.transform;
        }

        private static void ScaleToHeight(GameObject instance, float targetHeight)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y > 0.0001f)
            {
                instance.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);
            }
        }

        private static void ConfigureAsBackground(GameObject instance, Color tint)
        {
            int backgroundLayer = LayerMask.NameToLayer("Background Far");
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.isStatic = true;
                if (backgroundLayer >= 0)
                {
                    child.gameObject.layer = backgroundLayer;
                }
            }

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint);
                block.SetColor("_Color", tint);
                renderer.SetPropertyBlock(block);
                EditorUtility.SetDirty(renderer);
            }
        }
    }
}


// Trigger Unity compilation after glTFast installation.
