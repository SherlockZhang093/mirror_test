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
        bool moveChargeExpanded;
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
            var end = Event.current.mousePosition;
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

            Handles.BeginGUI();
            for (var i = 0; i < links.arraySize; i++)
            {
                var link = links.GetArrayElementAtIndex(i);
                if (!indexById.TryGetValue(link.FindPropertyRelative("fromInstanceId").stringValue, out var from) ||
                    !indexById.TryGetValue(link.FindPropertyRelative("toInstanceId").stringValue, out var to))
                    continue;
                var fromRect = OffsetRect(layouts[from].rect, canvas.position);
                var toRect = OffsetRect(layouts[to].rect, canvas.position);
                var start = new Vector3(fromRect.xMax, fromRect.center.y);
                var end = new Vector3(toRect.xMin, toRect.center.y);
                var tangent = Mathf.Max(45f, Mathf.Abs(end.x - start.x) * 0.42f);
                var color = GetConditionColor((ComboInputCondition)link.FindPropertyRelative("condition").enumValueIndex);
                Handles.DrawBezier(start, end, start + Vector3.right * tangent, end + Vector3.left * tangent, color, null, 3f);
                Handles.color = color;
                Handles.DrawAAConvexPolygon(end, end + new Vector3(-10f, -5f), end + new Vector3(-10f, 5f));
            }
            Handles.EndGUI();

            for (var i = 0; i < links.arraySize; i++)
            {
                var link = links.GetArrayElementAtIndex(i);
                if (!indexById.TryGetValue(link.FindPropertyRelative("fromInstanceId").stringValue, out var from) ||
                    !indexById.TryGetValue(link.FindPropertyRelative("toInstanceId").stringValue, out var to))
                    continue;
                var fromRect = OffsetRect(layouts[from].rect, canvas.position);
                var toRect = OffsetRect(layouts[to].rect, canvas.position);
                var center = Vector2.Lerp(new Vector2(fromRect.xMax, fromRect.center.y), new Vector2(toRect.xMin, toRect.center.y), 0.5f);
                var labelRect = new Rect(center.x - 58f, center.y - 25f, 116f, 38f);
                GUI.Box(labelRect, BuildTransitionLabel(link), EditorStyles.helpBox);
            }
        }

        void DrawComboNodes(Rect canvas, List<ComboNodeLayout> layouts, SerializedProperty moves, string entryId, string[] labels)
        {
            for (var i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                var node = moves.GetArrayElementAtIndex(layout.index);
                var move = node.FindPropertyRelative("move");
                var rect = OffsetRect(layout.rect, canvas.position);
                var isEntry = node.FindPropertyRelative("id").stringValue == entryId;
                var isSelected = layout.index == selectedGraphMoveIndex;
                var charge = node.FindPropertyRelative("enableCharge").boolValue;
                var heavy = move != null && move.FindPropertyRelative("attackType").enumValueIndex == 1;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = charge ? new Color(0.72f, 0.46f, 0.95f) : heavy ? new Color(1f, 0.63f, 0.25f) : new Color(0.42f, 0.66f, 0.84f);
                var subtitle = charge ? "蓄力" : heavy ? "重击" : "普通";
                if (GUI.Button(rect, (isEntry ? "起手  " : string.Empty) + labels[layout.index] + "\n" + subtitle))
                {
                    selectedGraphMoveIndex = layout.index;
                    selectedComboIndex = layout.index;
                    currentFrame = 0;
                    animationPlaying = false;
                    Repaint();
                }
                GUI.backgroundColor = old;
                if (isSelected) DrawRectOutline(rect, new Color(0.25f, 0.85f, 1f), 3f);
            }
        }

        string BuildTransitionLabel(SerializedProperty link)
        {
            var condition = (ComboInputCondition)link.FindPropertyRelative("condition").enumValueIndex;
            var text = condition == ComboInputCondition.Press ? "点击" : condition == ComboInputCondition.Tap ? "轻点" : condition == ComboInputCondition.Hold ? "按住" : "松开";
            if (condition == ComboInputCondition.Hold || condition == ComboInputCondition.Tap)
                text += " " + link.FindPropertyRelative("holdThreshold").floatValue.ToString("0.##") + "s";
            var command = (PlayerInputCommand)link.FindPropertyRelative("input").enumValueIndex;
            for (var i = 0; i < CommandValues.Length; i++) if (CommandValues[i] == command) { text += " " + CommandLabels[i]; break; }
            return text + "\n" + link.FindPropertyRelative("windowStart").floatValue.ToString("0.##") + "–" + link.FindPropertyRelative("windowEnd").floatValue.ToString("0.##") + "s";
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
            moveChargeExpanded = EditorGUILayout.Foldout(moveChargeExpanded, "蓄力", true, EditorStyles.foldoutHeader);
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
                var enabled = node.FindPropertyRelative("enableCharge");
                EditorGUILayout.PropertyField(enabled, new GUIContent("启用蓄力"));
                if (!enabled.boolValue) return;
                EditorGUILayout.PropertyField(node.FindPropertyRelative("chargeAnimation"), new GUIContent("蓄力动画"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("minimumChargeTime"), new GUIContent("最低蓄力"));
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("maximumChargeTime"), new GUIContent("满蓄力"));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("fullChargeDamageMultiplier"), new GUIContent("满蓄力伤害"));
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("fullChargeKnockbackMultiplier"), new GUIContent("满蓄力击退"));
                }
                EditorGUILayout.PropertyField(node.FindPropertyRelative("autoReleaseAtFullCharge"), new GUIContent("满蓄力自动释放"));
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
                    if (condition == ComboInputCondition.Hold || condition == ComboInputCondition.Tap)
                        EditorGUILayout.PropertyField(link.FindPropertyRelative("holdThreshold"), new GUIContent(condition == ComboInputCondition.Hold ? "按住判定" : "轻点最长时间"));
                }
            }
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
            serializedCombat = new SerializedObject(combat);
            serializedCombat.Update();
        }

        void AddComboGraph()
        {
            CommitAndRefresh();
            combat.ComboGraphs.Add(new PlayerComboGraph { name = "新连招" });
            selectedGraphIndex = combat.ComboGraphs.Count - 1;
            selectedGraphMoveIndex = 0;
            RefreshAfterStructureChange();
        }

        void DeleteComboGraph()
        {
            CommitAndRefresh();
            if (combat.ComboGraphs.Count > 0) combat.ComboGraphs.RemoveAt(Mathf.Clamp(selectedGraphIndex, 0, combat.ComboGraphs.Count - 1));
            selectedGraphIndex = Mathf.Max(0, selectedGraphIndex - 1);
            selectedGraphMoveIndex = 0;
            RefreshAfterStructureChange();
        }

        void AddComboMove(bool duplicate)
        {
            CommitAndRefresh();
            var graph = combat.ComboGraphs[selectedGraphIndex];
            PlayerComboMove node;
            if (duplicate && graph.instances.Count > 0)
            {
                var source = graph.instances[Mathf.Clamp(selectedGraphMoveIndex, 0, graph.instances.Count - 1)];
                node = JsonUtility.FromJson<PlayerComboMove>(JsonUtility.ToJson(source));
                node.id = Guid.NewGuid().ToString("N");
                node.name += " 副本";
            }
            else
            {
                node = new PlayerComboMove { id = Guid.NewGuid().ToString("N"), name = "新招式", move = new PlayerComboStep { name = "新招式" } };
            }
            graph.instances.Add(node);
            if (string.IsNullOrEmpty(graph.entryInstanceId)) graph.entryInstanceId = node.id;
            selectedGraphMoveIndex = graph.instances.Count - 1;
            RefreshAfterStructureChange();
        }

        void DeleteComboMove(int index)
        {
            CommitAndRefresh();
            var graph = combat.ComboGraphs[selectedGraphIndex];
            var id = graph.instances[index].id;
            graph.instances.RemoveAt(index);
            graph.transitions.RemoveAll(x => x.fromInstanceId == id || x.toInstanceId == id);
            if (graph.entryInstanceId == id) graph.entryInstanceId = graph.instances.Count > 0 ? graph.instances[0].id : string.Empty;
            selectedGraphMoveIndex = Mathf.Max(0, selectedGraphMoveIndex - 1);
            RefreshAfterStructureChange();
        }

        void AddComboTransition()
        {
            CommitAndRefresh();
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
            combat.ComboGraphs[selectedGraphIndex].transitions.RemoveAt(index);
            RefreshAfterStructureChange();
        }

        void RefreshAfterStructureChange()
        {
            EditorUtility.SetDirty(combat);
            serializedCombat = new SerializedObject(combat);
            GUIUtility.ExitGUI();
        }
    }
}
