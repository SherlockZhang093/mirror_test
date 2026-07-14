using System;
using System.Collections.Generic;
using MirrorTrial.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MirrorTrial.Editor
{
    [CustomEditor(typeof(PlayerAnimationDriver))]
    public sealed class PlayerAnimationDriverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("打开玩家动画编辑器", GUILayout.Height(30f)))
                PlayerAnimationEditorWindow.Open((PlayerAnimationDriver)target);
            EditorGUILayout.Space(4f);
            DrawDefaultInspector();
        }
    }

    public sealed class PlayerAnimationEditorWindow : EditorWindow
    {
        enum Category { All, Locomotion, Core, Bow, Combo, Unarmed, Sword }
        enum Tab { Table, Preview, Flow }
        struct AnimatorStateInfo { public string name; public AnimationClip clip; }

        static readonly string[] TabLabels = { "配置表", "动画预览", "流向图" };
        static readonly string[] CategoryLabels = { "全部", "移动", "核心", "弓", "连击", "徒手", "剑" };
        static readonly PlayerAnimationTimingSource[] TimingSourceValues = { PlayerAnimationTimingSource.ClipDuration, PlayerAnimationTimingSource.AttackWindow, PlayerAnimationTimingSource.CastWindow, PlayerAnimationTimingSource.DashDuration, PlayerAnimationTimingSource.HurtLock };
        static readonly string[] TimingSourceLabels = { "使用动画片段时长", "匹配攻击窗口", "匹配施法窗口", "匹配冲刺时长", "匹配受击锁定" };

        PlayerAnimationDriver driver;
        SerializedObject serializedDriver;
        Vector2 scroll;
        Vector2 flowScroll;
        float flowZoom = 1f;
        string search = string.Empty;
        Category category;
        bool errorsOnly;
        Tab tab;
        PlayerActionState previewState = PlayerActionState.Idle;
        float previewTime;
        bool previewPlaying;
        double lastPreviewUpdate;
        readonly List<AnimatorStateInfo> animatorStates = new List<AnimatorStateInfo>();
        string[] animatorStateNames = new string[0];

        static readonly PlayerActionState[] ExternalActionStates =
        {
            PlayerActionState.Attack, PlayerActionState.Cast, PlayerActionState.Dash, PlayerActionState.Hurt,
            PlayerActionState.Dead, PlayerActionState.BowDraw, PlayerActionState.BowAim, PlayerActionState.BowFull,
            PlayerActionState.BowFire, PlayerActionState.ComboAttackA, PlayerActionState.ComboAttackB,
            PlayerActionState.ComboAttackC, PlayerActionState.ComboAttackD, PlayerActionState.PunchA,
            PlayerActionState.PunchB, PlayerActionState.PunchC, PlayerActionState.KickA, PlayerActionState.KickB,
            PlayerActionState.KickC, PlayerActionState.SwordStandingSlash, PlayerActionState.SwordRunSlash,
            PlayerActionState.SwordGuard, PlayerActionState.SwordGuardImpact, PlayerActionState.SwordSprintSlash,
            PlayerActionState.CrouchSlash
        };

        [MenuItem("Tools/镜像试炼/战斗/玩家动画编辑器")]
        public static void OpenFromMenu()
        {
            Open(Selection.activeGameObject ? Selection.activeGameObject.GetComponent<PlayerAnimationDriver>() : null);
        }

        public static void Open(PlayerAnimationDriver target)
        {
            var window = GetWindow<PlayerAnimationEditorWindow>();
            window.titleContent = new GUIContent("玩家动画");
            window.minSize = new Vector2(980f, 520f);
            window.SetTarget(target);
            window.Show();
        }

        void OnEnable()
        {
            EditorApplication.update += TickPreview;
            if (!driver && Selection.activeGameObject)
                SetTarget(Selection.activeGameObject.GetComponent<PlayerAnimationDriver>());
        }

        void OnDisable()
        {
            EditorApplication.update -= TickPreview;
            StopPreview();
        }

        void OnSelectionChange()
        {
            if (!Selection.activeGameObject) return;
            var selectedDriver = Selection.activeGameObject.GetComponent<PlayerAnimationDriver>();
            if (selectedDriver) SetTarget(selectedDriver);
        }

        void SetTarget(PlayerAnimationDriver target)
        {
            StopPreview();
            driver = target;
            serializedDriver = driver ? new SerializedObject(driver) : null;
            RefreshAnimatorStates();
            Repaint();
        }

        void OnGUI()
        {
            DrawTargetToolbar();
            if (!driver || serializedDriver == null)
            {
                EditorGUILayout.HelpBox("\u8bf7\u9009\u62e9\u5e26\u6709 PlayerAnimationDriver \u7684\u6e38\u620f\u5bf9\u8c61\u3002", MessageType.Info);
                return;
            }
            serializedDriver.Update();
            DrawSettings();
            tab = (Tab)GUILayout.Toolbar((int)tab, TabLabels, GUILayout.Height(26f));
            EditorGUILayout.Space(4f);
            if (tab == Tab.Preview) DrawPreview();
            else if (tab == Tab.Flow) DrawFlow();
            else { DrawFilters(); DrawTable(); }
            if (serializedDriver.ApplyModifiedProperties()) EditorUtility.SetDirty(driver);
        }        void DrawTargetToolbar()
        {
            EditorGUI.BeginChangeCheck();
            var next = (PlayerAnimationDriver)EditorGUILayout.ObjectField("\u73a9\u5bb6", driver, typeof(PlayerAnimationDriver), true);
            if (EditorGUI.EndChangeCheck()) SetTarget(next);
        }

        void DrawSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var animatorProperty = serializedDriver.FindProperty("animator");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(animatorProperty, new GUIContent("\u52a8\u753b\u5668"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedDriver.ApplyModifiedProperties();
                    RefreshAnimatorStates();
                    serializedDriver.Update();
                }
                EditorGUILayout.PropertyField(serializedDriver.FindProperty("fadeDuration"), new GUIContent("\u8fc7\u6e21\u65f6\u95f4"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("\u5237\u65b0\u52a8\u753b\u5668", GUILayout.Width(130f))) RefreshAnimatorStates();
                    if (GUILayout.Button("\u9009\u4e2d\u63a7\u5236\u5668", GUILayout.Width(130f)))
                    {
                        var animator = GetAnimator();
                        if (animator && animator.runtimeAnimatorController) Selection.activeObject = animator.runtimeAnimatorController;
                    }
                }
            }
        }

        void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(180f));
                category = (Category)EditorGUILayout.Popup((int)category, CategoryLabels, EditorStyles.toolbarPopup, GUILayout.Width(110f));
                errorsOnly = GUILayout.Toggle(errorsOnly, "\u53ea\u770b\u9519\u8bef", EditorStyles.toolbarButton, GUILayout.Width(90f));
            }
        }

        void DrawTable()
        {
            var list = serializedDriver.FindProperty("animations");
            if (list == null) return;
            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var tuning = driver.GetComponent<PlayerTuning>();
            var displayed = 0;
            var missing = 0;
            for (var i = 0; i < list.arraySize; i++)
            {
                var row = list.GetArrayElementAtIndex(i);
                var actionProperty = row.FindPropertyRelative("action");
                var stateProperty = row.FindPropertyRelative("animatorState");
                if (actionProperty == null || stateProperty == null) continue;
                var action = (PlayerActionState)actionProperty.enumValueIndex;
                var stateIndex = FindAnimatorState(stateProperty.stringValue);
                var hasState = stateIndex >= 0;
                if (!PassesFilter(action, stateProperty.stringValue, hasState)) continue;
                displayed++;
                if (!hasState) missing++;
                DrawRow(row, action, stateProperty, stateIndex, tuning, displayed % 2 == 0);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField(displayed + " \u4e2a\u72b6\u6001\u663e\u793a  |  " + missing + " \u4e2a\u7f3a\u5931", EditorStyles.miniLabel);
        }

        void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("\u52a8\u4f5c\u72b6\u6001", EditorStyles.miniBoldLabel, GUILayout.Width(145f));
                GUILayout.Label("\u52a8\u753b\u5668\u72b6\u6001", EditorStyles.miniBoldLabel, GUILayout.MinWidth(180f));
                GUILayout.Label("\u7247\u6bb5", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
                GUILayout.Label("\u6e90\u65f6\u957f", EditorStyles.miniBoldLabel, GUILayout.Width(62f));
                GUILayout.Label("\u8ba1\u65f6\u65b9\u5f0f", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
                GUILayout.Label("\u76ee\u6807\u65f6\u957f", EditorStyles.miniBoldLabel, GUILayout.Width(62f));
                GUILayout.Label("\u901f\u5ea6", EditorStyles.miniBoldLabel, GUILayout.Width(55f));
                GUILayout.Space(82f);
            }
        }

        void DrawRow(SerializedProperty row, PlayerActionState action, SerializedProperty stateProperty, int stateIndex, PlayerTuning tuning, bool alternate)
        {
            var oldColor = GUI.backgroundColor;
            if (alternate) GUI.backgroundColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = oldColor;
                GUILayout.Label(EditorGUIUtility.IconContent(stateIndex >= 0 ? "d_greenLight" : "console.erroricon.sml"), GUILayout.Width(18f), GUILayout.Height(18f));
                GUILayout.Label(GetActionLabel(action), GUILayout.Width(123f));
                var selected = Mathf.Max(0, stateIndex + 1);
                EditorGUI.BeginChangeCheck();
                selected = EditorGUILayout.Popup(selected, animatorStateNames, GUILayout.MinWidth(180f));
                if (EditorGUI.EndChangeCheck()) stateProperty.stringValue = selected == 0 ? string.Empty : animatorStates[selected - 1].name;
                var clip = stateIndex >= 0 ? animatorStates[stateIndex].clip : null;
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false, GUILayout.Width(150f));
                var sourceProperty = row.FindPropertyRelative("sourceClipDuration");
                var timingProperty = row.FindPropertyRelative("timingSource");
                if (clip && sourceProperty.floatValue <= 0f) sourceProperty.floatValue = clip.length;
                sourceProperty.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(sourceProperty.floatValue, GUILayout.Width(62f)));
                DrawTimingSourcePopup(timingProperty, GUILayout.Width(110f));
                var targetDuration = GetTargetDuration((PlayerAnimationTimingSource)timingProperty.enumValueIndex, tuning, sourceProperty.floatValue);
                GUILayout.Label(FormatSeconds(targetDuration), GUILayout.Width(62f));
                DrawSpeed(sourceProperty.floatValue, targetDuration);
                if (GUILayout.Button("\u9884\u89c8", GUILayout.Width(72f), GUILayout.Height(18f)))
                {
                    previewState = action;
                    tab = Tab.Preview;
                    StartPreview(action);
                }
            }
            GUI.backgroundColor = oldColor;
        }
        void DrawPreview()
        {
            var list = serializedDriver.FindProperty("animations");
            if (list == null) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                category = (Category)EditorGUILayout.Popup((int)category, CategoryLabels, EditorStyles.toolbarPopup, GUILayout.Width(110f));
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(180f));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!previewPlaying))
                    if (GUILayout.Button("\u505c\u6b62", EditorStyles.toolbarButton, GUILayout.Width(58f))) StopPreview();
            }

            EditorGUILayout.HelpBox(Application.isPlaying
                ? "\u64ad\u653e\u6a21\u5f0f\uff1a\u9884\u89c8\u4f1a\u8d70 ForceState\uff0c\u4e5f\u5c31\u662f\u5b9e\u9645\u7684 PlayerStateMachine \u8bf7\u6c42\u8def\u5f84\u3002"
                : "\u7f16\u8f91\u6a21\u5f0f\uff1a\u9884\u89c8\u4f1a\u628a\u9009\u4e2d\u7684 AnimationClip \u76f4\u63a5\u91c7\u6837\u5230\u573a\u666f\u89d2\u8272\u8eab\u4e0a\u3002", MessageType.Info);

            var selectedRow = FindRow(list, previewState);
            var selectedClip = selectedRow != null ? GetClipForRow(selectedRow) : null;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                previewState = (PlayerActionState)EditorGUILayout.EnumPopup("\u5f53\u524d\u9884\u89c8", previewState);
                using (new EditorGUI.DisabledScope(selectedClip == null && !Application.isPlaying))
                    if (GUILayout.Button(previewPlaying ? "\u91cd\u64ad" : "\u64ad\u653e", GUILayout.Width(78f))) StartPreview(previewState);
                using (new EditorGUI.DisabledScope(!previewPlaying))
                    if (GUILayout.Button("\u505c\u6b62", GUILayout.Width(64f))) StopPreview();
            }

            if (!Application.isPlaying && selectedClip)
            {
                EditorGUI.BeginChangeCheck();
                previewTime = EditorGUILayout.Slider("\u5e27\u65f6\u95f4", previewTime, 0f, Mathf.Max(0.01f, selectedClip.length));
                if (EditorGUI.EndChangeCheck())
                {
                    previewPlaying = false;
                    SamplePreviewClip(selectedClip, previewTime);
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 28f) / 150f));
            var index = 0;
            for (var i = 0; i < list.arraySize; i++)
            {
                var row = list.GetArrayElementAtIndex(i);
                var actionProperty = row.FindPropertyRelative("action");
                var stateProperty = row.FindPropertyRelative("animatorState");
                if (actionProperty == null || stateProperty == null) continue;
                var action = (PlayerActionState)actionProperty.enumValueIndex;
                if (!PassesFilter(action, stateProperty.stringValue, true)) continue;
                if (index % columns == 0) EditorGUILayout.BeginHorizontal();
                var oldColor = GUI.backgroundColor;
                if (action == previewState) GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
                if (GUILayout.Button(GetActionLabel(action), GUILayout.Height(38f), GUILayout.Width(144f)))
                {
                    previewState = action;
                    StartPreview(action);
                }
                GUI.backgroundColor = oldColor;
                index++;
                if (index % columns == 0) EditorGUILayout.EndHorizontal();
            }
            if (index > 0 && index % columns != 0) EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        void StartPreview(PlayerActionState action)
        {
            previewState = action;
            previewTime = 0f;
            previewPlaying = true;
            lastPreviewUpdate = EditorApplication.timeSinceStartup;
            if (Application.isPlaying)
            {
                driver.ForceState(action);
                SceneView.RepaintAll();
                return;
            }
            var row = FindRow(serializedDriver.FindProperty("animations"), action);
            var clip = row != null ? GetClipForRow(row) : null;
            if (!clip) { previewPlaying = false; return; }
            SamplePreviewClip(clip, previewTime);
        }

        void StopPreview()
        {
            if (Application.isPlaying && driver) driver.ClearForcedState(previewState);
            previewPlaying = false;
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            SceneView.RepaintAll();
        }

        void TickPreview()
        {
            if (!previewPlaying || !driver) return;
            var now = EditorApplication.timeSinceStartup;
            var delta = Mathf.Clamp((float)(now - lastPreviewUpdate), 0f, 0.1f);
            lastPreviewUpdate = now;
            if (Application.isPlaying) { Repaint(); return; }
            var row = FindRow(serializedDriver != null ? serializedDriver.FindProperty("animations") : null, previewState);
            var clip = row != null ? GetClipForRow(row) : null;
            if (!clip) { StopPreview(); return; }
            previewTime += delta;
            if (previewTime > clip.length) previewTime = 0f;
            SamplePreviewClip(clip, previewTime);
            Repaint();
        }

        void SamplePreviewClip(AnimationClip clip, float time)
        {
            var animator = GetAnimator();
            if (!animator || !clip) return;
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }
        void DrawFlow()
        {
            var stateMachine = driver.GetComponent<PlayerStateMachine>();
            var current = stateMachine ? stateMachine.CurrentState : PlayerActionState.None;
            var previous = stateMachine ? stateMachine.PreviousState : PlayerActionState.None;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(Application.isPlaying ? "\u8fd0\u884c\u65f6\u6d41\u5411" : "\u6d41\u5411\u9884\u89c8", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
                GUILayout.Label("\u7f29\u653e", GUILayout.Width(36f));
                flowZoom = GUILayout.HorizontalSlider(flowZoom, 0.55f, 1.8f, GUILayout.Width(160f));
                flowZoom = EditorGUILayout.FloatField(flowZoom, GUILayout.Width(44f));
                flowZoom = Mathf.Clamp(flowZoom, 0.55f, 1.8f);
                if (GUILayout.Button("\u91cd\u7f6e\u89c6\u56fe", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                {
                    flowZoom = 1f;
                    flowScroll = Vector2.zero;
                }
                GUILayout.FlexibleSpace();
                if (Application.isPlaying)
                    GUILayout.Label(GetActionLabel(previous) + " -> " + GetActionLabel(current), EditorStyles.miniLabel, GUILayout.Width(220f));
            }

            var viewWidth = Mathf.Max(position.width - 28f, 900f);
            var canvasWidth = Mathf.Max(viewWidth, 1260f * flowZoom);
            var canvasHeight = 540f * flowZoom;
            flowScroll = EditorGUILayout.BeginScrollView(flowScroll);
            var canvas = GUILayoutUtility.GetRect(canvasWidth, canvasHeight, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            var nodes = BuildFlowNodes(canvas, flowZoom);

            Handles.BeginGUI();
            DrawEdge(nodes, PlayerActionState.Idle, PlayerActionState.Run, "\u79fb\u52a8\u8f93\u5165", previous, current);
            DrawEdge(nodes, PlayerActionState.Run, PlayerActionState.Idle, "\u505c\u6b62\u79fb\u52a8", previous, current);
            DrawEdge(nodes, PlayerActionState.Idle, PlayerActionState.JumpRise, "\u7a7a\u4e2d\u4e0a\u5347", previous, current);
            DrawEdge(nodes, PlayerActionState.Run, PlayerActionState.JumpRise, "\u7a7a\u4e2d\u4e0a\u5347", previous, current);
            DrawEdge(nodes, PlayerActionState.JumpRise, PlayerActionState.JumpFall, "\u4e0b\u843d", previous, current);
            DrawEdge(nodes, PlayerActionState.JumpFall, PlayerActionState.Land, "\u843d\u5730", previous, current);
            DrawEdge(nodes, PlayerActionState.Land, PlayerActionState.Idle, "\u7f13\u51b2\u7ed3\u675f", previous, current);
            DrawEdge(nodes, PlayerActionState.Land, PlayerActionState.Run, "\u79fb\u52a8\u8f93\u5165", previous, current);
            for (var i = 0; i < ExternalActionStates.Length; i++)
            {
                DrawEdge(nodes, PlayerActionState.Idle, ExternalActionStates[i], "\u8bf7\u6c42\u52a8\u4f5c", previous, current);
                DrawEdge(nodes, ExternalActionStates[i], PlayerActionState.Idle, "\u91ca\u653e\u52a8\u4f5c", previous, current);
            }
            if (Application.isPlaying && previous != current)
                DrawEdge(nodes, previous, current, "\u8fd0\u884c\u65f6", previous, current);
            Handles.EndGUI();

            foreach (var pair in nodes)
                DrawNode(pair.Value, pair.Key, current, previous);
            EditorGUILayout.EndScrollView();
            if (Application.isPlaying) Repaint();
        }

        Dictionary<PlayerActionState, Rect> BuildFlowNodes(Rect canvas, float zoom)
        {
            var x = canvas.x + 24f * zoom;
            var y = canvas.y + 22f * zoom;
            var w = 126f * zoom;
            var h = 44f * zoom;
            var nodes = new Dictionary<PlayerActionState, Rect>
            {
                { PlayerActionState.Idle, new Rect(x + 0f * zoom, y + 210f * zoom, w, h) },
                { PlayerActionState.Run, new Rect(x + 180f * zoom, y + 210f * zoom, w, h) },
                { PlayerActionState.JumpRise, new Rect(x + 90f * zoom, y + 70f * zoom, w, h) },
                { PlayerActionState.JumpFall, new Rect(x + 90f * zoom, y + 350f * zoom, w, h) },
                { PlayerActionState.Land, new Rect(x + 360f * zoom, y + 280f * zoom, w, h) }
            };
            for (var i = 0; i < ExternalActionStates.Length; i++)
            {
                var col = i % 5;
                var row = i / 5;
                nodes[ExternalActionStates[i]] = new Rect(x + (520f + col * 140f) * zoom, y + row * 72f * zoom, w, h);
            }
            return nodes;
        }

        void DrawNode(Rect rect, PlayerActionState state, PlayerActionState current, PlayerActionState previous)
        {
            var oldColor = GUI.backgroundColor;
            if (state == current) GUI.backgroundColor = new Color(0.28f, 0.75f, 0.42f, 1f);
            else if (state == previous) GUI.backgroundColor = new Color(0.95f, 0.72f, 0.28f, 1f);
            else GUI.backgroundColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            GUI.Box(rect, GetActionLabel(state), EditorStyles.helpBox);
            GUI.backgroundColor = oldColor;
        }

        void DrawEdge(Dictionary<PlayerActionState, Rect> nodes, PlayerActionState fromState, PlayerActionState toState, string label, PlayerActionState previous, PlayerActionState current)
        {
            Rect from;
            Rect to;
            if (!nodes.TryGetValue(fromState, out from) || !nodes.TryGetValue(toState, out to)) return;
            var active = Application.isPlaying && previous == fromState && current == toState;
            var start = new Vector3(from.xMax, from.center.y, 0f);
            var end = new Vector3(to.xMin, to.center.y, 0f);
            if (to.x < from.x)
            {
                start = new Vector3(from.xMin, from.center.y, 0f);
                end = new Vector3(to.xMax, to.center.y, 0f);
            }
            var tangent = Mathf.Max(40f * flowZoom, Mathf.Abs(end.x - start.x) * 0.35f);
            var color = active ? new Color(0.2f, 0.95f, 0.45f, 1f) : new Color(0.55f, 0.65f, 0.78f, 0.55f);
            var width = active ? 4f : 2f;
            Handles.DrawBezier(start, end, start + Vector3.right * tangent, end + Vector3.left * tangent, color, null, width);
            var dir = (end - start).normalized;
            var normal = new Vector3(-dir.y, dir.x, 0f);
            Handles.color = color;
            Handles.DrawAAPolyLine(width, end, end - dir * 10f * flowZoom + normal * 4f * flowZoom);
            Handles.DrawAAPolyLine(width, end, end - dir * 10f * flowZoom - normal * 4f * flowZoom);
            var mid = Vector3.Lerp(start, end, 0.5f);
            GUI.Label(new Rect(mid.x - 36f * flowZoom, mid.y - 10f, 92f, 18f), label, active ? EditorStyles.boldLabel : EditorStyles.miniLabel);
        }
        SerializedProperty FindRow(SerializedProperty list, PlayerActionState action)
        {
            if (list == null) return null;
            for (var i = 0; i < list.arraySize; i++)
            {
                var row = list.GetArrayElementAtIndex(i);
                var actionProperty = row.FindPropertyRelative("action");
                if (actionProperty != null && (PlayerActionState)actionProperty.enumValueIndex == action) return row;
            }
            return null;
        }

        AnimationClip GetClipForRow(SerializedProperty row)
        {
            var stateProperty = row.FindPropertyRelative("animatorState");
            if (stateProperty == null) return null;
            var stateIndex = FindAnimatorState(stateProperty.stringValue);
            return stateIndex >= 0 ? animatorStates[stateIndex].clip : null;
        }

        bool PassesFilter(PlayerActionState action, string stateName, bool hasState)
        {
            if (category != Category.All && GetCategory(action) != category) return false;
            if (errorsOnly && hasState) return false;
            if (string.IsNullOrEmpty(search)) return true;
            return GetActionLabel(action).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || action.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || stateName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static Category GetCategory(PlayerActionState action)
        {
            switch (action)
            {
                case PlayerActionState.Idle:
                case PlayerActionState.Run:
                case PlayerActionState.JumpRise:
                case PlayerActionState.JumpFall:
                case PlayerActionState.Land:
                    return Category.Locomotion;
                case PlayerActionState.BowDraw:
                case PlayerActionState.BowAim:
                case PlayerActionState.BowFull:
                case PlayerActionState.BowFire:
                    return Category.Bow;
                case PlayerActionState.ComboAttackA:
                case PlayerActionState.ComboAttackB:
                case PlayerActionState.ComboAttackC:
                case PlayerActionState.ComboAttackD:
                    return Category.Combo;
                case PlayerActionState.PunchA:
                case PlayerActionState.PunchB:
                case PlayerActionState.PunchC:
                case PlayerActionState.KickA:
                case PlayerActionState.KickB:
                case PlayerActionState.KickC:
                    return Category.Unarmed;
                case PlayerActionState.SwordStandingSlash:
                case PlayerActionState.SwordRunSlash:
                case PlayerActionState.SwordGuard:
                case PlayerActionState.SwordGuardImpact:
                case PlayerActionState.SwordSprintSlash:
                case PlayerActionState.CrouchSlash:
                    return Category.Sword;
                default:
                    return Category.Core;
            }
        }

        static string GetActionLabel(PlayerActionState action)
        {
            switch (action)
            {
                case PlayerActionState.None: return "\u65e0";
                case PlayerActionState.Idle: return "\u5f85\u673a";
                case PlayerActionState.Run: return "\u5954\u8dd1";
                case PlayerActionState.JumpRise: return "\u8df3\u8d77";
                case PlayerActionState.JumpFall: return "\u4e0b\u843d";
                case PlayerActionState.Land: return "\u843d\u5730";
                case PlayerActionState.Attack: return "\u653b\u51fb";
                case PlayerActionState.Cast: return "\u65bd\u6cd5";
                case PlayerActionState.Dash: return "\u51b2\u523a";
                case PlayerActionState.Hurt: return "\u53d7\u51fb";
                case PlayerActionState.Dead: return "\u6b7b\u4ea1";
                case PlayerActionState.BowDraw: return "\u62c9\u5f13";
                case PlayerActionState.BowAim: return "\u7784\u51c6";
                case PlayerActionState.BowFull: return "\u6ee1\u5f13";
                case PlayerActionState.BowFire: return "\u5c04\u7bad";
                case PlayerActionState.ComboAttackA: return "\u8fde\u51fb A";
                case PlayerActionState.ComboAttackB: return "\u8fde\u51fb B";
                case PlayerActionState.ComboAttackC: return "\u8fde\u51fb C";
                case PlayerActionState.ComboAttackD: return "\u8fde\u51fb D";
                case PlayerActionState.PunchA: return "\u51fa\u62f3 A";
                case PlayerActionState.PunchB: return "\u51fa\u62f3 B";
                case PlayerActionState.PunchC: return "\u51fa\u62f3 C";
                case PlayerActionState.KickA: return "\u8e22\u51fb A";
                case PlayerActionState.KickB: return "\u8e22\u51fb B";
                case PlayerActionState.KickC: return "\u8e22\u51fb C";
                case PlayerActionState.SwordStandingSlash: return "\u7ad9\u7acb\u65a9";
                case PlayerActionState.SwordRunSlash: return "\u5954\u8dd1\u65a9";
                case PlayerActionState.SwordGuard: return "\u5251\u9632\u5fa1";
                case PlayerActionState.SwordGuardImpact: return "\u9632\u5fa1\u53cd\u51fb";
                case PlayerActionState.SwordSprintSlash: return "\u75be\u8dd1\u65a9";
                case PlayerActionState.CrouchSlash: return "\u4e0b\u8e72\u65a9";
                default: return ObjectNames.NicifyVariableName(action.ToString());
            }
        }

        void RefreshAnimatorStates()
        {
            animatorStates.Clear();
            var animator = GetAnimator();
            if (animator && animator.runtimeAnimatorController) CollectStates(animator.runtimeAnimatorController);
            animatorStates.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            animatorStateNames = new string[animatorStates.Count + 1];
            animatorStateNames[0] = "<\u65e0>";
            for (var i = 0; i < animatorStates.Count; i++) animatorStateNames[i + 1] = animatorStates[i].name;
        }

        Animator GetAnimator()
        {
            if (!driver) return null;
            if (serializedDriver == null) serializedDriver = new SerializedObject(driver);
            var property = serializedDriver.FindProperty("animator");
            var animator = property != null ? property.objectReferenceValue as Animator : null;
            return animator ? animator : driver.GetComponentInChildren<Animator>(true);
        }

        void CollectStates(RuntimeAnimatorController runtimeController)
        {
            var overrideController = runtimeController as AnimatorOverrideController;
            var controller = overrideController ? overrideController.runtimeAnimatorController as AnimatorController : runtimeController as AnimatorController;
            if (!controller || controller.layers.Length == 0) return;
            var overrides = new Dictionary<AnimationClip, AnimationClip>();
            if (overrideController)
            {
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(pairs);
                for (var i = 0; i < pairs.Count; i++) overrides[pairs[i].Key] = pairs[i].Value;
            }
            CollectStates(controller.layers[0].stateMachine, overrides);
        }

        void CollectStates(AnimatorStateMachine stateMachine, Dictionary<AnimationClip, AnimationClip> overrides)
        {
            var states = stateMachine.states;
            for (var i = 0; i < states.Length; i++)
            {
                var clip = GetFirstClip(states[i].state.motion);
                AnimationClip replacement;
                if (clip && overrides.TryGetValue(clip, out replacement) && replacement) clip = replacement;
                animatorStates.Add(new AnimatorStateInfo { name = states[i].state.name, clip = clip });
            }
            var childMachines = stateMachine.stateMachines;
            for (var i = 0; i < childMachines.Length; i++) CollectStates(childMachines[i].stateMachine, overrides);
        }

        static AnimationClip GetFirstClip(Motion motion)
        {
            var clip = motion as AnimationClip;
            if (clip) return clip;
            var tree = motion as BlendTree;
            if (tree == null) return null;
            var children = tree.children;
            for (var i = 0; i < children.Length; i++)
            {
                clip = GetFirstClip(children[i].motion);
                if (clip) return clip;
            }
            return null;
        }

        int FindAnimatorState(string stateName)
        {
            for (var i = 0; i < animatorStates.Count; i++)
                if (animatorStates[i].name == stateName) return i;
            return -1;
        }

        static float GetTargetDuration(PlayerAnimationTimingSource timingSource, PlayerTuning tuning, float fallback)
        {
            if (!tuning) return fallback;
            switch (timingSource)
            {
                case PlayerAnimationTimingSource.AttackWindow:
                    return tuning.combat.attackStartup + tuning.combat.attackActiveTime + tuning.combat.attackRecovery;
                case PlayerAnimationTimingSource.CastWindow:
                    return tuning.abilities.mirrorBladeStartup + tuning.abilities.mirrorBladeRecovery;
                case PlayerAnimationTimingSource.DashDuration:
                    return tuning.abilities.echoDashDuration;
                case PlayerAnimationTimingSource.HurtLock:
                    return tuning.hurt.hurtLockTime;
                default:
                    return fallback;
            }
        }

        static void DrawSpeed(float sourceDuration, float targetDuration)
        {
            var speed = targetDuration > 0f && sourceDuration > 0f ? sourceDuration / targetDuration : 1f;
            var previousContentColor = GUI.contentColor;
            if (speed < 0.5f || speed > 2f) GUI.contentColor = new Color(1f, 0.35f, 0.3f);
            else if (speed < 0.8f || speed > 1.25f) GUI.contentColor = new Color(1f, 0.72f, 0.2f);
            GUILayout.Label(sourceDuration > 0f ? speed.ToString("0.00") + "x" : "-", GUILayout.Width(55f));
            GUI.contentColor = previousContentColor;
        }

        static void DrawTimingSourcePopup(SerializedProperty property, params GUILayoutOption[] options)
        {
            var current = (PlayerAnimationTimingSource)property.enumValueIndex;
            var selected = 0;
            for (var i = 0; i < TimingSourceValues.Length; i++)
            {
                if (TimingSourceValues[i] == current)
                {
                    selected = i;
                    break;
                }
            }
            selected = EditorGUILayout.Popup(selected, TimingSourceLabels, options);
            property.enumValueIndex = (int)TimingSourceValues[Mathf.Clamp(selected, 0, TimingSourceValues.Length - 1)];
        }
        static string FormatSeconds(float value)
        {
            return value > 0f ? value.ToString("0.###") + "s" : "-";
        }
    }
}
