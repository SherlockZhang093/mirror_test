using System.Collections.Generic;
using MirrorTrial.Level;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public static class Level01StoryTutorialSetup
    {
        const string SceneName = "Level_Reality_01";
        const string RootName = "剧情教学_第一幕";
        const string FontPath = "Assets/MirrorTrial/Mod Assets/Mod Resources/Fonts/ZCOOLKuaiLe-Regular SDF.asset";

        [MenuItem("MirrorTrial/关卡/配置第一关剧情新手教程")]
        public static void Setup()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            {
                EditorUtility.DisplayDialog("剧情教学", $"请先打开 {SceneName} 场景。", "知道了");
                return;
            }

            var levelManager = Object.FindObjectOfType<LevelManager>();
            if (!levelManager)
            {
                EditorUtility.DisplayDialog("剧情教学", "当前场景中没有 LevelManager。", "知道了");
                return;
            }

            var root = GameObject.Find(RootName);
            if (!root)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "创建剧情教学");
                root.transform.SetParent(levelManager.GameplayRoot ? levelManager.GameplayRoot : levelManager.transform);
                root.transform.localPosition = Vector3.zero;
            }

            var sequence = root.GetComponent<StoryTutorialSequence>();
            if (!sequence)
                sequence = Undo.AddComponent<StoryTutorialSequence>(root);

            var vistaTarget = FindOrCreateTarget(root.transform, "开场废墟特写", new Vector3(-6.4f, -0.8f, 0f));
            var mirrorGate = Object.FindObjectOfType<MirrorGate>();
            var mirrorTarget = mirrorGate ? mirrorGate.transform : FindOrCreateTarget(root.transform, "镜门特写", new Vector3(5.57f, -1.22f, 0f));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var steps = new List<StoryTutorialStep>
            {
                Title("镜界试炼\n<size=28>第一幕 · 失落的入口</size>", 1.55f),
                Shot(vistaTarget, 5.8f, 1.7f),
                Dialogue("镜中低语", "当镜面开始呼吸，现实便不再可靠。"),
                Dialogue("引路者", "向前走。镜门只会回应仍敢迈步的人。"),
                Objective(StoryTutorialStepType.MoveObjective, "循着微光前进", "A / D 或 ← / →  移动", 2.6f),
                Objective(StoryTutorialStepType.JumpObjective, "越过断裂的石阶", "空格  跳跃"),
                Shot(mirrorTarget, 4.6f, 1.45f),
                Dialogue("镜中低语", "门上没有锁。它在等你证明——你不是倒影。"),
                Objective(StoryTutorialStepType.PrimaryAttackObjective, "回应镜面的试探", "J 或鼠标左键  攻击"),
                Objective(StoryTutorialStepType.DodgeObjective, "从反击中脱身", "左 Shift  闪避"),
                Dialogue("引路者", "很好。镜子可以复制动作，却不能复制你的选择。"),
                Title("教程完成\n<size=25><color=#65EFFF>前往镜门，开始真正的试炼</color></size>", 1.15f)
            };

            Undo.RecordObject(sequence, "配置剧情教学内容");
            sequence.Configure("Level_Reality_01_StoryTutorial_v1", font, steps, "第一幕 · 失落的入口");
            var sequenceSerialized = new SerializedObject(sequence);
            sequenceSerialized.FindProperty("playOnStart").boolValue = false;
            sequenceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var trigger = root.GetComponent<StorySequenceTrigger>();
            if (!trigger) trigger = Undo.AddComponent<StorySequenceTrigger>(root);
            var triggerSerialized = new SerializedObject(trigger);
            triggerSerialized.FindProperty("sequence").objectReferenceValue = sequence;
            triggerSerialized.FindProperty("triggerMode").enumValueIndex = (int)StorySequenceTriggerMode.SceneStart;
            triggerSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);
            EditorUtility.SetDirty(trigger);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log("[MirrorTrial] 第一关剧情教学已配置并保存。", sequence);
        }

        [MenuItem("MirrorTrial/关卡/重置剧情教程完成记录")]
        public static void ResetCompletion()
        {
            PlayerPrefs.DeleteKey("MirrorTrial.StoryTutorial.Level_Reality_01_StoryTutorial_v1");
            PlayerPrefs.Save();
            Debug.Log("[MirrorTrial] 已重置第一关剧情教程完成记录。");
        }

        static Transform FindOrCreateTarget(Transform parent, string objectName, Vector3 position)
        {
            var existing = parent.Find(objectName);
            if (existing)
            {
                existing.position = position;
                return existing;
            }

            var go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, "创建剧情镜头目标");
            go.transform.SetParent(parent);
            go.transform.position = position;
            return go.transform;
        }

        static StoryTutorialStep Title(string text, float duration)
        {
            return new StoryTutorialStep
            {
                type = StoryTutorialStepType.TitleCard,
                text = text,
                duration = duration
            };
        }

        static StoryTutorialStep Shot(Transform target, float cameraSize, float duration)
        {
            return new StoryTutorialStep
            {
                type = StoryTutorialStepType.CameraShot,
                cameraTarget = target,
                cameraSize = cameraSize,
                duration = duration,
                blendIn = 0.65f,
                blendOut = 0.5f
            };
        }

        static StoryTutorialStep Dialogue(string speaker, string text)
        {
            return new StoryTutorialStep
            {
                type = StoryTutorialStepType.Dialogue,
                speaker = speaker,
                text = text
            };
        }

        static StoryTutorialStep Objective(StoryTutorialStepType type, string text, string hint, float amount = 1f)
        {
            return new StoryTutorialStep
            {
                type = type,
                text = text,
                hint = hint,
                requiredAmount = amount
            };
        }
    }
}
