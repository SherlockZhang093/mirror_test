using System;
using System.Collections.Generic;
using MirrorTrial.Player;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public sealed partial class PlayerInputComboEditorWindow
    {
        static readonly Color TapDecisionColor = new Color(0.32f, 0.86f, 1f);
        static readonly Color HoldDecisionColor = new Color(0.78f, 0.48f, 1f);
        static readonly Color DecisionNodeColor = new Color(0.28f, 0.2f, 0.42f);

        int selectedDecisionIndex = -1;
        int draggingDecisionIndex = -1;
        int connectingFromDecisionIndex = -1;
        int connectingDecisionBranch = -1;
        Vector2 decisionDragOffset;

        sealed class ComboDecisionLayout
        {
            public int index;
            public Rect rect;
        }

        List<ComboDecisionLayout> BuildDecisionLayouts(SerializedProperty decisions)
        {
            var layouts = new List<ComboDecisionLayout>();
            if (decisions == null) return layouts;
            for (var i = 0; i < decisions.arraySize; i++)
            {
                var position = decisions.GetArrayElementAtIndex(i).FindPropertyRelative("graphPosition").vector2Value;
                layouts.Add(new ComboDecisionLayout { index = i, rect = new Rect(position.x, position.y, 132f, 82f) });
            }
            return layouts;
        }

        void DrawDecisionEdges(Rect canvas, List<ComboNodeLayout> moveLayouts,
            List<ComboDecisionLayout> decisionLayouts, SerializedProperty moves, SerializedProperty decisions)
        {
            var moveIndexById = new Dictionary<string, int>();
            for (var i = 0; i < moves.arraySize; i++)
                moveIndexById[moves.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue] = i;

            Handles.BeginGUI();
            for (var i = 0; i < decisions.arraySize; i++)
            {
                var decision = decisions.GetArrayElementAtIndex(i);
                var decisionRect = OffsetRect(decisionLayouts[i].rect, canvas.position);
                var sourceId = decision.FindPropertyRelative("sourceInstanceId").stringValue;
                if (!string.IsNullOrEmpty(sourceId) && moveIndexById.TryGetValue(sourceId, out var sourceIndex))
                {
                    var sourceRect = OffsetRect(moveLayouts[sourceIndex].rect, canvas.position);
                    DrawDecisionBezier(new Vector3(sourceRect.xMax, sourceRect.center.y),
                        new Vector3(decisionRect.xMin, decisionRect.center.y), new Color(0.68f, 0.73f, 0.82f), null);
                }

                DrawDecisionBranch(decisionRect, 0, "tapTargetInstanceId", "点按", TapDecisionColor,
                    decision, moveIndexById, moveLayouts, canvas);
                DrawDecisionBranch(decisionRect, 1, "holdTargetInstanceId", "长按", HoldDecisionColor,
                    decision, moveIndexById, moveLayouts, canvas);
                DrawDecisionBranch(decisionRect, 2, "noInputTargetInstanceId", "无输入", new Color(0.58f, 0.61f, 0.68f),
                    decision, moveIndexById, moveLayouts, canvas);
            }
            Handles.EndGUI();
        }

        void DrawDecisionBranch(Rect decisionRect, int branch, string targetField, string label, Color color,
            SerializedProperty decision, Dictionary<string, int> moveIndexById, List<ComboNodeLayout> moveLayouts, Rect canvas)
        {
            var targetId = decision.FindPropertyRelative(targetField).stringValue;
            if (string.IsNullOrEmpty(targetId) || !moveIndexById.TryGetValue(targetId, out var targetIndex)) return;
            var targetRect = OffsetRect(moveLayouts[targetIndex].rect, canvas.position);
            var start = DecisionOutputCenter(decisionRect, branch);
            var end = new Vector3(targetRect.xMin, targetRect.center.y);
            var runtimeId = decision.FindPropertyRelative("id").stringValue + ":" + DecisionBranchKey(branch);
            var runtime = IsRuntimePreviewDecisionBranch(runtimeId);
            DrawDecisionBezier(start, end, runtime ? new Color(0.35f, 1f, 0.38f) : color, label, runtime ? 5f : 3f);
        }

        static void DrawDecisionBezier(Vector3 start, Vector3 end, Color color, string label, float width = 3f)
        {
            var tangent = Mathf.Max(45f, Mathf.Abs(end.x - start.x) * 0.42f);
            Handles.DrawBezier(start, end, start + Vector3.right * tangent, end + Vector3.left * tangent, color, null, width);
            Handles.color = color;
            Handles.DrawAAConvexPolygon(end, end + new Vector3(-10f, -5f), end + new Vector3(-10f, 5f));
            if (!string.IsNullOrEmpty(label))
            {
                var center = Vector2.Lerp(start, end, 0.52f);
                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = color },
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(new Rect(center.x - 28f, center.y - 11f, 56f, 20f), label, style);
            }
        }

        void DrawDecisionNodes(Rect canvas, List<ComboDecisionLayout> layouts,
            SerializedProperty decisions, string entryDecisionId)
        {
            var evt = Event.current;
            for (var i = 0; i < layouts.Count; i++)
            {
                var decision = decisions.GetArrayElementAtIndex(i);
                var rect = OffsetRect(layouts[i].rect, canvas.position);
                var id = decision.FindPropertyRelative("id").stringValue;
                var isEntry = id == entryDecisionId;
                var isSelected = selectedDecisionIndex == i;
                var isRuntime = runtimePreviewCombat && runtimePreviewCombat.RuntimeCurrentDecisionId == id;
                HandleDecisionMouse(evt, canvas, rect, i, decision, isEntry);

                var center = rect.center;
                var points = new[]
                {
                    new Vector3(center.x, rect.y), new Vector3(rect.xMax, center.y),
                    new Vector3(center.x, rect.yMax), new Vector3(rect.x, center.y)
                };
                Handles.BeginGUI();
                Handles.color = decision.FindPropertyRelative("enabled").boolValue ? DecisionNodeColor : new Color(0.25f, 0.25f, 0.27f);
                Handles.DrawAAConvexPolygon(points);
                Handles.EndGUI();

                var name = decision.FindPropertyRelative("name").stringValue;
                var frame = decision.FindPropertyRelative("decisionFrame").intValue;
                var style = new GUIStyle(EditorStyles.whiteMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                GUI.Label(new Rect(rect.x + 19f, rect.y + 17f, rect.width - 38f, rect.height - 34f),
                    (isEntry ? "起手\n" : string.Empty) + name + "\n判定 " + frame + " 帧", style);

                DrawDecisionPort(rect, -1, new Color(0.72f, 0.78f, 0.88f));
                DrawDecisionPort(rect, 0, TapDecisionColor);
                DrawDecisionPort(rect, 1, HoldDecisionColor);
                DrawDecisionPort(rect, 2, new Color(0.58f, 0.61f, 0.68f));
                DrawDecisionPortLabel(rect, 0, "点按", TapDecisionColor);
                DrawDecisionPortLabel(rect, 1, "长按", HoldDecisionColor);
                DrawDecisionPortLabel(rect, 2, "无输入", new Color(0.68f, 0.71f, 0.78f));
                if (isRuntime) DrawRectOutline(rect, new Color(0.35f, 1f, 0.38f), 4f);
                else if (isSelected) DrawRectOutline(rect, new Color(1f, 0.78f, 0.28f), 3f);
            }
            HandleDecisionConnectionRelease(evt, canvas, layouts);
            if (evt.type == EventType.MouseUp && evt.button == 0) draggingDecisionIndex = -1;
        }

        static void DrawDecisionPort(Rect rect, int branch, Color color)
        {
            Vector2 center;
            if (branch < 0) center = new Vector2(rect.x, rect.center.y);
            else center = DecisionOutputCenter(rect, branch);
            EditorGUI.DrawRect(new Rect(center.x - 6f, center.y - 6f, 12f, 12f), color);
        }

        static void DrawDecisionPortLabel(Rect rect, int branch, string label, Color color)
        {
            var center = DecisionOutputCenter(rect, branch);
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(center.x + 8f, center.y - 9f, 45f, 18f), label, style);
        }

        static Vector3 DecisionOutputCenter(Rect rect, int branch)
        {
            if (branch == 0) return new Vector3(rect.xMax, rect.center.y - 20f);
            if (branch == 1) return new Vector3(rect.xMax, rect.center.y);
            return new Vector3(rect.xMax, rect.center.y + 20f);
        }

        void HandleDecisionMouse(Event evt, Rect canvas, Rect rect, int index,
            SerializedProperty decision, bool isEntry)
        {
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                for (var branch = 0; branch < 3; branch++)
                {
                    var center = DecisionOutputCenter(rect, branch);
                    if (!new Rect(center.x - 10f, center.y - 10f, 20f, 20f).Contains(evt.mousePosition)) continue;
                    connectingFromDecisionIndex = index;
                    connectingDecisionBranch = branch;
                    connectingFromNodeIndex = -1;
                    selectedDecisionIndex = index;
                    selectedTransitionIndex = -1;
                    evt.Use();
                    return;
                }
            }
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                selectedDecisionIndex = index;
                selectedTransitionIndex = -1;
                draggingDecisionIndex = index;
                decisionDragOffset = evt.mousePosition - canvas.position - decision.FindPropertyRelative("graphPosition").vector2Value;
                Undo.RecordObject(combat, "移动输入判定节点");
                evt.Use();
                Repaint();
                return;
            }
            if (evt.type == EventType.MouseDrag && evt.button == 0 && draggingDecisionIndex == index)
            {
                var next = evt.mousePosition - canvas.position - decisionDragOffset;
                decision.FindPropertyRelative("graphPosition").vector2Value = new Vector2(Mathf.Max(12f, next.x), Mathf.Max(12f, next.y));
                EditorUtility.SetDirty(combat);
                evt.Use();
                Repaint();
                return;
            }
            if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
            {
                var captured = index;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("设为起手判定"), isEntry, () => SetEntryDecision(captured));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("删除判定节点"), false, () => DeleteInputDecision(captured));
                menu.ShowAsContext();
                evt.Use();
            }
        }

        void HandleDecisionConnectionRelease(Event evt, Rect canvas, List<ComboDecisionLayout> layouts)
        {
            if (evt.type != EventType.MouseUp || evt.button != 0 || connectingFromDecisionIndex < 0) return;
            var graph = serializedCombat.FindProperty("comboGraphs").GetArrayElementAtIndex(selectedGraphIndex);
            var moves = graph.FindPropertyRelative("instances");
            var target = -1;
            for (var i = 0; i < moves.arraySize; i++)
            {
                var position = moves.GetArrayElementAtIndex(i).FindPropertyRelative("graphPosition").vector2Value;
                var moveRect = OffsetRect(new Rect(position.x, position.y, 145f, 64f), canvas.position);
                if (new Rect(moveRect.x - 12f, moveRect.center.y - 14f, 28f, 28f).Contains(evt.mousePosition))
                {
                    target = i;
                    break;
                }
            }
            var source = connectingFromDecisionIndex;
            var branch = connectingDecisionBranch;
            connectingFromDecisionIndex = -1;
            connectingDecisionBranch = -1;
            if (target >= 0) ConnectDecisionToMove(source, branch, target);
            evt.Use();
            Repaint();
        }

        void DrawSelectedDecisionEditor(SerializedProperty graph, SerializedProperty decision, int index, SerializedProperty moves)
        {
            EditorGUILayout.Space(7f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("◇ 输入判定节点", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    var id = decision.FindPropertyRelative("id").stringValue;
                    var isEntry = graph.FindPropertyRelative("entryDecisionId").stringValue == id;
                    if (GUILayout.Button(isEntry ? "当前起手" : "设为起手判定", GUILayout.Width(100f)))
                        SetEntryDecision(index);
                    if (GUILayout.Button("删除", GUILayout.Width(52f))) { DeleteInputDecision(index); return; }
                }
                EditorGUILayout.PropertyField(decision.FindPropertyRelative("enabled"), new GUIContent("启用节点"));
                EditorGUILayout.PropertyField(decision.FindPropertyRelative("name"), new GUIContent("显示名称"));
                DrawCommandPopup(decision.FindPropertyRelative("input"), new GUIContent("监听输入"));

                var source = decision.FindPropertyRelative("sourceInstanceId");
                var isEntryDecision = graph.FindPropertyRelative("entryDecisionId").stringValue ==
                    decision.FindPropertyRelative("id").stringValue;
                if (isEntryDecision)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField("来源动画", "无（起手判定）");
                    source.stringValue = string.Empty;
                }
                else
                {
                    DrawOptionalMovePopup(source, moves, "来源动画", "未连接");
                }
                if (isEntryDecision)
                {
                    decision.FindPropertyRelative("listenStartFrame").intValue = 0;
                    EditorGUILayout.PropertyField(decision.FindPropertyRelative("decisionFrame"), new GUIContent("等待帧数"));
                    EditorGUILayout.PropertyField(decision.FindPropertyRelative("entryFrameRate"), new GUIContent("判定帧率"));
                    decision.FindPropertyRelative("decisionFrame").intValue =
                        Mathf.Max(1, decision.FindPropertyRelative("decisionFrame").intValue);
                    decision.FindPropertyRelative("entryFrameRate").intValue =
                        Mathf.Max(1, decision.FindPropertyRelative("entryFrameRate").intValue);
                    var frameCount = Mathf.Max(0, decision.FindPropertyRelative("decisionFrame").intValue);
                    var frameRate = Mathf.Max(1, decision.FindPropertyRelative("entryFrameRate").intValue);
                    EditorGUILayout.HelpBox(string.Format("按下后保持待机；{0} 帧内松开立即走点按，保持到结束走长按。当前约 {1:0.000} 秒。",
                        frameCount, frameCount / (float)frameRate), MessageType.Info);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(decision.FindPropertyRelative("listenStartFrame"), new GUIContent("监听开始帧"));
                        EditorGUILayout.PropertyField(decision.FindPropertyRelative("decisionFrame"), new GUIContent("分支判定帧"));
                    }
                    decision.FindPropertyRelative("listenStartFrame").intValue =
                        Mathf.Max(0, decision.FindPropertyRelative("listenStartFrame").intValue);
                    decision.FindPropertyRelative("decisionFrame").intValue = Mathf.Max(
                        decision.FindPropertyRelative("listenStartFrame").intValue,
                        decision.FindPropertyRelative("decisionFrame").intValue);
                    EditorGUILayout.HelpBox("来源动画播放到监听开始帧后接收新输入；判定帧仍按住走长按，已经松开走点按。最后一帧按下也算长按。", MessageType.Info);
                }

                EditorGUILayout.Space(4f);
                GUILayout.Label("分支出口", EditorStyles.boldLabel);
                DrawDecisionTargetRow(decision, moves, "tapTargetInstanceId", "tapRequiresHit", "点按", TapDecisionColor, "未到判定帧就松开");
                DrawDecisionTargetRow(decision, moves, "holdTargetInstanceId", "holdRequiresHit", "长按", HoldDecisionColor, "判定帧仍保持按住");
                DrawDecisionTargetRow(decision, moves, "noInputTargetInstanceId", "noInputRequiresHit", "无输入", new Color(0.58f, 0.61f, 0.68f), "没有按下时默认结束连招");
                EditorGUILayout.LabelField("也可以直接从菱形右侧三个彩色端口拖线到招式节点。", EditorStyles.miniLabel);
            }
        }

        void DrawDecisionTargetRow(SerializedProperty decision, SerializedProperty moves,
            string targetField, string requiresHitField, string label, Color color, string hint)
        {
            var old = GUI.color;
            GUI.color = color;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(42f));
                GUI.color = old;
                DrawOptionalMovePopup(decision.FindPropertyRelative(targetField), moves, GUIContent.none.text, "结束连招");
                EditorGUILayout.PropertyField(decision.FindPropertyRelative(requiresHitField), GUIContent.none, GUILayout.Width(18f));
                GUILayout.Label("需命中", GUILayout.Width(45f));
            }
            GUI.color = old;
            EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
        }

        static void DrawOptionalMovePopup(SerializedProperty id, SerializedProperty moves, string label, string emptyLabel)
        {
            var labels = new string[moves.arraySize + 1];
            labels[0] = emptyLabel;
            var selected = 0;
            for (var i = 0; i < moves.arraySize; i++)
            {
                var move = moves.GetArrayElementAtIndex(i);
                labels[i + 1] = move.FindPropertyRelative("name").stringValue;
                if (move.FindPropertyRelative("id").stringValue == id.stringValue) selected = i + 1;
            }
            selected = string.IsNullOrEmpty(label)
                ? EditorGUILayout.Popup(selected, labels)
                : EditorGUILayout.Popup(label, selected, labels);
            id.stringValue = selected <= 0 ? string.Empty :
                moves.GetArrayElementAtIndex(selected - 1).FindPropertyRelative("id").stringValue;
        }

        void AddInputDecisionAt(Vector2 position)
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "添加输入判定节点");
            var graph = combat.ComboGraphs[selectedGraphIndex];
            graph.inputDecisions.Add(new PlayerComboInputDecision
            {
                id = Guid.NewGuid().ToString("N"),
                name = "点按 / 长按",
                graphPosition = new Vector2(Mathf.Max(12f, position.x), Mathf.Max(12f, position.y)),
                input = PlayerInputCommand.PrimaryAttack,
                listenStartFrame = 0,
                decisionFrame = 2,
                entryFrameRate = 60
            });
            selectedDecisionIndex = graph.inputDecisions.Count - 1;
            selectedTransitionIndex = -1;
            RefreshAfterStructureChange();
        }

        void DeleteInputDecision(int index)
        {
            CommitAndRefresh();
            var graph = combat.ComboGraphs[selectedGraphIndex];
            if (index < 0 || index >= graph.inputDecisions.Count) return;
            Undo.RecordObject(combat, "删除输入判定节点");
            var id = graph.inputDecisions[index].id;
            graph.inputDecisions.RemoveAt(index);
            if (graph.entryDecisionId == id) graph.entryDecisionId = string.Empty;
            selectedDecisionIndex = -1;
            RefreshAfterStructureChange();
        }

        void SetEntryDecision(int index)
        {
            CommitAndRefresh();
            var graph = combat.ComboGraphs[selectedGraphIndex];
            if (index < 0 || index >= graph.inputDecisions.Count) return;
            Undo.RecordObject(combat, "设置起手输入判定");
            graph.inputDecisions[index].sourceInstanceId = string.Empty;
            graph.inputDecisions[index].listenStartFrame = 0;
            graph.entryDecisionId = graph.inputDecisions[index].id;
            RefreshAfterStructureChange();
        }

        void ConnectMoveToDecision(int moveIndex, int decisionIndex)
        {
            CommitAndRefresh();
            var graph = combat.ComboGraphs[selectedGraphIndex];
            if (moveIndex < 0 || moveIndex >= graph.instances.Count || decisionIndex < 0 || decisionIndex >= graph.inputDecisions.Count) return;
            Undo.RecordObject(combat, "连接招式到输入判定");
            var decision = graph.inputDecisions[decisionIndex];
            decision.sourceInstanceId = graph.instances[moveIndex].id;
            if (graph.entryDecisionId == decision.id) graph.entryDecisionId = string.Empty;
            selectedDecisionIndex = decisionIndex;
            RefreshAfterStructureChange();
        }

        void ConnectDecisionToMove(int decisionIndex, int branch, int moveIndex)
        {
            CommitAndRefresh();
            var graph = combat.ComboGraphs[selectedGraphIndex];
            if (decisionIndex < 0 || decisionIndex >= graph.inputDecisions.Count || moveIndex < 0 || moveIndex >= graph.instances.Count) return;
            Undo.RecordObject(combat, "连接输入判定分支");
            var decision = graph.inputDecisions[decisionIndex];
            var targetId = graph.instances[moveIndex].id;
            if (branch == 0) decision.tapTargetInstanceId = targetId;
            else if (branch == 1) decision.holdTargetInstanceId = targetId;
            else decision.noInputTargetInstanceId = targetId;
            selectedDecisionIndex = decisionIndex;
            RefreshAfterStructureChange();
        }

        static string DecisionBranchKey(int branch)
        {
            return branch == 0 ? "tap" : branch == 1 ? "hold" : "none";
        }

        bool IsRuntimePreviewDecisionBranch(string runtimeId)
        {
            return runtimePreviewCombat && runtimePreviewCombat.RuntimeLastTransitionId == runtimeId;
        }
    }
}
