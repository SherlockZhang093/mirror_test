using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
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
            LevelNamePromptWindow.Show(false);
        }

        [MenuItem("Tools/镜像试炼/关卡/新建镜中关卡")]
        public static void NewMirrorLevel()
        {
            LevelNamePromptWindow.Show(true);
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

        static bool CreateNewLevel(bool mirror, string levelId, out string error)
        {
            error = ValidateLevelId(levelId);
            if (!string.IsNullOrEmpty(error))
                return false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                error = "已取消创建；当前场景没有被替换。";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder("Assets/MirrorTrial", "LevelConfigs");

            var config = ScriptableObject.CreateInstance<LevelConfig>();
            config.levelId = levelId;
            config.displayName = levelId;
            config.isMirrorLevel = mirror;
            if (!mirror)
                config.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            var path = string.Format("{0}/{1}.asset", ConfigFolder, config.levelId);
            AssetDatabase.CreateAsset(config, path);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            LevelConfigImporter.GenerateScene(config);
            EditorGUIUtility.PingObject(config);
            return true;
        }

        static string ValidateLevelId(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
                return "请输入关卡名。";

            levelId = levelId.Trim();
            if (levelId.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || levelId.Contains("/") || levelId.Contains("\\"))
                return "关卡名包含文件名不支持的字符。";

            var configPath = string.Format("{0}/{1}.asset", ConfigFolder, levelId);
            var scenePath = string.Format("{0}/{1}.unity", SceneFolder, levelId);
            if (AssetDatabase.LoadAssetAtPath<Object>(configPath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
                return "已经存在同名关卡配置或场景，请换一个名字。";

            return string.Empty;
        }

        sealed class LevelNamePromptWindow : EditorWindow
        {
            bool mirror;
            string levelId = string.Empty;
            string validationMessage = string.Empty;

            public static void Show(bool mirror)
            {
                var window = CreateInstance<LevelNamePromptWindow>();
                window.mirror = mirror;
                window.titleContent = new GUIContent(mirror ? "新建镜中关卡" : "新建现实关卡");
                window.minSize = new Vector2(420f, 165f);
                window.maxSize = window.minSize;
                window.ShowModalUtility();
            }

            void OnGUI()
            {
                EditorGUILayout.Space(12f);
                EditorGUILayout.LabelField(mirror ? "请输入新的镜中关卡名" : "请输入新的现实关卡名", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("必须输入唯一名称；不会自动命名，也不会覆盖已有配置或场景。", MessageType.Info);

                GUI.SetNextControlName("LevelIdField");
                levelId = EditorGUILayout.TextField("关卡名", levelId);
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.FocusTextInControl("LevelIdField");

                if (!string.IsNullOrEmpty(validationMessage))
                    EditorGUILayout.HelpBox(validationMessage, MessageType.Error);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("取消", GUILayout.Width(90f)))
                    Close();
                if (GUILayout.Button("创建", GUILayout.Width(90f)))
                {
                    var trimmedId = levelId.Trim();
                    if (CreateNewLevel(mirror, trimmedId, out validationMessage))
                        Close();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
