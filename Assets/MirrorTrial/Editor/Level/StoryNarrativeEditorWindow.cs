using System;
using System.Collections.Generic;
using System.Linq;
using MirrorTrial.Level;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public sealed class StoryNarrativeEditorWindow : EditorWindow
    {
        const string FontPath = "Assets/MirrorTrial/Mod Assets/Mod Resources/Fonts/ZCOOLKuaiLe-Regular SDF.asset";

        static readonly string[] StepNames =
        {
            "章节标题",
            "镜头特写",
            "内心独白",
            "移动教学",
            "跳跃教学",
            "攻击教学",
            "闪避教学",
            "等待",
            "生命能量教学",
            "系统说明"
        };

        static readonly Color Cyan = new Color(0.22f, 0.82f, 0.95f, 1f);
        static readonly Color DarkPanel = new Color(0.12f, 0.145f, 0.18f, 1f);

        readonly List<StoryTutorialSequence> sequences = new List<StoryTutorialSequence>();
        StoryTutorialSequence selected;
        SerializedObject sequenceObject;
        SerializedObject triggerObject;
        ReorderableList stepList;
        Vector2 sequenceScroll;
        Vector2 detailScroll;
        GUIStyle cardStyle;
        GUIStyle selectedCardStyle;

        [MenuItem("MirrorTrial/剧情/打开关卡剧情编辑器")]
        public static void Open()
        {
            var window = GetWindow<StoryNarrativeEditorWindow>();
            window.titleContent = new GUIContent("关卡剧情编辑器");
            window.minSize = new Vector2(880f, 560f);
            window.Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("关卡剧情编辑器");
            minSize = new Vector2(880f, 560f);
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            EditorApplication.hierarchyChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            EditorApplication.hierarchyChanged -= Refresh;
        }

        void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            selected = null;
            Refresh();
        }

        void OnSelectionChange()
        {
            var sequence = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<StoryTutorialSequence>() : null;
            if (sequence && sequences.Contains(sequence)) Select(sequence);
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawSequenceColumn();
            DrawDivider();
            DrawDetailColumn();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(24f));
            GUILayout.Label($"{SceneManager.GetActiveScene().name}  ·  叙事链", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("新增剧情段落", EditorStyles.toolbarButton, GUILayout.Width(94f)))
                CreateSequence();
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                Refresh();
            if (GUILayout.Button("检查", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                StoryNarrativeValidator.ValidateCurrentSceneFromMenu();
            if (GUILayout.Button("保存场景", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorGUILayout.EndHorizontal();
        }

        void DrawSequenceColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(285f));
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("剧情段落", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("按触发顺序组织整关叙事", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            sequenceScroll = EditorGUILayout.BeginScrollView(sequenceScroll);
            if (sequences.Count == 0)
            {
                EditorGUILayout.HelpBox("这个关卡还没有剧情段落。\n点击上方“新增剧情段落”开始。", MessageType.Info);
            }

            for (var i = 0; i < sequences.Count; i++)
                DrawSequenceCard(sequences[i], i);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(sequences.Count < 2))
            {
                if (GUILayout.Button("按列表顺序自动串联", GUILayout.Height(28f)))
                    ChainInOrder();
            }
            EditorGUILayout.Space(7f);
            EditorGUILayout.EndVertical();
        }

        void DrawSequenceCard(StoryTutorialSequence sequence, int index)
        {
            if (!sequence) return;
            var style = sequence == selected ? selectedCardStyle : cardStyle;
            EditorGUILayout.BeginVertical(style);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label((index + 1).ToString("00"), EditorStyles.miniBoldLabel, GUILayout.Width(24f));
            GUILayout.Label(sequence.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(sequence.Steps.Count + " 步", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            var trigger = sequence.GetComponent<StorySequenceTrigger>();
            var triggerLabel = trigger ? TriggerName(trigger.TriggerMode) : "直接播放 / 未设触发";
            GUILayout.Label("触发：" + triggerLabel, EditorStyles.miniLabel);

            var rect = GUILayoutUtility.GetLastRect();
            var cardRect = new Rect(rect.x - 28f, rect.y - 23f, rect.width + 28f, rect.height + 31f);
            if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
            {
                Select(sequence);
                Event.current.Use();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        void DrawDivider()
        {
            var divider = GUILayoutUtility.GetRect(1f, position.height - 25f, GUILayout.Width(1f));
            EditorGUI.DrawRect(divider, new Color(0f, 0f, 0f, 0.45f));
        }

        void DrawDetailColumn()
        {
            EditorGUILayout.BeginVertical();
            if (!selected || sequenceObject == null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("选择左侧剧情段落开始编辑", CenteredLabel());
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            sequenceObject.Update();
            triggerObject?.Update();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            DrawSequenceHeader();
            DrawTriggerSettings();
            EditorGUILayout.Space(10f);
            DrawStepHeader();
            stepList.DoLayoutList();
            EditorGUILayout.Space(12f);
            DrawPreviewControls();
            EditorGUILayout.EndScrollView();

            if (sequenceObject.ApplyModifiedProperties())
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (triggerObject != null && triggerObject.ApplyModifiedProperties())
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorGUILayout.EndVertical();
        }

        void DrawSequenceHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("段落设置", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("场景定位", GUILayout.Width(72f)))
            {
                Selection.activeGameObject = selected.gameObject;
                EditorGUIUtility.PingObject(selected.gameObject);
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawProperty("displayName", "段落名称");
            DrawProperty("sequenceId", "唯一 ID");
            EditorGUILayout.BeginHorizontal();
            DrawProperty("rememberCompletion", "通关后不再重复");
            DrawProperty("replayInEditor", "编辑器内每次重播");
            EditorGUILayout.EndHorizontal();
            DrawProperty("chineseFont", "中文字体");
            EditorGUILayout.EndVertical();
        }

        void DrawTriggerSettings()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("何时播放", EditorStyles.boldLabel);
            var trigger = selected.GetComponent<StorySequenceTrigger>();
            if (!trigger)
            {
                EditorGUILayout.HelpBox("当前段落没有触发条件，可由其他组件手动播放。", MessageType.None);
                if (GUILayout.Button("添加触发条件", GUILayout.Height(26f)))
                {
                    trigger = Undo.AddComponent<StorySequenceTrigger>(selected.gameObject);
                    triggerObject = new SerializedObject(trigger);
                    triggerObject.FindProperty("sequence").objectReferenceValue = selected;
                    triggerObject.ApplyModifiedProperties();
                }
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var mode = triggerObject.FindProperty("triggerMode");
            EditorGUILayout.PropertyField(mode, new GUIContent("触发方式"));
            DrawTriggerTarget((StorySequenceTriggerMode)mode.enumValueIndex);
            EditorGUILayout.BeginHorizontal();
            DrawTriggerProperty("oneShot", "只触发一次");
            DrawTriggerProperty("delay", "触发延迟");
            EditorGUILayout.EndHorizontal();
            DrawTriggerProperty("nextSequence", "完成后播放下一段");
            DrawTriggerProperty("nextSequenceDelay", "下一段延迟");
            EditorGUILayout.EndVertical();
        }

        void DrawTriggerTarget(StorySequenceTriggerMode mode)
        {
            switch (mode)
            {
                case StorySequenceTriggerMode.PlayerEnter:
                    var box = selected.GetComponent<BoxCollider2D>();
                    if (!box && GUILayout.Button("创建玩家进入区域"))
                    {
                        box = Undo.AddComponent<BoxCollider2D>(selected.gameObject);
                        box.isTrigger = true;
                        box.size = new Vector2(3f, 4f);
                    }
                    if (box)
                    {
                        EditorGUILayout.LabelField("区域位置和大小可直接在 Scene 视图调整。", EditorStyles.miniLabel);
                        if (GUILayout.Button("在 Scene 中调整区域"))
                        {
                            Selection.activeGameObject = selected.gameObject;
                            SceneView.lastActiveSceneView?.FrameSelected();
                        }
                    }
                    break;
                case StorySequenceTriggerMode.EncounterCleared:
                    DrawTriggerProperty("encounter", "目标战斗区");
                    break;
                case StorySequenceTriggerMode.MirrorSmashed:
                case StorySequenceTriggerMode.MirrorCompleted:
                    DrawTriggerProperty("mirrorGate", "目标镜门");
                    break;
                case StorySequenceTriggerMode.PreviousSequenceCompleted:
                    DrawTriggerProperty("previousSequence", "上一剧情段落");
                    break;
            }
        }

        void DrawStepHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("演出步骤", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("可拖动左侧把手调整顺序", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawPreviewControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("试玩", EditorStyles.boldLabel);
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("播放当前段落", GUILayout.Height(30f)))
                {
                    selected.ClearCompletionRecord();
                    selected.Replay();
                }
                if (GUILayout.Button("跳过当前段落", GUILayout.Height(30f)))
                    selected.Skip();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后，可在这里单独播放当前段落。", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        void Select(StoryTutorialSequence sequence)
        {
            selected = sequence;
            sequenceObject = sequence ? new SerializedObject(sequence) : null;
            var trigger = sequence ? sequence.GetComponent<StorySequenceTrigger>() : null;
            triggerObject = trigger ? new SerializedObject(trigger) : null;
            BuildStepList();
            Repaint();
        }

        void BuildStepList()
        {
            if (sequenceObject == null) return;
            var property = sequenceObject.FindProperty("steps");
            stepList = new ReorderableList(sequenceObject, property, true, true, true, true);
            stepList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"步骤数量：{property.arraySize}");
            stepList.elementHeightCallback = index => StepHeight(property.GetArrayElementAtIndex(index));
            stepList.drawElementCallback = DrawStep;
            stepList.onAddDropdownCallback = (rect, list) => ShowAddStepMenu();
        }

        float StepHeight(SerializedProperty element)
        {
            var type = (StoryTutorialStepType)element.FindPropertyRelative("type").enumValueIndex;
            switch (type)
            {
                case StoryTutorialStepType.CameraShot: return 142f;
                case StoryTutorialStepType.Dialogue:
                case StoryTutorialStepType.SystemMessage: return 120f;
                case StoryTutorialStepType.TitleCard: return 112f;
                case StoryTutorialStepType.MoveObjective: return 132f;
                case StoryTutorialStepType.JumpObjective:
                case StoryTutorialStepType.PrimaryAttackObjective:
                case StoryTutorialStepType.DodgeObjective:
                case StoryTutorialStepType.HealthResourceObjective: return 134f;
                default: return 72f;
            }
        }

        void DrawStep(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = stepList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 3f;
            rect.height -= 6f;
            EditorGUI.DrawRect(rect, isActive ? new Color(0.16f, 0.24f, 0.3f, 0.72f) : DarkPanel);
            var inner = new Rect(rect.x + 9f, rect.y + 5f, rect.width - 18f, 19f);
            var typeProperty = element.FindPropertyRelative("type");
            var type = (StoryTutorialStepType)typeProperty.enumValueIndex;
            EditorGUI.LabelField(new Rect(inner.x, inner.y, 28f, inner.height), (index + 1).ToString("00"), EditorStyles.miniBoldLabel);
            typeProperty.enumValueIndex = EditorGUI.Popup(new Rect(inner.x + 30f, inner.y, 145f, inner.height), typeProperty.enumValueIndex, StepNames);
            type = (StoryTutorialStepType)typeProperty.enumValueIndex;
            inner.y += 24f;

            switch (type)
            {
                case StoryTutorialStepType.TitleCard:
                    DrawStepText(element, ref inner, "标题内容", 38f);
                    DrawStepField(element, "duration", "停留时间", ref inner);
                    break;
                case StoryTutorialStepType.CameraShot:
                    DrawStepField(element, "cameraTarget", "镜头目标", ref inner);
                    DrawStepField(element, "cameraSize", "镜头大小", ref inner);
                    DrawStepField(element, "duration", "停留时间", ref inner);
                    DrawTwoStepFields(element, "blendIn", "进入过渡", "blendOut", "退出过渡", ref inner);
                    break;
                case StoryTutorialStepType.Dialogue:
                    EditorGUI.LabelField(new Rect(inner.x, inner.y, inner.width, 19f), "第一人称内心独白，不显示说话人", EditorStyles.miniLabel);
                    inner.y += 22f;
                    DrawStepText(element, ref inner, "独白内容", 46f);
                    break;
                case StoryTutorialStepType.SystemMessage:
                    EditorGUI.LabelField(new Rect(inner.x, inner.y, inner.width, 19f), "玩法规则说明，不属于角色独白", EditorStyles.miniLabel);
                    inner.y += 22f;
                    DrawStepText(element, ref inner, "说明内容", 46f);
                    break;
                case StoryTutorialStepType.MoveObjective:
                    DrawStepField(element, "text", "教学目标", ref inner);
                    DrawStepField(element, "hint", "按键提示", ref inner);
                    DrawStepField(element, "requiredAmount", "移动距离", ref inner);
                    break;
                case StoryTutorialStepType.JumpObjective:
                case StoryTutorialStepType.PrimaryAttackObjective:
                case StoryTutorialStepType.DodgeObjective:
                    DrawStepField(element, "text", "教学目标", ref inner);
                    DrawStepField(element, "hint", "按键提示", ref inner);
                    break;
                case StoryTutorialStepType.HealthResourceObjective:
                    DrawStepField(element, "cameraTarget", "生命能量目标", ref inner);
                    DrawStepField(element, "text", "教学目标", ref inner);
                    DrawStepField(element, "hint", "按键提示", ref inner);
                    break;
                case StoryTutorialStepType.Wait:
                    DrawStepField(element, "duration", "等待时间", ref inner);
                    break;
            }
        }

        void DrawStepField(SerializedProperty element, string propertyName, string label, ref Rect rect)
        {
            var property = element.FindPropertyRelative(propertyName);
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, 19f), property, new GUIContent(label));
            rect.y += 22f;
        }

        void DrawTwoStepFields(SerializedProperty element, string leftName, string leftLabel, string rightName, string rightLabel, ref Rect rect)
        {
            var half = (rect.width - 8f) * 0.5f;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, half, 19f), element.FindPropertyRelative(leftName), new GUIContent(leftLabel));
            EditorGUI.PropertyField(new Rect(rect.x + half + 8f, rect.y, half, 19f), element.FindPropertyRelative(rightName), new GUIContent(rightLabel));
            rect.y += 22f;
        }

        void DrawStepText(SerializedProperty element, ref Rect rect, string label, float height)
        {
            var property = element.FindPropertyRelative("text");
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, height), property, new GUIContent(label));
            rect.y += height + 3f;
        }

        void ShowAddStepMenu()
        {
            var menu = new GenericMenu();
            for (var i = 0; i < StepNames.Length; i++)
            {
                var type = (StoryTutorialStepType)i;
                menu.AddItem(new GUIContent(StepNames[i]), false, () => AddStep(type));
            }
            menu.ShowAsContext();
        }

        void AddStep(StoryTutorialStepType type)
        {
            sequenceObject.Update();
            var array = sequenceObject.FindProperty("steps");
            var index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            var element = array.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("type").enumValueIndex = (int)type;
            element.FindPropertyRelative("speaker").stringValue = string.Empty;
            element.FindPropertyRelative("text").stringValue = DefaultText(type);
            element.FindPropertyRelative("hint").stringValue = DefaultHint(type);
            element.FindPropertyRelative("duration").floatValue = type == StoryTutorialStepType.TitleCard ? 1.5f : 1f;
            element.FindPropertyRelative("requiredAmount").floatValue = type == StoryTutorialStepType.MoveObjective ? 2.5f : 1f;
            element.FindPropertyRelative("cameraTarget").objectReferenceValue = null;
            element.FindPropertyRelative("cameraSize").floatValue = 5.5f;
            element.FindPropertyRelative("blendIn").floatValue = 0.6f;
            element.FindPropertyRelative("blendOut").floatValue = 0.5f;
            sequenceObject.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        void CreateSequence()
        {
            var manager = FindObjectOfType<LevelManager>();
            var parent = manager && manager.GameplayRoot ? manager.GameplayRoot : null;
            var go = new GameObject($"剧情段落_{sequences.Count + 1:00}");
            Undo.RegisterCreatedObjectUndo(go, "新增剧情段落");
            if (parent) go.transform.SetParent(parent);

            var sequence = Undo.AddComponent<StoryTutorialSequence>(go);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            sequence.Configure($"{SceneManager.GetActiveScene().name}_Story_{sequences.Count + 1:00}", font, new List<StoryTutorialStep>(), $"剧情段落 {sequences.Count + 1:00}");
            var serialized = new SerializedObject(sequence);
            serialized.FindProperty("playOnStart").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Refresh();
            Select(sequence);
            Selection.activeGameObject = go;
        }

        void ChainInOrder()
        {
            for (var i = 0; i < sequences.Count; i++)
            {
                var trigger = sequences[i].GetComponent<StorySequenceTrigger>();
                if (!trigger) trigger = Undo.AddComponent<StorySequenceTrigger>(sequences[i].gameObject);
                var serialized = new SerializedObject(trigger);
                serialized.FindProperty("sequence").objectReferenceValue = sequences[i];
                serialized.FindProperty("nextSequence").objectReferenceValue = i + 1 < sequences.Count ? sequences[i + 1] : null;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(trigger);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Select(selected);
        }

        void Refresh()
        {
            sequences.Clear();
            sequences.AddRange(FindObjectsOfType<StoryTutorialSequence>(true)
                .Where(item => item.gameObject.scene == SceneManager.GetActiveScene())
                .OrderBy(item => item.transform.GetSiblingIndex()));

            if (selected && !sequences.Contains(selected)) selected = null;
            if (!selected && sequences.Count > 0) selected = sequences[0];
            Select(selected);
        }

        void DrawProperty(string propertyName, string label)
        {
            EditorGUILayout.PropertyField(sequenceObject.FindProperty(propertyName), new GUIContent(label));
        }

        void DrawTriggerProperty(string propertyName, string label)
        {
            EditorGUILayout.PropertyField(triggerObject.FindProperty(propertyName), new GUIContent(label));
        }

        void EnsureStyles()
        {
            if (cardStyle != null) return;
            cardStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(9, 9, 8, 8) };
            selectedCardStyle = new GUIStyle(cardStyle);
            var selectedTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            selectedTexture.SetPixel(0, 0, new Color(0.12f, 0.35f, 0.44f, 1f));
            selectedTexture.Apply();
            selectedCardStyle.normal.background = selectedTexture;
        }

        static GUIStyle CenteredLabel()
        {
            return new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.65f, 0.7f, 0.75f) }
            };
        }

        static string TriggerName(StorySequenceTriggerMode mode)
        {
            switch (mode)
            {
                case StorySequenceTriggerMode.SceneStart: return "关卡开始";
                case StorySequenceTriggerMode.PlayerEnter: return "玩家进入区域";
                case StorySequenceTriggerMode.EncounterCleared: return "战斗清场";
                case StorySequenceTriggerMode.MirrorSmashed: return "镜门击碎";
                case StorySequenceTriggerMode.MirrorCompleted: return "镜中战斗完成";
                case StorySequenceTriggerMode.PreviousSequenceCompleted: return "上一段完成";
                default: return "手动触发";
            }
        }

        static string DefaultText(StoryTutorialStepType type)
        {
            switch (type)
            {
                case StoryTutorialStepType.TitleCard: return "新章节";
                case StoryTutorialStepType.Dialogue: return "在这里输入第一人称独白……";
                case StoryTutorialStepType.SystemMessage: return "在这里输入玩法说明……";
                case StoryTutorialStepType.MoveObjective: return "移动";
                case StoryTutorialStepType.JumpObjective: return "跳跃";
                case StoryTutorialStepType.PrimaryAttackObjective: return "攻击";
                case StoryTutorialStepType.DodgeObjective: return "闪避";
                case StoryTutorialStepType.HealthResourceObjective: return "打碎生命能量";
                default: return string.Empty;
            }
        }

        static string DefaultHint(StoryTutorialStepType type)
        {
            switch (type)
            {
                case StoryTutorialStepType.MoveObjective: return "A / D 或 ← / →  移动";
                case StoryTutorialStepType.JumpObjective: return "空格  跳跃";
                case StoryTutorialStepType.PrimaryAttackObjective: return "J 或鼠标左键  攻击";
                case StoryTutorialStepType.DodgeObjective: return "左 Shift  闪避";
                case StoryTutorialStepType.HealthResourceObjective: return "J 或鼠标左键  攻击";
                default: return string.Empty;
            }
        }
    }
}
