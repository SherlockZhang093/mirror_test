using MirrorTrial.Combat;
using MirrorTrial.Level;
using MirrorTrial.Player;
using MirrorTrial.HealthResources;
using Platformer.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MirrorTrial.Editor.HealthResources
{
    public static class HealthResourceSampleInstaller
    {
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string SampleFolder = "Assets/MirrorTrial/Prefabs/Level/Resources";
        const string SamplePrefabPath = SampleFolder + "/HealthResource_Sample.prefab";
        const string Reality02Path = "Assets/MirrorTrial/Scenes/Level_Reality_02.unity";

        [MenuItem("Tools/Mirror Trial/关卡/在 Reality 02 安装生命资源示例")]
        public static void Install()
        {
            InstallPlayerComponents();
            var prefab = BuildSamplePrefab();
            PlaceInReality02(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[HealthResourceSampleInstaller] 生命资源系统已安装，并已在 Level_Reality_02 放置测试节点。", prefab);
        }

        public static void InstallFromBatchMode()
        {
            Install();
        }

        static void InstallPlayerComponents()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (!root.GetComponent<PlayerHealthReserve>()) root.AddComponent<PlayerHealthReserve>();
                if (!root.GetComponent<PlayerRecoveryAbility>()) root.AddComponent<PlayerRecoveryAbility>();
                var temporaryUi = root.GetComponent<MirrorTrial.UI.HealthReserveUI>();
                if (temporaryUi) Object.DestroyImmediate(temporaryUi);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject BuildSamplePrefab()
        {
            EnsureFolder("Assets/MirrorTrial/Prefabs/Level", "Resources");
            var root = new GameObject("HealthResource_Sample");
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MirrorTrial/Sprites/WhiteSquare.png");
                renderer.color = new Color(0.18f, 0.95f, 0.55f, 1f);
                renderer.sortingOrder = 15;
                root.transform.localScale = new Vector3(0.22f, 0.30f, 1f);
                var collider = root.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                root.AddComponent<Hurtbox>();
                root.AddComponent<HealthResourceNode>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, SamplePrefabPath);
                HealthResourceEditorWindow.ConfigurePrefab(prefab, 3, 3, "Hit", Color.white, 0.1f, null, 1f);
                HealthResourceCatalogEditorUtility.Register(prefab);
                return AssetDatabase.LoadAssetAtPath<GameObject>(SamplePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void PlaceInReality02(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(Reality02Path, OpenSceneMode.Single);
            var existing = GameObject.Find("HealthResource_Sample_Demo");
            if (existing) Object.DestroyImmediate(existing);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "HealthResource_Sample_Demo";
            var manager = Object.FindObjectOfType<LevelManager>();
            var spawn = manager ? manager.PlayerSpawn : null;
            instance.transform.position = spawn ? spawn.position + new Vector3(0.9f, 0.65f, 0f) : new Vector3(0.9f, 1f, 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
