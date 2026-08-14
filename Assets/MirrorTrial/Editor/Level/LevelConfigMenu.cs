using System.IO;
using System.Text.RegularExpressions;
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
            var scenePath = string.Format("{0}/{1}.unity", SceneFolder, config.levelId);
            var overwrite = !AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) ||
                EditorUtility.DisplayDialog(
                    "确认覆盖场景",
                    "场景已经存在：\n" + scenePath + "\n\n只有确认后才会覆盖。",
                    "确认覆盖",
                    "取消");
            if (overwrite)
                LevelConfigImporter.GenerateScene(config, scenePath, true);
        }

        [MenuItem("Tools/镜像试炼/关卡/新建现实关卡")]
        public static void NewRealityLevel()
        {
            CreateNextLevel(false);
        }

        [MenuItem("Tools/镜像试炼/关卡/新建镜中关卡")]
        public static void NewMirrorLevel()
        {
            CreateNextLevel(true);
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

        static void CreateNextLevel(bool mirror)
        {
            var prefix = mirror ? "Level_Mirror_" : "Level_Reality_";
            var nextNumber = FindNextLevelNumber(prefix);
            var levelId = prefix + nextNumber.ToString("00");

            var configPath = string.Format("{0}/{1}.asset", ConfigFolder, levelId);
            var scenePath = string.Format("{0}/{1}.unity", SceneFolder, levelId);
            if (AssetDatabase.LoadAssetAtPath<Object>(configPath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
            {
                EditorUtility.DisplayDialog(
                    "无法新建关卡",
                    "自动编号得到的关卡名已经存在：" + levelId + "\n请检查关卡配置和场景文件。",
                    "确定");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder("Assets/MirrorTrial", "LevelConfigs");

            var config = ScriptableObject.CreateInstance<LevelConfig>();
            config.levelId = levelId;
            config.displayName = mirror ? "镜中关卡 " + nextNumber : "现实关卡 " + nextNumber;
            config.isMirrorLevel = mirror;
            if (!mirror)
                config.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            AssetDatabase.CreateAsset(config, configPath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            LevelConfigImporter.GenerateScene(config, scenePath);
            EditorGUIUtility.PingObject(config);
        }

        static int FindNextLevelNumber(string prefix)
        {
            var maxNumber = 0;
            var pattern = "^" + Regex.Escape(prefix) + "(\\d+)$";

            if (AssetDatabase.IsValidFolder(ConfigFolder))
            {
                var configGuids = AssetDatabase.FindAssets("t:LevelConfig", new[] { ConfigFolder });
                foreach (var guid in configGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
                    if (config)
                        UpdateMaxNumber(config.levelId, pattern, ref maxNumber);
                    UpdateMaxNumber(Path.GetFileNameWithoutExtension(path), pattern, ref maxNumber);
                }
            }

            if (AssetDatabase.IsValidFolder(SceneFolder))
            {
                var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder });
                foreach (var guid in sceneGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    UpdateMaxNumber(Path.GetFileNameWithoutExtension(path), pattern, ref maxNumber);
                }
            }

            return maxNumber + 1;
        }

        static void UpdateMaxNumber(string candidate, string pattern, ref int maxNumber)
        {
            if (string.IsNullOrEmpty(candidate))
                return;

            var match = Regex.Match(candidate, pattern, RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var number) && number > maxNumber)
                maxNumber = number;
        }
    }
}
