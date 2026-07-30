using System;
using MirrorTrial.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor
{
    public sealed partial class PlayerInputComboEditorWindow
    {
        const string RuntimePreviewScenePath = "Assets/MirrorTrial/Scenes/MirrorTrial_TestGym.unity";

        PlayerCombat runtimePreviewCombat;
        PlayerInputReader runtimePreviewInput;
        double runtimePreviewLastRepaint;
        bool runtimePreviewAutoSync = true;

        void EnableRuntimePreviewBridge()
        {
            EditorApplication.update += TickRuntimePreviewBridge;
        }

        void DisableRuntimePreviewBridge()
        {
            EditorApplication.update -= TickRuntimePreviewBridge;
            ReleaseRuntimePreviewInput();
            runtimePreviewCombat = null;
            runtimePreviewInput = null;
        }

        void HandleRuntimePreviewPlayModeChange(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ConnectRuntimePreview;
            else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                Time.timeScale = 1f;
                runtimePreviewCombat = null;
                runtimePreviewInput = null;
                Repaint();
            }
        }

        void TickRuntimePreviewBridge()
        {
            if (!EditorApplication.isPlaying)
                return;
            if (!runtimePreviewCombat)
                ConnectRuntimePreview();
            if (EditorApplication.timeSinceStartup - runtimePreviewLastRepaint > 0.08d)
            {
                runtimePreviewLastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        void DrawRuntimePreviewPanel()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("\u8fd0\u884c\u65f6\u9884\u89c8 · TestGym", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    runtimePreviewAutoSync = GUILayout.Toggle(runtimePreviewAutoSync, "\u53c2\u6570\u53d8\u5316\u81ea\u52a8\u540c\u6b65", GUILayout.Width(125f));
                    if (!EditorApplication.isPlaying)
                    {
                        if (GUILayout.Button("\u542f\u52a8 TestGym \u9884\u89c8", GUILayout.Width(145f)))
                        {
                            StartRuntimePreview();
                            GUIUtility.ExitGUI();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("\u505c\u6b62\u9884\u89c8", GUILayout.Width(90f)))
                            EditorApplication.isPlaying = false;
                    }
                }

                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.HelpBox("\u542f\u52a8\u540e\u4f1a\u8fdb\u5165 MirrorTrial_TestGym\uff0c\u6280\u80fd\u7f16\u8f91\u5668\u4ecd\u7136\u4fdd\u6301\u6253\u5f00\uff0c\u6240\u6709\u8c03\u6574\u4f1a\u540c\u6b65\u5230\u8fd0\u884c\u4e2d\u7684\u73a9\u5bb6\u3002", MessageType.Info);
                    return;
                }

                if (!runtimePreviewCombat || !runtimePreviewInput)
                {
                    EditorGUILayout.HelpBox("\u6b63\u5728\u8fde\u63a5 TestGym \u4e2d\u7684\u73a9\u5bb6……", MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("\u8f7b\u70b9\u4e3b\u653b\u51fb", GUILayout.Height(28f)))
                        runtimePreviewInput.PreviewTap(PlayerInputCommand.PrimaryAttack);
                    if (GUILayout.Button("\u6309\u4e0b\u4e3b\u653b\u51fb", GUILayout.Height(28f)))
                        runtimePreviewInput.PreviewPress(PlayerInputCommand.PrimaryAttack);
                    if (GUILayout.Button("\u677e\u5f00\u4e3b\u653b\u51fb", GUILayout.Height(28f)))
                        runtimePreviewInput.PreviewRelease(PlayerInputCommand.PrimaryAttack);
                    if (GUILayout.Button("\u91cd\u7f6e\u8fde\u62db", GUILayout.Width(88f), GUILayout.Height(28f)))
                        ResetRuntimePreviewCombo();
                    if (GUILayout.Button("\u7acb\u5373\u540c\u6b65", GUILayout.Width(88f), GUILayout.Height(28f)))
                        SyncRuntimePreview(true);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("\u9884\u89c8\u901f\u5ea6", GUILayout.Width(62f));
                    if (GUILayout.Toggle(Mathf.Approximately(Time.timeScale, 0.25f), "0.25×", EditorStyles.miniButtonLeft, GUILayout.Width(55f))) Time.timeScale = 0.25f;
                    if (GUILayout.Toggle(Mathf.Approximately(Time.timeScale, 0.5f), "0.5×", EditorStyles.miniButtonMid, GUILayout.Width(55f))) Time.timeScale = 0.5f;
                    if (GUILayout.Toggle(Mathf.Approximately(Time.timeScale, 1f), "1×", EditorStyles.miniButtonRight, GUILayout.Width(55f))) Time.timeScale = 1f;
                    GUILayout.Space(12f);
                    GUILayout.Label("\u72b6\u6001: " + runtimePreviewCombat.RuntimePreviewStatus);
                    GUILayout.Label("\u5f53\u524d\u62db\u5f0f: " + (string.IsNullOrEmpty(runtimePreviewCombat.RuntimeCurrentMoveName) ? "—" : runtimePreviewCombat.RuntimeCurrentMoveName));
                }

                var charge = runtimePreviewCombat.RuntimeChargeNormalized;
                var chargeRect = EditorGUILayout.GetControlRect(false, 18f);
                EditorGUI.ProgressBar(chargeRect, charge, string.Format("蓄力 {0:0%}  |  {1}  |  {2:0.00}s  |  伤害 ×{3:0.00}", charge,
                    GetChargeStageLabel(runtimePreviewCombat.RuntimeChargeStage), runtimePreviewCombat.RuntimeChargeSeconds,
                    runtimePreviewCombat.RuntimeDamageMultiplier));
                EditorGUILayout.LabelField("\u753b\u9762\u4f7f\u7528 Game \u89c6\u56fe\uff1b\u672c\u9762\u677f\u8d1f\u8d23\u8f93\u5165\u3001\u6162\u653e\u3001\u70ed\u540c\u6b65\u548c\u8fd0\u884c\u72b6\u6001\u3002", EditorStyles.miniLabel);
            }
        }

        static string GetChargeStageLabel(PlayerChargeStage stage)
        {
            switch (stage)
            {
                case PlayerChargeStage.Charging: return "第一阶段";
                case PlayerChargeStage.LaunchReady: return "第二阶段·击飞就绪";
                case PlayerChargeStage.Full: return "满蓄力";
                default: return "未蓄力";
            }
        }

        void StartRuntimePreview()
        {
            if (serializedCombat != null)
                serializedCombat.ApplyModifiedProperties();
            if (combat)
                EditorUtility.SetDirty(combat);
            if (combat && !PrefabUtility.IsPartOfPrefabAsset(combat.gameObject))
            {
                SaveToPrefab();
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(combat);
                if (source)
                    SetTarget(source.gameObject);
            }
            AssetDatabase.SaveAssets();

            if (SceneManager.GetActiveScene().path != RuntimePreviewScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(RuntimePreviewScenePath, OpenSceneMode.Single);
            }
            EditorApplication.isPlaying = true;
        }

        void ConnectRuntimePreview()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != RuntimePreviewScenePath)
                return;

            var candidates = UnityEngine.Object.FindObjectsOfType<PlayerCombat>(true);
            runtimePreviewCombat = candidates.Length > 0 ? candidates[0] : null;
            runtimePreviewInput = runtimePreviewCombat ? runtimePreviewCombat.GetComponent<PlayerInputReader>() : null;
            if (!runtimePreviewCombat)
                return;

            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(runtimePreviewCombat);
            if (source && combat != source)
                SetTarget(source.gameObject);
            SyncRuntimePreview(true);
            Repaint();
        }

        void SyncRuntimePreviewIfNeeded()
        {
            if (runtimePreviewAutoSync)
                SyncRuntimePreview(true);
        }

        void SyncRuntimePreview(bool resetCombo)
        {
            if (!EditorApplication.isPlaying || !runtimePreviewCombat || !combat || runtimePreviewCombat == combat)
                return;

            if (serializedCombat != null)
                serializedCombat.ApplyModifiedProperties();
            var sourceObject = new SerializedObject(combat);
            sourceObject.Update();
            var runtimeObject = new SerializedObject(runtimePreviewCombat);
            runtimeObject.Update();
            var sourceGraphs = sourceObject.FindProperty("comboGraphs");
            if (sourceGraphs == null)
                return;

            if (resetCombo)
                runtimePreviewCombat.CancelCurrentAction(PlayerActionCancelReason.Hit);
            runtimeObject.CopyFromSerializedProperty(sourceGraphs);
            runtimeObject.ApplyModifiedPropertiesWithoutUndo();
            runtimePreviewCombat.EnsureComboData();
            if (resetCombo)
                runtimePreviewCombat.ClearRuntimePreviewState();
        }

        void ResetRuntimePreviewCombo()
        {
            if (!runtimePreviewCombat)
                return;
            ReleaseRuntimePreviewInput();
            runtimePreviewCombat.CancelCurrentAction(PlayerActionCancelReason.Hit);
            runtimePreviewCombat.ClearRuntimePreviewState();
        }

        void ReleaseRuntimePreviewInput()
        {
            if (runtimePreviewInput)
                runtimePreviewInput.ClearPreviewInput();
        }

        bool IsRuntimePreviewMove(string moveId)
        {
            return runtimePreviewCombat && !string.IsNullOrEmpty(moveId) && runtimePreviewCombat.RuntimeCurrentMoveId == moveId;
        }

        bool IsRuntimePreviewTransition(string transitionId)
        {
            return runtimePreviewCombat && !string.IsNullOrEmpty(transitionId) && runtimePreviewCombat.RuntimeLastTransitionId == transitionId;
        }    }
}
