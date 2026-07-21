using MirrorTrial.Level;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    public static class LevelConfigMenu
    {
        const string ConfigFolder = "Assets/MirrorTrial/LevelConfigs";
        const string SceneFolder = "Assets/MirrorTrial/Scenes";
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";

        [MenuItem("Tools/镜像试炼/关卡/从场景导出当前关卡配置")]
        public static void ExportCurrent()
        {
            LevelConfigExporter.ExportCurrentScene();
        }

        [MenuItem("Tools/镜像试炼/关卡/从配置生成场景")]
        public static void GenerateFromConfig()
        {
            var config = Selection.activeObject as LevelConfig;
            if (!config)
            {
                EditorUtility.DisplayDialog("生成场景", "请在 Project 窗口选中一个 LevelConfig 资产后再执行。", "确定");
                return;
            }
            LevelConfigImporter.GenerateScene(config);
        }

        [MenuItem("Tools/镜像试炼/关卡/新建现实关卡")]
        public static void NewRealityLevel()
        {
            CreateNewLevel(false);
        }

        [MenuItem("Tools/镜像试炼/关卡/新建镜中关卡")]
        public static void NewMirrorLevel()
        {
            CreateNewLevel(true);
        }

        [MenuItem("Tools/镜像试炼/关卡/批量生成所有配置场景")]
        public static void GenerateAllConfigs()
        {
            if (!AssetDatabase.IsValidFolder(ConfigFolder))
            {
                EditorUtility.DisplayDialog("批量生成", "未找到配置目录：" + ConfigFolder, "确定");
                return;
            }
            var guids = AssetDatabase.FindAssets("t:LevelConfig", new string[] { ConfigFolder });
            var n = 0;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
                if (config)
                {
                    LevelConfigImporter.GenerateScene(config);
                    n++;
                }
            }
            Debug.Log("[镜像试炼] 批量生成完成：" + n + " 个场景");
        }

        static void CreateNewLevel(bool mirror)
        {
            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder("Assets/MirrorTrial", "LevelConfigs");

            var config = ScriptableObject.CreateInstance<LevelConfig>();
            config.levelId = mirror ? "Level_Mirror_01" : "Level_Reality_01";
            config.displayName = mirror ? "镜中关卡 1" : "现实关卡 1";
            config.isMirrorLevel = mirror;
            if (!mirror)
                config.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            var path = AssetDatabase.GenerateUniqueAssetPath(string.Format("{0}/{1}.asset", ConfigFolder, config.levelId));
            AssetDatabase.CreateAsset(config, path);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            LevelConfigImporter.GenerateScene(config);
            EditorGUIUtility.PingObject(config);
        }
    }
}
