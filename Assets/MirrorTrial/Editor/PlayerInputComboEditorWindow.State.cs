using System;
using System.Collections.Generic;
using MirrorTrial.Player;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public sealed partial class PlayerInputComboEditorWindow
    {
        static readonly Color StateDecisionColor = new Color(0.18f, 0.42f, 0.34f);
        static readonly Color StateBranchColor = new Color(0.38f, 0.92f, 0.58f);
        static readonly Color StateDefaultColor = new Color(0.72f, 0.75f, 0.82f);

        bool draggingStateDecision;
        Vector2 stateDecisionDragOffset;

        void DrawStateDecisionSettings(SerializedProperty graph, SerializedProperty moves, SerializedProperty decisions)
        {
            var stateDecision = graph.FindPropertyRelative("stateDecision");
            if (stateDecision == null) return;
            EditorGUILayout.Space(5f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var enabled = stateDecision.FindPropertyRelative("enabled");
                EditorGUILayout.PropertyField(enabled, new GUIContent("\u542f\u7528\u5f53\u524d\u72b6\u6001\u5224\u5b9a"));
                if (!enabled.boolValue) return;
                EditorGUILayout.PropertyField(stateDecision.FindPropertyRelative("name"), new GUIContent("\u8282\u70b9\u540d\u79f0"));
                var branches = stateDecision.FindPropertyRelative("branches");
                for (var i = 0; i < branches.arraySize; i++)
                {
                    var branch = branches.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(branch.FindPropertyRelative("state"), GUIContent.none, GUILayout.Width(145f));
                        DrawStateTargetPopup(branch.FindPropertyRelative("targetId"), moves, decisions, "\u76ee\u6807");
                        if (GUILayout.Button("\u5220\u9664", GUILayout.Width(48f)))
                        {
                            branches.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }
                }
                if (GUILayout.Button("\u6dfb\u52a0\u72b6\u6001\u5206\u652f", GUILayout.Width(120f)))
                {
                    branches.InsertArrayElementAtIndex(branches.arraySize);
                    var branch = branches.GetArrayElementAtIndex(branches.arraySize - 1);
                    branch.FindPropertyRelative("state").enumValueIndex = (int)PlayerActionState.Idle;
                    branch.FindPropertyRelative("targetId").stringValue = string.Empty;
                }
                DrawStateTargetPopup(stateDecision.FindPropertyRelative("defaultTargetId"), moves, decisions, "\u9ed8\u8ba4\u76ee\u6807");
                EditorGUILayout.HelpBox("\u6d41\u7a0b\u8fdb\u5165\u8be5\u8282\u70b9\u65f6\u8bfb\u53d6 PlayerStateMachine.CurrentState\uff1b\u547d\u4e2d\u5206\u652f\u540e\u8df3\u8f6c\uff0c\u5426\u5219\u8fdb\u5165\u9ed8\u8ba4\u76ee\u6807\u3002", MessageType.Info);
            }
        }

        static void DrawStateTargetPopup(SerializedProperty target, SerializedProperty moves,
            SerializedProperty decisions, string label)
        {
            var ids = new List<string> { string.Empty };
            var labels = new List<string> { "\u65e0" };
            for (var i = 0; i < moves.arraySize; i++)
            {
                var move = moves.GetArrayElementAtIndex(i);
                ids.Add(move.FindPropertyRelative("id").stringValue);
                labels.Add("\u62db\u5f0f / " + move.FindPropertyRelative("name").stringValue);
            }
            for (var i = 0; i < decisions.arraySize; i++)
            {
                var decision = decisions.GetArrayElementAtIndex(i);
                ids.Add(decision.FindPropertyRelative("id").stringValue);
                labels.Add("\u5224\u5b9a / " + decision.FindPropertyRelative("name").stringValue);
            }
            var selected = Mathf.Max(0, ids.IndexOf(target.stringValue));
            selected = EditorGUILayout.Popup(label, selected, labels.ToArray());
            target.stringValue = ids[selected];
        }

        static Rect GetStateDecisionRect(SerializedProperty decision)
        {
            var position = decision.FindPropertyRelative("graphPosition").vector2Value;
            var branches = decision.FindPropertyRelative("branches");
            var height = Mathf.Max(78f, 54f + (branches.arraySize + 1) * 18f);
            return new Rect(position.x, position.y, 150f, height);
        }

        static void ExpandStateDecisionCanvas(SerializedProperty decision, ref float maxX, ref float maxY)
        {
            if (decision == null || !decision.FindPropertyRelative("enabled").boolValue) return;
            var rect = GetStateDecisionRect(decision);
            maxX = Mathf.Max(maxX, rect.xMax + 65f);
            maxY = Mathf.Max(maxY, rect.yMax + 40f);
        }

        void DrawStateDecisionEdges(Rect canvas, SerializedProperty decision,
            List<ComboNodeLayout> moveLayouts, List<ComboDecisionLayout> decisionLayouts,
            SerializedProperty moves, SerializedProperty inputDecisions)
        {
            if (decision == null || !decision.FindPropertyRelative("enabled").boolValue) return;
            var moveRects = new Dictionary<string, Rect>();
            for (var i = 0; i < moves.arraySize; i++)
                moveRects[moves.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue] =
                    OffsetRect(moveLayouts[i].rect, canvas.position);
            var decisionRects = new Dictionary<string, Rect>();
            for (var i = 0; i < inputDecisions.arraySize; i++)
                decisionRects[inputDecisions.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue] =
                    OffsetRect(decisionLayouts[i].rect, canvas.position);

            var rect = OffsetRect(GetStateDecisionRect(decision), canvas.position);
            var branches = decision.FindPropertyRelative("branches");
            Handles.BeginGUI();
            for (var i = 0; i < branches.arraySize; i++)
            {
                var branch = branches.GetArrayElementAtIndex(i);
                var state = (PlayerActionState)branch.FindPropertyRelative("state").enumValueIndex;
                DrawStateTargetEdge(rect, i, branches.arraySize + 1,
                    branch.FindPropertyRelative("targetId").stringValue, state.ToString(), StateBranchColor,
                    moveRects, decisionRects);
            }
            DrawStateTargetEdge(rect, branches.arraySize, branches.arraySize + 1,
                decision.FindPropertyRelative("defaultTargetId").stringValue, "Default", StateDefaultColor,
                moveRects, decisionRects);
            Handles.EndGUI();
        }

        static void DrawStateTargetEdge(Rect sourceRect, int index, int count, string targetId,
            string label, Color color, Dictionary<string, Rect> moveRects, Dictionary<string, Rect> decisionRects)
        {
            Rect targetRect;
            if (!moveRects.TryGetValue(targetId, out targetRect) && !decisionRects.TryGetValue(targetId, out targetRect))
                return;
            var start = new Vector3(sourceRect.xMax, sourceRect.y + (index + 1f) * sourceRect.height / (count + 1f));
            var end = new Vector3(targetRect.xMin, targetRect.center.y);
            DrawDecisionBezier(start, end, color, label);
        }

        void DrawStateDecisionNode(Rect canvas, SerializedProperty decision)
        {
            if (decision == null || !decision.FindPropertyRelative("enabled").boolValue) return;
            var rect = OffsetRect(GetStateDecisionRect(decision), canvas.position);
            HandleStateDecisionMouse(rect, canvas, decision);
            EditorGUI.DrawRect(rect, StateDecisionColor);
            DrawRectOutline(rect, new Color(0.42f, 1f, 0.64f), 2f);
            EditorGUI.DrawRect(new Rect(rect.x - 6f, rect.center.y - 6f, 12f, 12f),
                new Color(0.72f, 0.78f, 0.88f));
            var branches = decision.FindPropertyRelative("branches");
            var title = decision.FindPropertyRelative("name").stringValue;
            var style = new GUIStyle(EditorStyles.whiteMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            GUI.Label(new Rect(rect.x + 8f, rect.y + 7f, rect.width - 16f, 34f),
                "\u72b6\u6001\u5224\u5b9a\n" + title, style);
            for (var i = 0; i < branches.arraySize; i++)
            {
                var state = (PlayerActionState)branches.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("state").enumValueIndex;
                GUI.Label(new Rect(rect.x + 10f, rect.y + 42f + i * 17f, rect.width - 20f, 17f),
                    state + "  \u2192", EditorStyles.whiteMiniLabel);
            }
            GUI.Label(new Rect(rect.x + 10f, rect.y + 42f + branches.arraySize * 17f, rect.width - 20f, 17f),
                "Default  \u2192", EditorStyles.whiteMiniLabel);
        }

        void HandleStateDecisionMouse(Rect rect, Rect canvas, SerializedProperty decision)
        {
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                draggingStateDecision = true;
                stateDecisionDragOffset = evt.mousePosition - canvas.position -
                    decision.FindPropertyRelative("graphPosition").vector2Value;
                Undo.RecordObject(combat, "\u79fb\u52a8\u72b6\u6001\u5224\u5b9a\u8282\u70b9");
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && evt.button == 0 && draggingStateDecision)
            {
                var next = evt.mousePosition - canvas.position - stateDecisionDragOffset;
                decision.FindPropertyRelative("graphPosition").vector2Value =
                    new Vector2(Mathf.Max(12f, next.x), Mathf.Max(12f, next.y));
                EditorUtility.SetDirty(combat);
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0)
                draggingStateDecision = false;
        }

        static void ClearStateDecisionTarget(PlayerComboGraph graph, string id)
        {
            if (graph.stateDecision == null) return;
            if (graph.stateDecision.defaultTargetId == id) graph.stateDecision.defaultTargetId = string.Empty;
            for (var i = 0; i < graph.stateDecision.branches.Count; i++)
                if (graph.stateDecision.branches[i].targetId == id)
                    graph.stateDecision.branches[i].targetId = string.Empty;
        }

        static void ArrangeStateDecision(PlayerComboGraph graph)
        {
            if (graph.stateDecision == null || !graph.stateDecision.enabled) return;
            var entryDecision = graph.inputDecisions.Find(decision => decision != null && decision.id == graph.entryDecisionId);
            for (var i = 0; i < graph.instances.Count; i++)
                graph.instances[i].graphPosition += Vector2.right * 370f;
            for (var i = 0; i < graph.inputDecisions.Count; i++)
                if (graph.inputDecisions[i] != entryDecision)
                    graph.inputDecisions[i].graphPosition += Vector2.right * 370f;
            graph.stateDecision.graphPosition = entryDecision != null
                ? entryDecision.graphPosition + new Vector2(190f, -5f)
                : new Vector2(18f, 28f);
        }

        void DrawBowComboMoveEditor(SerializedProperty node, SerializedProperty move, int index)
        {
            var bow = node.FindPropertyRelative("bowShot");
            if (bow == null) return;
            bow.FindPropertyRelative("enabled").boolValue = true;
            move.FindPropertyRelative("moveCategory").enumValueIndex = (int)PlayerMoveCategory.Bow;
            move.FindPropertyRelative("animationState").enumValueIndex = (int)PlayerActionState.BowDraw;
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("\u84c4\u529b\u5c04\u7bad\uff08\u590d\u5408\u62db\u5f0f\uff09", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("\u5220\u9664\u8fd9\u4e00\u62db", EditorStyles.toolbarButton, GUILayout.Width(86f)))
                {
                    DeleteComboMove(index);
                    return;
                }
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(node.FindPropertyRelative("name"), new GUIContent("\u62db\u5f0f\u540d\u79f0"));
                move.FindPropertyRelative("name").stringValue = node.FindPropertyRelative("name").stringValue;
                EditorGUILayout.PropertyField(bow.FindPropertyRelative("drawClip"), new GUIContent("\u62c9\u5f13\u52a8\u753b"));
                EditorGUILayout.PropertyField(bow.FindPropertyRelative("fullDrawClip"), new GUIContent("\u84c4\u529b\u59ff\u52bf\u52a8\u753b"));
                EditorGUILayout.PropertyField(bow.FindPropertyRelative("fireClip"), new GUIContent("\u5c04\u51fb\u52a8\u753b"));
                move.FindPropertyRelative("animationClip").objectReferenceValue =
                    bow.FindPropertyRelative("drawClip").objectReferenceValue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(bow.FindPropertyRelative("minimumChargeTime"), new GUIContent("\u6700\u77ed\u84c4\u529b"));
                    EditorGUILayout.PropertyField(bow.FindPropertyRelative("maximumChargeTime"), new GUIContent("\u6ee1\u84c4\u529b"));
                    EditorGUILayout.PropertyField(bow.FindPropertyRelative("recovery"), new GUIContent("\u540e\u6447"));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(bow.FindPropertyRelative("minimumDamage"), new GUIContent("\u6700\u4f4e\u4f24\u5bb3"));
                    EditorGUILayout.PropertyField(bow.FindPropertyRelative("maximumDamage"), new GUIContent("\u6ee1\u84c4\u529b\u4f24\u5bb3"));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(bow.FindPropertyRelative("minimumSpeed"), new GUIContent("\u6700\u4f4e\u7bad\u901f"));
                    EditorGUILayout.PropertyField(bow.FindPropertyRelative("maximumSpeed"), new GUIContent("\u6ee1\u84c4\u529b\u7bad\u901f"));
                }
                EditorGUILayout.PropertyField(bow.FindPropertyRelative("range"), new GUIContent("\u5c04\u7a0b"));
                EditorGUILayout.PropertyField(bow.FindPropertyRelative("knockback"), new GUIContent("\u51fb\u9000"));
                EditorGUILayout.PropertyField(bow.FindPropertyRelative("hitStop"), new GUIContent("\u547d\u4e2d\u987f\u5e27"));
                EditorGUILayout.PropertyField(bow.FindPropertyRelative("hitFlashType"),
                    new GUIContent("\u53d7\u51fb\u95ea\u5149", "\u7bad\u547d\u4e2d\u654c\u5175\u65f6\u5168\u8eab\u95ea\u767d\u6216\u95ea\u7ea2\u3002"));
                DrawCommandPopup(bow.FindPropertyRelative("cancelInput"), new GUIContent("\u53d6\u6d88\u8f93\u5165"));
                EditorGUILayout.HelpBox("\u4e00\u5957\u5b8c\u6574\u6d41\u7a0b\uff1a\u6309\u4e0b\u64ad\u653e\u62c9\u5f13\uff0c\u62c9\u5f13\u7ed3\u675f\u540e\u4fdd\u6301\u84c4\u529b\u59ff\u52bf\uff0c\u677e\u5f00\u540e\u64ad\u653e\u5c04\u51fb\u5e76\u53d1\u5c04\u7bad\u77e2\u3002", MessageType.Info);
            }
            selectedComboIndex = index;
            DrawBowAnimationPreview(bow);
        }

        int selectedBowPreviewPhase;

        void DrawBowAnimationPreview(SerializedProperty bow)
        {
            var clipProperties = new[]
            {
                bow.FindPropertyRelative("drawClip"),
                bow.FindPropertyRelative("fullDrawClip"),
                bow.FindPropertyRelative("fireClip")
            };
            var labels = new[] { "\u62c9\u5f13", "\u84c4\u529b\u59ff\u52bf", "\u5c04\u51fb" };
            selectedBowPreviewPhase = Mathf.Clamp(selectedBowPreviewPhase, 0, clipProperties.Length - 1);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("\u5f13\u7bad\u9010\u5e27\u52a8\u753b\u9884\u89c8", EditorStyles.boldLabel);
                var nextPhase = GUILayout.Toolbar(selectedBowPreviewPhase, labels);
                if (nextPhase != selectedBowPreviewPhase)
                {
                    selectedBowPreviewPhase = nextPhase;
                    currentFrame = 0;
                    animationPlaying = false;
                }

                var clip = clipProperties[selectedBowPreviewPhase].objectReferenceValue as AnimationClip;
                animationPreviewOverride = clip;
                if (!clip)
                {
                    EditorGUILayout.HelpBox(labels[selectedBowPreviewPhase] + "\u52a8\u753b\u672a\u6307\u5b9a\uff0c\u8bf7\u5148\u5728\u4e0a\u65b9\u7ed1\u5b9a AnimationClip\u3002", MessageType.Warning);
                    return;
                }

                var frameRate = Mathf.Max(1, Mathf.RoundToInt(clip.frameRate));
                var maxFrame = Mathf.Max(0, Mathf.CeilToInt(clip.length * frameRate));
                currentFrame = Mathf.Clamp(currentFrame, 0, maxFrame);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("|<", GUILayout.Width(30f)))
                    {
                        animationPlaying = false;
                        currentFrame = 0;
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    if (GUILayout.Button(animationPlaying ? "||" : ">", GUILayout.Width(30f)))
                    {
                        animationPlaying = !animationPlaying;
                        lastAnimationUpdate = EditorApplication.timeSinceStartup;
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    if (GUILayout.Button("<", GUILayout.Width(28f)))
                    {
                        animationPlaying = false;
                        currentFrame = Mathf.Max(0, currentFrame - 1);
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    if (GUILayout.Button(">", GUILayout.Width(28f)))
                    {
                        animationPlaying = false;
                        currentFrame = Mathf.Min(maxFrame, currentFrame + 1);
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    EditorGUI.BeginChangeCheck();
                    currentFrame = EditorGUILayout.IntSlider(new GUIContent("\u5f53\u524d\u5e27"), currentFrame, 0, maxFrame);
                    if (EditorGUI.EndChangeCheck())
                    {
                        animationPlaying = false;
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                }
                EditorGUILayout.LabelField(
                    $"{clip.name}  {clip.length:0.###}s  {frameRate} FPS  \u5171 {maxFrame + 1} \u5e27",
                    EditorStyles.miniLabel);
                EditorGUILayout.HelpBox("\u8fd9\u91cc\u5206\u6bb5\u9884\u89c8\u540c\u4e00\u6b21\u6309\u4f4f/\u677e\u5f00\u6d41\u7a0b\u4e2d\u7684\u4e09\u4e2a\u9636\u6bb5\uff0c\u5b83\u4eec\u4e0d\u662f\u4e09\u4e2a\u72ec\u7acb\u8fde\u62db\u3002", MessageType.None);
            }
        }
    }
}
