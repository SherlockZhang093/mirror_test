using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    [CustomEditor(typeof(MirrorTrial.Level.LevelManager))]
    public class LevelManagerEditor : UnityEditor.Editor
    {
        MirrorTrial.Level.LevelManager manager;
        SerializedProperty levelId;
        SerializedProperty levelDisplayName;
        SerializedProperty playerSpawn;
        SerializedProperty geometryRoot;
        SerializedProperty gameplayRoot;
        SerializedProperty runtimeRoot;
        SerializedProperty autoCollectOnAwake;

        bool showInfo = true;
        bool showRoots = true;
        bool showAdd = true;
        bool showList = true;
        bool showValidate = true;

        void OnEnable()
        {
            manager = target as MirrorTrial.Level.LevelManager;
            levelId = serializedObject.FindProperty("levelId");
            levelDisplayName = serializedObject.FindProperty("levelDisplayName");
            playerSpawn = serializedObject.FindProperty("playerSpawn");
            geometryRoot = serializedObject.FindProperty("geometryRoot");
            gameplayRoot = serializedObject.FindProperty("gameplayRoot");
            runtimeRoot = serializedObject.FindProperty("runtimeRoot");
            autoCollectOnAwake = serializedObject.FindProperty("AutoCollectOnAwake");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("镜中试炼 - 关卡总控", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            showInfo = EditorGUILayout.Foldout(showInfo, "关卡信息");
            if (showInfo)
            {
                EditorGUILayout.PropertyField(levelId, new GUIContent("关卡ID"));
                EditorGUILayout.PropertyField(levelDisplayName, new GUIContent("关卡显示名"));
                EditorGUILayout.PropertyField(playerSpawn, new GUIContent("玩家出生点"));
            }

            showRoots = EditorGUILayout.Foldout(showRoots, "场景根节点");
            if (showRoots)
            {
                EditorGUILayout.PropertyField(geometryRoot, new GUIContent("地形根节点"));
                EditorGUILayout.PropertyField(gameplayRoot, new GUIContent("玩法根节点"));
                EditorGUILayout.PropertyField(runtimeRoot, new GUIContent("运行时根节点"));
                EditorGUILayout.PropertyField(autoCollectOnAwake, new GUIContent("启动时自动收集"));
            }

            EditorGUILayout.Space(10);
            showAdd = EditorGUILayout.Foldout(showAdd, "添加玩法对象", true);
            if (showAdd)
            {
                DrawAddButtons();
            }

            EditorGUILayout.Space(10);
            showList = EditorGUILayout.Foldout(showList, "已收集对象", true);
            if (showList)
            {
                DrawCollections();
            }

            EditorGUILayout.Space(10);
            showValidate = EditorGUILayout.Foldout(showValidate, "配置校验", true);
            if (showValidate)
            {
                DrawValidation();
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("全部收集", GUILayout.Height(28)))
            {
                Undo.RecordObject(manager, "全部收集");
                manager.CollectAll();
                EditorUtility.SetDirty(manager);
            }
        }

        void DrawAddButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 段落", GUILayout.Height(28)))
                AddGameplayObject<MirrorTrial.Level.LevelSegment>("段落", "SEG_", Vector3.zero);
            if (GUILayout.Button("+ 触发器", GUILayout.Height(28)))
                AddGameplayObject<MirrorTrial.Level.LevelTrigger>("触发器", "TR_", Vector3.zero);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 战斗", GUILayout.Height(28)))
                AddGameplayObject<MirrorTrial.Level.CombatEncounter>("战斗区", "Combat_", Vector3.zero);
            if (GUILayout.Button("+ 镜子门", GUILayout.Height(28)))
                AddGameplayObject<MirrorTrial.Level.MirrorGate>("镜子门", "MirrorGate_", Vector3.zero);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 门", GUILayout.Height(28)))
                AddGameplayObject<MirrorTrial.Level.AreaGate>("门", "Gate_", Vector3.zero);
            if (GUILayout.Button("+ 刷怪点", GUILayout.Height(28)))
                AddGameplayObject<MirrorTrial.Level.SpawnPoint>("刷怪点", "SP_", Vector3.zero);
            EditorGUILayout.EndHorizontal();
        }

        void AddGameplayObject<T>(string category, string prefix, Vector3 offset) where T : Component
        {
            var name = manager.GetUniqueName(prefix + "01");
            var pos = manager.transform.position + offset;
            var created = manager.CreateGameplayObject<T>(category, name, pos);
            if (created)
            {
                Selection.activeGameObject = created.gameObject;
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(created.gameObject.scene);
            }
        }

        void DrawCollections()
        {
            manager.CollectAll();
            EditorGUILayout.LabelField($"段落: {manager.Segments.Count}");
            EditorGUILayout.LabelField($"触发器: {manager.Triggers.Count}");
            EditorGUILayout.LabelField($"战斗区: {manager.Encounters.Count}");
            EditorGUILayout.LabelField($"镜子门: {manager.MirrorGates.Count}");
            EditorGUILayout.LabelField($"门: {manager.Gates.Count}");
            EditorGUILayout.LabelField($"刷怪点: {manager.SpawnPoints.Count}");
        }

        void DrawValidation()
        {
            if (GUILayout.Button("校验关卡", GUILayout.Height(32)))
            {
                ValidateAndShow();
            }
        }

        void ValidateAndShow()
        {
            manager.CollectAll();
            var issues = MirrorTrial.Level.LevelValidationUtility.Validate(manager);

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("关卡校验", "未发现配置问题。", "确定");
                Debug.Log("[镜中试炼] 关卡校验通过。");
                return;
            }

            var error = issues.Count(i => i.severity == MirrorTrial.Level.LevelValidationSeverity.Error);
            var warning = issues.Count(i => i.severity == MirrorTrial.Level.LevelValidationSeverity.Warning);
            var info = issues.Count(i => i.severity == MirrorTrial.Level.LevelValidationSeverity.Info);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"错误: {error} | 警告: {warning} | 信息: {info}\n");
            foreach (var issue in issues)
            {
                sb.AppendLine($"• [{issue.severity}] {issue.message}");
                switch (issue.severity)
                {
                    case MirrorTrial.Level.LevelValidationSeverity.Error:
                        Debug.LogError($"[镜中试炼][校验] {issue.message}", issue.context);
                        break;
                    case MirrorTrial.Level.LevelValidationSeverity.Warning:
                        Debug.LogWarning($"[镜中试炼][校验] {issue.message}", issue.context);
                        break;
                    default:
                        Debug.Log($"[镜中试炼][校验] {issue.message}", issue.context);
                        break;
                }
            }

            EditorUtility.DisplayDialog("关卡校验", sb.ToString(), "确定");
        }
    }
}
