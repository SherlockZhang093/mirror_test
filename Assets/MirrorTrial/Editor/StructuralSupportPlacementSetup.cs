#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor
{
    [InitializeOnLoad]
    public static class StructuralSupportPlacementSetup
    {
        private const string RootName = "GeneratedStructuralSupports";
        private const string BasePath =
            "Assets/MirrorTrial/Art/Background2D/StructuralSupports/";

        static StructuralSupportPlacementSetup()
        {
            EditorApplication.delayCall += TryAutomaticInstall;
        }

        [MenuItem("Mirror Trial/Environment/Install Structural Supports")]
        public static void InstallFromMenu()
        {
            Install(true);
        }

        private static void TryAutomaticInstall()
        {
            Install(false);
        }

        private static void Install(bool reportFailure)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (reportFailure)
                    Debug.Log("Stop Play Mode before installing structural supports.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Level_Reality_01")
            {
                if (reportFailure)
                    Debug.Log("Open Level_Reality_01 before installing structural supports.");
                return;
            }

            Sprite leftTower = LoadSprite("support_left_tower_cliff_v1.png");
            Sprite mainArch = LoadSprite("support_single_main_arch_v1.png");
            Sprite foundation = LoadSprite("support_fortress_foundation_v1.png");
            Sprite massiveLowerFoundation = LoadSprite("support_massive_lower_foundation_v1.png");
            Sprite upperConnector = LoadSprite("support_upper_wall_connector_v1.png");
            Sprite upperPillarShaft = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/pingtai/slices/pingtai_04.png");
            if (leftTower == null || mainArch == null || foundation == null ||
                massiveLowerFoundation == null ||
                upperConnector == null || upperPillarShaft == null)
            {
                Debug.LogWarning("Structural support sprites are not ready yet. Unity will retry after import.");
                EditorApplication.delayCall += TryAutomaticInstall;
                return;
            }

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Install structural supports");
            }

            Color nearStone = new Color(0.46f, 0.56f, 0.68f, 1f);
            Color deepStone = new Color(0.40f, 0.50f, 0.62f, 1f);

            CreateOrUpdateSupport(root.transform, "LeftTowerCliff", leftTower,
                new Vector3(-18.20f, -7.00f, 0f), 1.35f, nearStone, true, -8);
            CreateOrUpdateSupport(root.transform, "CentralBrokenArch", mainArch,
                new Vector3(-9.25f, -4.30f, 0f), 0.55f, deepStone, false, -9);
            CreateOrUpdateSupport(root.transform, "RightFortressFoundation", foundation,
                new Vector3(-2.20f, -5.95f, 0f), 1.75f, nearStone, false, -8);
            CreateOrUpdateSupport(root.transform, "UpperWallConnector", upperConnector,
                new Vector3(3.00f, -3.10f, 0f), 1.40f, deepStone, true, -9);
            CreateOrUpdateSupport(root.transform, "UpperPillarGroundShaft", upperPillarShaft,
                new Vector3(-5.40f, -2.37f, 0f), 0.86f, nearStone, false, 0);
            CreateOrUpdateSupport(root.transform, "MassiveLowerFoundation", massiveLowerFoundation,
                new Vector3(-3.40f, -6.00f, 0f), 0.55f, nearStone, false, -7);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("Installed structural supports behind the gameplay platforms.");
        }

        private static Sprite LoadSprite(string fileName)
        {
            string path = BasePath + fileName;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single ||
                 !importer.alphaIsTransparency ||
                 importer.mipmapEnabled))
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

        private static void CreateOrUpdateSupport(Transform parent, string name, Sprite sprite,
            Vector3 position, float scale, Color color, bool flipX, int sortingOrder)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                if (name == "MassiveLowerFoundation")
                {
                    SpriteRenderer existingRenderer = existing.GetComponent<SpriteRenderer>();
                    if (existingRenderer != null)
                    {
                        existingRenderer.color = color;
                        existingRenderer.sortingOrder = sortingOrder;
                    }
                }
                return;
            }

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.flipX = flipX;
            renderer.sortingOrder = sortingOrder;
        }
    }
}
#endif