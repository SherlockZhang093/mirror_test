using System;
using System.Collections.Generic;
using MirrorTrial.Player;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public sealed partial class PlayerInputComboEditorWindow
    {
        int workspacePage;
        int selectedGraphIndex;
        int selectedGraphMoveIndex;
        bool moveCombatExpanded = true;
        bool moveChargeExpanded = true;
        bool moveFeedbackExpanded;
        Vector2 comboGraphScroll;
        bool connectionSettingsExpanded;
        int draggingNodeIndex = -1;
        Vector2 nodeDragOffset;
        int connectingFromNodeIndex = -1;
        int selectedTransitionIndex = -1;
        Rect lastGraphCanvasRect;

        void DrawMoveComboWorkspace()
        {
            workspacePage = GUILayout.Toolbar(workspacePage, new[] { "连招编辑", "角色设置" }, GUILayout.Height(30f));
            EditorGUILayout.Space(6f);
            if (workspacePage == 0)
                DrawComboGraphWorkspace();
            else
                DrawCharacterSettingsWorkspace();
        }

        void DrawCharacterSettingsWorkspace()
        {
            EditorGUILayout.HelpBox("这里集中放置不经常修改的武器槽、武器参数和按键映射。", MessageType.Info);
            DrawWeaponSlots();
            DrawSelectedWeaponSettings();
            DrawInputBindings();
        }

        void DrawComboGraphWorkspace()
        {
            var graphs = serializedCombat.FindProperty("comboGraphs");
            if (graphs == null) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("连招方案", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("新建连招", EditorStyles.toolbarButton, GUILayout.Width(82f))) { AddComboGraph(); return; }
                using (new EditorGUI.DisabledScope(graphs.arraySize == 0))
                    if (GUILayout.Button("删除连招", EditorStyles.toolbarButton, GUILayout.Width(72f))) { DeleteComboGraph(); return; }
            }
            if (graphs.arraySize == 0)
            {
                EditorGUILayout.HelpBox("还没有连招方案。", MessageType.Info);
                return;
            }

            selectedGraphIndex = Mathf.Clamp(selectedGraphIndex, 0, graphs.arraySize - 1);
            selectedGraphIndex = EditorGUILayout.Popup("当前连招", selectedGraphIndex, BuildNameLabels(graphs, "连招"));
            var graph = graphs.GetArrayElementAtIndex(selectedGraphIndex);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(graph.FindPropertyRelative("name"), new GUIContent("名称"));
                    EditorGUILayout.PropertyField(graph.FindPropertyRelative("weaponType"), new GUIContent("所属武器"));
                }
                DrawCommandPopup(graph.FindPropertyRelative("entryInput"), new GUIContent("起手输入"));
            }

            var moves = graph.FindPropertyRelative("instances");
            var links = graph.FindPropertyRelative("transitions");
            DrawComboComposition(graph, moves, links);
            if (moves.arraySize > 0)
            {
                selectedGraphMoveIndex = Mathf.Clamp(selectedGraphMoveIndex, 0, moves.arraySize - 1);
                if (selectedTransitionIndex >= 0 && selectedTransitionIndex < links.arraySize)
                    DrawSelectedConnectionEditor(links.GetArrayElementAtIndex(selectedTransitionIndex), selectedTransitionIndex, moves);
                else
                    DrawSelectedMoveEditor(moves.GetArrayElementAtIndex(selectedGraphMoveIndex), selectedGraphMoveIndex);
            }
        }

        sealed class ComboNodeLayout
        {
            public int index;
            public int depth;
            public Rect rect;
        }

        void DrawComboComposition(SerializedProperty graph, SerializedProperty moves, SerializedProperty links)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("连招路线图", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("\u81ea\u52a8\u6574\u7406", GUILayout.Width(72f))) { AutoArrangeCurrentGraph(); return; }
                if (GUILayout.Button("添加招式", GUILayout.Width(82f))) { AddComboMove(false); return; }
                using (new EditorGUI.DisabledScope(moves.arraySize == 0))
                    if (GUILayout.Button("复制所选", GUILayout.Width(72f))) { AddComboMove(true); return; }
            }
            if (moves.arraySize == 0)
            {
                EditorGUILayout.HelpBox("添加第一招后，连招路线会显示在这里。", MessageType.Info);
                return;
            }

            var labels = BuildNameLabels(moves, "招式");
            var entryId = graph.FindPropertyRelative("entryInstanceId");
            var entryIndex = FindStringIndex(moves, "id", entryId.stringValue);
            entryIndex = EditorGUILayout.Popup("起手招式", Mathf.Max(0, entryIndex), labels);
            entryId.stringValue = moves.GetArrayElementAtIndex(entryIndex).FindPropertyRelative("id").stringValue;

            DrawComboGraphCanvas(moves, links, entryId.stringValue, labels);

            connectionSettingsExpanded = EditorGUILayout.Foldout(connectionSettingsExpanded, "连接参数（需要调整输入窗口时展开）", true, EditorStyles.foldoutHeader);
            if (connectionSettingsExpanded)
                DrawComboTransitions(links, moves);
        }

        void DrawComboGraphCanvas(SerializedProperty moves, SerializedProperty links, string entryId, string[] labels)
        {
            var layouts = BuildComboLayouts(moves);
            var maxX = 620f;
            var maxY = 190f;
            for (var i = 0; i < layouts.Count; i++)
            {
                maxX = Mathf.Max(maxX, layouts[i].rect.xMax + 45f);
                maxY = Mathf.Max(maxY, layouts[i].rect.yMax + 40f);
            }

            comboGraphScroll = EditorGUILayout.BeginScrollView(comboGraphScroll, true, false, GUILayout.Height(Mathf.Min(420f, maxY + 18f)));
            var canvas = GUILayoutUtility.GetRect(maxX, maxY);
            lastGraphCanvasRect = canvas;
            EditorGUI.DrawRect(canvas, new Color(0.12f, 0.13f, 0.15f, 1f));
            DrawComboEdges(canvas, layouts, moves, links);
            DrawConnectionPreview(canvas, layouts);
            DrawComboNodes(canvas, layouts, moves, entryId, labels);
            HandleCanvasContextMenu(canvas, layouts);
            HandleGraphDeleteShortcut();
            EditorGUILayout.EndScrollView();
        }

        List<ComboNodeLayout> BuildComboLayouts(SerializedProperty moves)
        {
            var layouts = new List<ComboNodeLayout>();
            for (var i = 0; i < moves.arraySize; i++)
            {
                var position = moves.GetArrayElementAtIndex(i).FindPropertyRelative("graphPosition").vector2Value;
                layouts.Add(new ComboNodeLayout { index = i, rect = new Rect(position.x, position.y, 145f, 64f) });
            }
            return layouts;
        }

        void DrawConnectionPreview(Rect canvas, List<ComboNodeLayout> layouts)
        {
            if (connectingFromNodeIndex < 0 || connectingFromNodeIndex >= layouts.Count) return;
            var from = OffsetRect(layouts[connectingFromNodeIndex].rect, canvas.position);
            var start = new Vector3(from.xMax, from.center.y);
            var end = (Vector3)Event.current.mousePosition;
            Handles.BeginGUI();
            Handles.DrawBezier(start, end, start + Vector3.right * 70f, end + Vector3.left * 70f, new Color(0.3f, 0.9f, 1f), null, 3f);
            Handles.EndGUI();
            Repaint();
        }

        void HandleCanvasContextMenu(Rect canvas, List<ComboNodeLayout> layouts)
        {
            var evt = Event.current;
            if (evt.type != EventType.ContextClick || !canvas.Contains(evt.mousePosition)) return;
            for (var i = 0; i < layouts.Count; i++)
                if (OffsetRect(layouts[i].rect, canvas.position).Contains(evt.mousePosition)) return;
            var graphPosition = evt.mousePosition - canvas.position;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("新建招式"), false, () => AddComboMoveAt(graphPosition));
            menu.AddItem(new GUIContent("自动整理"), false, AutoArrangeCurrentGraph);
            menu.ShowAsContext();
            evt.Use();
        }

        void HandleGraphDeleteShortcut()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown || (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace) || GUIUtility.keyboardControl != 0) return;
            if (selectedTransitionIndex >= 0)
            {
                DeleteComboTransition(selectedTransitionIndex);
                evt.Use();
                return;
            }
            if (selectedGraphMoveIndex >= 0)
            {
                DeleteComboMove(selectedGraphMoveIndex);
                evt.Use();
            }
        }

        void DrawComboEdges(Rect canvas, List<ComboNodeLayout> layouts, SerializedProperty moves, SerializedProperty links)
        {
            var indexById = new Dictionary<string, int>();
            for (var i = 0; i < moves.arraySize; i++)
                indexById[moves.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue] = i;

            var evt = Event.current;
            var clickedLink = -1;
            var clickedDistance = 10f;
            Handles.BeginGUI();
            for (var i = 0; i < links.arraySize; i++)
            {
                var link = links.GetArrayElementAtIndex(i);
                if (!indexById.TryGetValue(link.FindPropertyRelative("fromInstanceId").stringValue, out var from) ||
                    !indexById.TryGetValue(link.FindPropertyRelative("toInstanceId").stringValue, out var to)) continue;
                var fromRect = OffsetRect(layouts[from].rect, canvas.position);
                var toRect = OffsetRect(layouts[to].rect, canvas.position);
                var start = new Vector3(fromRect.xMax, fromRect.center.y);
                var end = new Vector3(toRect.xMin, toRect.center.y);
                var tangent = Mathf.Max(45f, Mathf.Abs(end.x - start.x) * 0.42f);
                var controlA = start + Vector3.right * tangent;
                var controlB = end + Vector3.left * tangent;
                var baseColor = GetConditionColor((ComboInputCondition)link.FindPropertyRelative("condition").enumValueIndex);
                var selected = selectedTransitionIndex == i;
                var runtimeActive = IsRuntimePreviewTransition(link.FindPropertyRelative("id").stringValue);
                var color = runtimeActive ? new Color(0.35f, 1f, 0.38f) : selected ? new Color(1f, 0.88f, 0.2f) : baseColor;
                Handles.DrawBezier(start, end, controlA, controlB, color, null, runtimeActive || selected ? 5f : 3f);
                Handles.color = color;
                Handles.DrawAAConvexPolygon(end, end + new Vector3(-10f, -5f), end + new Vector3(-10f, 5f));
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    var distance = DistanceToBezier(evt.mousePosition, start, controlA, controlB, end);
                    if (distance < clickedDistance) { clickedDistance = distance; clickedLink = i; }
                }
            }
            Handles.EndGUI();

            if (clickedLink >= 0)
            {
                selectedTransitionIndex = clickedLink;
                draggingNodeIndex = -1;
                connectingFromNodeIndex = -1;
                evt.Use();
                Repaint();
            }

            for (var i = 0; i < links.arraySize; i++)
            {
                var link = links.GetArrayElementAtIndex(i);
                if (!indexById.TryGetValue(link.FindPropertyRelative("fromInstanceId").stringValue, out var from) ||
                    !indexById.TryGetValue(link.FindPropertyRelative("toInstanceId").stringValue, out var to)) continue;
                var fromRect = OffsetRect(layouts[from].rect, canvas.position);
                var toRect = OffsetRect(layouts[to].rect, canvas.position);
                var center = Vector2.Lerp(new Vector2(fromRect.xMax, fromRect.center.y), new Vector2(toRect.xMin, toRect.center.y), 0.5f);
                var labelRect = new Rect(center.x - 58f, center.y - 25f, 116f, 38f);
                GUI.Box(labelRect, BuildTransitionLabel(link), selectedTransitionIndex == i ? EditorStyles.helpBox : EditorStyles.miniButton);
            }
        }

        void DrawComboNodes(Rect canvas, List<ComboNodeLayout> layouts, SerializedProperty moves, string entryId, string[] labels)
        {
            var evt = Event.current;
            for (var i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                var node = moves.GetArrayElementAtIndex(layout.index);
                var move = node.FindPropertyRelative("move");
                var rect = OffsetRect(layout.rect, canvas.position);
                var inputPort = new Rect(rect.x - 7f, rect.center.y - 7f, 14f, 14f);
                var outputPort = new Rect(rect.xMax - 7f, rect.center.y - 7f, 14f, 14f);
                var nodeId = node.FindPropertyRelative("id").stringValue;
                var isEntry = nodeId == entryId;
                var isRuntime = IsRuntimePreviewMove(nodeId);
                var isSelected = layout.index == selectedGraphMoveIndex && selectedTransitionIndex < 0;
                var charge = node.FindPropertyRelative("enableCharge").boolValue;
                var heavy = move != null && move.FindPropertyRelative("attackType").enumValueIndex == 1;

                HandleNodeMouse(evt, canvas, rect, outputPort, layout.index, node);

                var old = GUI.backgroundColor;
                GUI.backgroundColor = charge ? new Color(0.72f, 0.46f, 0.95f) : heavy ? new Color(1f, 0.63f, 0.25f) : new Color(0.42f, 0.66f, 0.84f);
                var subtitle = charge ? "蓄力" : heavy ? "重击" : "普通";
                GUI.Box(rect, (isEntry ? "起手  " : string.Empty) + labels[layout.index] + "\n" + subtitle, GUI.skin.button);
                GUI.backgroundColor = old;
                EditorGUI.DrawRect(inputPort, new Color(0.75f, 0.82f, 0.9f));
                EditorGUI.DrawRect(outputPort, connectingFromNodeIndex == layout.index ? new Color(0.2f, 1f, 1f) : new Color(0.35f, 0.85f, 1f));
                if (isRuntime) DrawRectOutline(rect, new Color(0.35f, 1f, 0.38f), 4f);
                else if (isSelected) DrawRectOutline(rect, new Color(0.25f, 0.85f, 1f), 3f);
            }
            HandleConnectionRelease(evt, canvas, layouts);
            if (evt.type == EventType.MouseUp && evt.button == 0) draggingNodeIndex = -1;
        }

        void HandleNodeMouse(Event evt, Rect canvas, Rect rect, Rect outputPort, int index, SerializedProperty node)
        {
            if (evt.type == EventType.MouseDown && evt.button == 0 && outputPort.Contains(evt.mousePosition))
            {
                connectingFromNodeIndex = index;
                selectedGraphMoveIndex = index;
                selectedTransitionIndex = -1;
                evt.Use();
                return;
            }
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                selectedGraphMoveIndex = index;
                selectedComboIndex = index;
                selectedTransitionIndex = -1;
                currentFrame = 0;
                animationPlaying = false;
                draggingNodeIndex = index;
                nodeDragOffset = evt.mousePosition - canvas.position - node.FindPropertyRelative("graphPosition").vector2Value;
                Undo.RecordObject(combat, "移动连招节点");
                evt.Use();
                Repaint();
                return;
            }
            if (evt.type == EventType.MouseDrag && evt.button == 0 && draggingNodeIndex == index)
            {
                var next = evt.mousePosition - canvas.position - nodeDragOffset;
                node.FindPropertyRelative("graphPosition").vector2Value = new Vector2(Mathf.Max(12f, next.x), Mathf.Max(12f, next.y));
                EditorUtility.SetDirty(combat);
                evt.Use();
                Repaint();
                return;
            }
            if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
            {
                var capturedIndex = index;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("设为起手"), false, () => SetEntryMove(capturedIndex));
                menu.AddItem(new GUIContent("复制招式"), false, () => DuplicateComboMove(capturedIndex));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("删除招式"), false, () => DeleteComboMove(capturedIndex));
                menu.ShowAsContext();
                evt.Use();
            }
        }

        void HandleConnectionRelease(Event evt, Rect canvas, List<ComboNodeLayout> layouts)
        {
            if (evt.type != EventType.MouseUp || evt.button != 0 || connectingFromNodeIndex < 0) return;
            var target = -1;
            for (var i = 0; i < layouts.Count; i++)
            {
                var rect = OffsetRect(layouts[i].rect, canvas.position);
                var inputPort = new Rect(rect.x - 10f, rect.center.y - 12f, 24f, 24f);
                if (inputPort.Contains(evt.mousePosition)) { target = i; break; }
            }
            var source = connectingFromNodeIndex;
            connectingFromNodeIndex = -1;
            if (target >= 0 && target != source) CreateGraphConnection(source, target);
            evt.Use();
            Repaint();
        }

        static float DistanceToBezier(Vector2 point, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var best = float.MaxValue;
            var previous = (Vector2)a;
            for (var i = 1; i <= 24; i++)
            {
                var t = i / 24f;
                var u = 1f - t;
                var current = (Vector2)(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
                best = Mathf.Min(best, HandleUtility.DistancePointToLineSegment(point, previous, current));
                previous = current;
            }
            return best;
        }

        string BuildTransitionLabel(SerializedProperty link)
        {
            var condition = (ComboInputCondition)link.FindPropertyRelative("condition").enumValueIndex;
            var text = condition == ComboInputCondition.Press ? "点击" : condition == ComboInputCondition.Tap ? "轻点" : condition == ComboInputCondition.Hold ? "按住" : "松开";
            var command = (PlayerInputCommand)link.FindPropertyRelative("input").enumValueIndex;
            for (var i = 0; i < CommandValues.Length; i++) if (CommandValues[i] == command) { text += " " + CommandLabels[i]; break; }
            if (link.FindPropertyRelative("requiresHit").boolValue) text += " · 需命中";
            return text + "\n" + link.FindPropertyRelative("windowStart").floatValue.ToString("0.##") + "-" + link.FindPropertyRelative("windowEnd").floatValue.ToString("0.##") + "s";
        }

        static Color GetConditionColor(ComboInputCondition condition)
        {
            if (condition == ComboInputCondition.Hold) return new Color(0.78f, 0.48f, 1f);
            if (condition == ComboInputCondition.Tap) return new Color(0.35f, 0.85f, 1f);
            if (condition == ComboInputCondition.Release) return new Color(1f, 0.72f, 0.3f);
            return new Color(0.65f, 0.8f, 0.9f);
        }

        static Rect OffsetRect(Rect rect, Vector2 offset)
        {
            rect.position += offset;
            return rect;
        }

        static void DrawRectOutline(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        void DrawSelectedConnectionEditor(SerializedProperty link, int index, SerializedProperty moves)
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("\u5f53\u524d\u8fde\u63a5", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("\u5220\u9664\u8fde\u63a5", EditorStyles.toolbarButton, GUILayout.Width(72f))) { DeleteComboTransition(index); return; }
            }
            var labels = BuildNameLabels(moves, "\u62db\u5f0f");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMoveIdPopup(link.FindPropertyRelative("fromInstanceId"), moves, labels, "从");
                    GUILayout.Label("→", GUILayout.Width(18f));
                    DrawMoveIdPopup(link.FindPropertyRelative("toInstanceId"), moves, labels, "到");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawCommandPopup(link.FindPropertyRelative("input"), new GUIContent("\u8f93\u5165"));
                    EditorGUILayout.PropertyField(link.FindPropertyRelative("condition"), new GUIContent("\u65b9\u5f0f"));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(link.FindPropertyRelative("windowStart"), new GUIContent("\u7a97\u53e3\u5f00\u59cb"));
                    EditorGUILayout.PropertyField(link.FindPropertyRelative("windowEnd"), new GUIContent("\u7a97\u53e3\u7ed3\u675f"));
                }
                var condition = (ComboInputCondition)link.FindPropertyRelative("condition").enumValueIndex;
                DrawTransitionDecisionHint(condition);
                EditorGUILayout.PropertyField(link.FindPropertyRelative("requiresHit"), new GUIContent("需要前一招命中"));
            }
        }

        void DrawSelectedMoveEditor(SerializedProperty node, int index)
        {
            var move = node.FindPropertyRelative("move");
            if (move == null) return;
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("当前招式详情", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("删除这一招", EditorStyles.toolbarButton, GUILayout.Width(86f))) { DeleteComboMove(index); return; }
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(node.FindPropertyRelative("name"), new GUIContent("招式名称"));
                move.FindPropertyRelative("name").stringValue = node.FindPropertyRelative("name").stringValue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMoveCategoryPopup(move.FindPropertyRelative("moveCategory"), new GUIContent("招式类别"));
                    DrawComboCategoryPopup(move.FindPropertyRelative("comboCategory"), new GUIContent("连招定位"));
                }
                EditorGUILayout.PropertyField(move.FindPropertyRelative("animationClip"), new GUIContent("动画片段"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(move.FindPropertyRelative("animationFrameCount"), new GUIContent("总帧数"));
                    EditorGUILayout.PropertyField(move.FindPropertyRelative("mirrorHitboxByFacing"), new GUIContent("按朝向镜像"));
                }
            }

            selectedComboIndex = index;
            DrawHitboxKeyEditor(move, index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("动作时间", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(move.FindPropertyRelative("startup"), new GUIContent("前摇"));
                    EditorGUILayout.PropertyField(move.FindPropertyRelative("activeTime"), new GUIContent("有效"));
                    EditorGUILayout.PropertyField(move.FindPropertyRelative("recovery"), new GUIContent("后摇"));
                }
                EditorGUILayout.PropertyField(move.FindPropertyRelative("lockMovement"), new GUIContent("动作期间锁定移动"));
            }

            moveCombatExpanded = EditorGUILayout.Foldout(moveCombatExpanded, "伤害与目标反应", true, EditorStyles.foldoutHeader);
            if (moveCombatExpanded) DrawMoveCombat(move);
            moveChargeExpanded = EditorGUILayout.Foldout(moveChargeExpanded, "蓄力设置", true, EditorStyles.foldoutHeader);
            if (moveChargeExpanded) DrawMoveCharge(node);
            moveFeedbackExpanded = EditorGUILayout.Foldout(moveFeedbackExpanded, "命中反馈", true, EditorStyles.foldoutHeader);
            if (moveFeedbackExpanded) DrawHitFeedbackEditor(move);
        }

        void DrawMoveCombat(SerializedProperty move)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(move.FindPropertyRelative("attackType"), new GUIContent("攻击类型"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(move.FindPropertyRelative("damageMultiplier"), new GUIContent("伤害倍率"));
                    EditorGUILayout.PropertyField(move.FindPropertyRelative("knockbackMultiplier"), new GUIContent("击退倍率"));
                }
                var reaction = move.FindPropertyRelative("enableTargetReaction");
                EditorGUILayout.PropertyField(reaction, new GUIContent("启用目标反应"));
                if (reaction.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(move.FindPropertyRelative("targetReaction"), new GUIContent("反应类型"));
                    var custom = move.FindPropertyRelative("useCustomKnockback");
                    EditorGUILayout.PropertyField(custom, new GUIContent("自定义击退"));
                    if (custom.boolValue) EditorGUILayout.PropertyField(move.FindPropertyRelative("customKnockback"), new GUIContent("击退向量"));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(move.FindPropertyRelative("interruptPower"), new GUIContent("中断"));
                        EditorGUILayout.PropertyField(move.FindPropertyRelative("poiseDamage"), new GUIContent("削韧"));
                        EditorGUILayout.PropertyField(move.FindPropertyRelative("breaksSuperArmor"), new GUIContent("破霸体"));
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        void DrawMoveCharge(SerializedProperty node)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("这里控制选中招式进入蓄力状态后的表现。连线上的‘按住判定’只负责决定是否进入这条蓄力分支。", MessageType.Info);

                var enabled = node.FindPropertyRelative("enableCharge");
                EditorGUILayout.PropertyField(enabled, new GUIContent("启用蓄力", "开启后，这个招式会等待玩家松开按键再出招。"));
                if (!enabled.boolValue)
                {
                    EditorGUILayout.LabelField("当前招式会直接播放，不进行蓄力。", EditorStyles.miniLabel);
                    return;
                }

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("蓄力时间", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("minimumChargeTime"), new GUIContent("最低有效蓄力", "低于该时间时不会完成蓄力招式。通常与连线的按住判定保持一致。"));
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("maximumChargeTime"), new GUIContent("达到满蓄力", "伤害和击退增长到最大值所需的总按住时间。"));
                }

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("击飞判定", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(node.FindPropertyRelative("launchChargeThreshold"),
                    new GUIContent("击飞就绪进度", "只有招式配置为“击飞”且蓄力进度达到此值时才会真正击飞。"));
                EditorGUILayout.PropertyField(node.FindPropertyRelative("insufficientLaunchReaction"),
                    new GUIContent("未达标受击", "击飞技未达到阈值时，敌人采用的受击反应。若误选击飞，运行时会回退为重受击。"));
                EditorGUILayout.HelpBox("蓄力分为第一阶段（蓄力中）与第二阶段（击飞就绪）；满蓄力是第二阶段的最终状态。", MessageType.None);

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("蓄力收益", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("fullChargeDamageMultiplier"), new GUIContent("满蓄力伤害倍率"));
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("fullChargeKnockbackMultiplier"), new GUIContent("满蓄力击退倍率"));
                }

                var autoRelease = node.FindPropertyRelative("autoReleaseAtFullCharge");
                EditorGUILayout.PropertyField(autoRelease, new GUIContent("蓄满后自动出招", "关闭时，蓄满后会保持满蓄力，直到玩家松开按键。"));
                EditorGUILayout.HelpBox(autoRelease.boolValue
                    ? "当前释放方式：达到满蓄力后自动出招。"
                    : "当前释放方式：可以一直按住；蓄满后保持，松开按键才出招。", MessageType.None);

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("蓄力表现", EditorStyles.boldLabel);
EditorGUILayout.PropertyField(
                    node.FindPropertyRelative("chargeHoldFrame"),
                    new GUIContent("蓄力定格帧", "该动作播放到指定动画帧后暂停；松开按键后从这一帧继续播放。"));
                EditorGUILayout.HelpBox("蓄力会定格当前招式自己的动画，不会切换到另一段蓄力动画。", MessageType.None);
                var showEffect = node.FindPropertyRelative("showChargeEffect");
                EditorGUILayout.PropertyField(showEffect, new GUIContent("显示蓄力特效"));
                if (showEffect.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("chargeEffectPrefab"), new GUIContent("蓄力特效 Prefab", "颜色、光点、脉冲和音效统一在 Prefab 的 ChargeTelegraphPresentation 组件中调整。"));
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("chargeEffectOffset"), new GUIContent("特效位置偏移"));
                    EditorGUILayout.HelpBox("这里只选择 Prefab 和调整挂点位置。特效外观请直接打开 Prefab 调整。", MessageType.Info);
                    EditorGUI.indentLevel--;
                }
            }
        }
        void DrawComboTransitions(SerializedProperty links, SerializedProperty moves)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("连接设置", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(moves.arraySize < 2))
                    if (GUILayout.Button("添加连接", GUILayout.Width(82f))) { AddComboTransition(); return; }
            }
            var labels = BuildNameLabels(moves, "招式");
            for (var i = 0; i < links.arraySize; i++)
            {
                var link = links.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawMoveIdPopup(link.FindPropertyRelative("fromInstanceId"), moves, labels, "从");
                        GUILayout.Label("→", GUILayout.Width(18f));
                        DrawMoveIdPopup(link.FindPropertyRelative("toInstanceId"), moves, labels, "到");
                        if (GUILayout.Button("删除", GUILayout.Width(48f))) { DeleteComboTransition(i); return; }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawCommandPopup(link.FindPropertyRelative("input"), new GUIContent("输入"));
                        EditorGUILayout.PropertyField(link.FindPropertyRelative("condition"), new GUIContent("方式"));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(link.FindPropertyRelative("windowStart"), new GUIContent("窗口开始"));
                        EditorGUILayout.PropertyField(link.FindPropertyRelative("windowEnd"), new GUIContent("窗口结束"));
                    }
                    var condition = (ComboInputCondition)link.FindPropertyRelative("condition").enumValueIndex;
                    DrawTransitionDecisionHint(condition);
                    EditorGUILayout.PropertyField(link.FindPropertyRelative("requiresHit"), new GUIContent("需要前一招命中"));
                }
            }
        }

        static void DrawTransitionDecisionHint(ComboInputCondition condition)
        {
            if (condition == ComboInputCondition.Hold)
                EditorGUILayout.HelpBox("源招式结束时按键仍处于按住状态，就进入蓄力招式。", MessageType.Info);
            else if (condition == ComboInputCondition.Tap)
                EditorGUILayout.HelpBox("源招式结束前已经松开按键，就进入快速招式。", MessageType.Info);
        }
        SerializedProperty GetSelectedGraphMoveProperty()
        {
            var graphs = serializedCombat != null ? serializedCombat.FindProperty("comboGraphs") : null;
            if (graphs == null || graphs.arraySize == 0) return null;
            selectedGraphIndex = Mathf.Clamp(selectedGraphIndex, 0, graphs.arraySize - 1);
            var moves = graphs.GetArrayElementAtIndex(selectedGraphIndex).FindPropertyRelative("instances");
            if (moves == null || moves.arraySize == 0) return null;
            selectedGraphMoveIndex = Mathf.Clamp(selectedGraphMoveIndex, 0, moves.arraySize - 1);
            return moves.GetArrayElementAtIndex(selectedGraphMoveIndex).FindPropertyRelative("move");
        }

        static void DrawMoveIdPopup(SerializedProperty id, SerializedProperty moves, string[] labels, string label)
        {
            var index = FindStringIndex(moves, "id", id.stringValue);
            index = EditorGUILayout.Popup(label, Mathf.Max(0, index), labels);
            if (moves.arraySize > 0) id.stringValue = moves.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue;
        }

        static int FindStringIndex(SerializedProperty array, string field, string value)
        {
            for (var i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).FindPropertyRelative(field).stringValue == value) return i;
            return -1;
        }

        static string[] BuildNameLabels(SerializedProperty array, string fallback)
        {
            var labels = new string[array.arraySize];
            for (var i = 0; i < labels.Length; i++)
            {
                var name = array.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                labels[i] = string.IsNullOrEmpty(name) ? fallback + " " + (i + 1) : name;
            }
            return labels;
        }

        void CommitAndRefresh()
        {
            serializedCombat.ApplyModifiedProperties();
            EditorUtility.SetDirty(combat);
            SyncRuntimePreviewIfNeeded();
            serializedCombat = new SerializedObject(combat);
            serializedCombat.Update();
        }

        void AddComboGraph()
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "新建连招");
            combat.ComboGraphs.Add(new PlayerComboGraph { name = "新连招" });
            selectedGraphIndex = combat.ComboGraphs.Count - 1;
            selectedGraphMoveIndex = 0;
            RefreshAfterStructureChange();
        }

        void DeleteComboGraph()
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "删除连招");
            if (combat.ComboGraphs.Count > 0) combat.ComboGraphs.RemoveAt(Mathf.Clamp(selectedGraphIndex, 0, combat.ComboGraphs.Count - 1));
            selectedGraphIndex = Mathf.Max(0, selectedGraphIndex - 1);
            selectedGraphMoveIndex = 0;
            RefreshAfterStructureChange();
        }

        void AddComboMove(bool duplicate)
        {
            if (duplicate)
            {
                DuplicateComboMove(selectedGraphMoveIndex);
                return;
            }
            CommitAndRefresh();
            var graph = combat.ComboGraphs[selectedGraphIndex];
            var position = graph.instances.Count == 0
                ? new Vector2(35f, 55f)
                : graph.instances[Mathf.Clamp(selectedGraphMoveIndex, 0, graph.instances.Count - 1)].graphPosition + new Vector2(225f, 0f);
            AddComboMoveAt(position);
        }

        void DeleteComboMove(int index)
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "删除连招招式");
            var graph = combat.ComboGraphs[selectedGraphIndex];
            var id = graph.instances[index].id;
            graph.instances.RemoveAt(index);
            graph.transitions.RemoveAll(x => x.fromInstanceId == id || x.toInstanceId == id);
            if (graph.entryInstanceId == id) graph.entryInstanceId = graph.instances.Count > 0 ? graph.instances[0].id : string.Empty;
            selectedGraphMoveIndex = Mathf.Max(0, selectedGraphMoveIndex - 1);
            selectedTransitionIndex = -1;
            RefreshAfterStructureChange();
        }

        void AddComboTransition()
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "添加连招连接");
            var graph = combat.ComboGraphs[selectedGraphIndex];
            graph.transitions.Add(new PlayerComboTransition
            {
                id = Guid.NewGuid().ToString("N"),
                fromInstanceId = graph.instances[0].id,
                toInstanceId = graph.instances[Mathf.Min(1, graph.instances.Count - 1)].id
            });
            RefreshAfterStructureChange();
        }

        void DeleteComboTransition(int index)
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "删除连招连接");
            combat.ComboGraphs[selectedGraphIndex].transitions.RemoveAt(index);
            selectedTransitionIndex = -1;
            RefreshAfterStructureChange();
        }

        void AddComboMoveAt(Vector2 position)
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "新建连招招式");
            var graph = combat.ComboGraphs[selectedGraphIndex];
            var node = new PlayerComboMove
            {
                id = Guid.NewGuid().ToString("N"),
                name = "新招式",
                move = new PlayerComboStep { name = "新招式" },
                graphPosition = new Vector2(Mathf.Max(12f, position.x), Mathf.Max(12f, position.y))
            };
            graph.instances.Add(node);
            if (string.IsNullOrEmpty(graph.entryInstanceId)) graph.entryInstanceId = node.id;
            selectedGraphMoveIndex = graph.instances.Count - 1;
            selectedTransitionIndex = -1;
            RefreshAfterStructureChange();
        }

        void DuplicateComboMove(int index)
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "复制连招招式");
            var graph = combat.ComboGraphs[selectedGraphIndex];
            var source = graph.instances[Mathf.Clamp(index, 0, graph.instances.Count - 1)];
            var node = JsonUtility.FromJson<PlayerComboMove>(JsonUtility.ToJson(source));
            node.id = Guid.NewGuid().ToString("N");
            node.name += " 副本";
            node.graphPosition += new Vector2(35f, 85f);
            graph.instances.Add(node);
            selectedGraphMoveIndex = graph.instances.Count - 1;
            selectedTransitionIndex = -1;
            RefreshAfterStructureChange();
        }

        void SetEntryMove(int index)
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "设置连招起手");
            var graph = combat.ComboGraphs[selectedGraphIndex];
            graph.entryInstanceId = graph.instances[Mathf.Clamp(index, 0, graph.instances.Count - 1)].id;
            RefreshAfterStructureChange();
        }

        void CreateGraphConnection(int fromIndex, int toIndex)
        {
            CommitAndRefresh();
            var graph = combat.ComboGraphs[selectedGraphIndex];
            if (fromIndex < 0 || toIndex < 0 || fromIndex >= graph.instances.Count || toIndex >= graph.instances.Count || fromIndex == toIndex) return;
            var fromId = graph.instances[fromIndex].id;
            var toId = graph.instances[toIndex].id;
            for (var i = 0; i < graph.transitions.Count; i++)
            {
                var existing = graph.transitions[i];
                if (existing.fromInstanceId == fromId && existing.toInstanceId == toId && existing.condition == ComboInputCondition.Press)
                    return;
            }
            Undo.RecordObject(combat, "创建连招连接");
            graph.transitions.Add(new PlayerComboTransition
            {
                id = Guid.NewGuid().ToString("N"),
                fromInstanceId = fromId,
                toInstanceId = toId,
                input = PlayerInputCommand.PrimaryAttack,
                condition = ComboInputCondition.Press
            });
            selectedTransitionIndex = graph.transitions.Count - 1;
            RefreshAfterStructureChange();
        }

        void AutoArrangeCurrentGraph()
        {
            CommitAndRefresh();
            Undo.RecordObject(combat, "自动整理连招图");
            var graph = combat.ComboGraphs[selectedGraphIndex];
            var count = graph.instances.Count;
            if (count == 0) return;
            var indexById = new Dictionary<string, int>();
            var depth = new int[count];
            for (var i = 0; i < count; i++) { indexById[graph.instances[i].id] = i; depth[i] = -1; }
            if (indexById.TryGetValue(graph.entryInstanceId, out var entry)) depth[entry] = 0; else depth[0] = 0;
            for (var pass = 0; pass < count; pass++)
            {
                var changed = false;
                for (var i = 0; i < graph.transitions.Count; i++)
                {
                    var link = graph.transitions[i];
                    if (!indexById.TryGetValue(link.fromInstanceId, out var from) || !indexById.TryGetValue(link.toInstanceId, out var to) || depth[from] < 0) continue;
                    var next = depth[from] + 1;
                    if (depth[to] < next) { depth[to] = next; changed = true; }
                }
                if (!changed) break;
            }
            var maxDepth = 0;
            for (var i = 0; i < count; i++) maxDepth = Mathf.Max(maxDepth, depth[i]);
            for (var i = 0; i < count; i++) if (depth[i] < 0) depth[i] = ++maxDepth;
            var rows = new Dictionary<int, int>();
            for (var i = 0; i < count; i++) rows[depth[i]] = rows.TryGetValue(depth[i], out var rowCount) ? rowCount + 1 : 1;
            var maxRows = 1;
            foreach (var pair in rows) maxRows = Mathf.Max(maxRows, pair.Value);
            var cursor = new Dictionary<int, int>();
            for (var i = 0; i < count; i++)
            {
                var row = cursor.TryGetValue(depth[i], out var current) ? current : 0;
                cursor[depth[i]] = row + 1;
                graph.instances[i].graphPosition = new Vector2(35f + depth[i] * 225f, 28f + (row + (maxRows - rows[depth[i]]) * 0.5f) * 105f);
            }
            RefreshAfterStructureChange();
        }

        void RefreshAfterStructureChange()
        {
            EditorUtility.SetDirty(combat);
            SyncRuntimePreviewIfNeeded();
            serializedCombat = new SerializedObject(combat);
            GUIUtility.ExitGUI();
        }
    }
}
// Graph editor compile refresh
