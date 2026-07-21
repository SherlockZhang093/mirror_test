using System.Collections.Generic;
using System.Linq;
using MirrorTrial.Enemies;
using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    [CustomEditor(typeof(MirrorTrial.Level.LevelManager))]
    public class LevelManagerEditor : UnityEditor.Editor
    {
        public enum EditorTab
        {
            Level,
            Geometry,
            Gameplay,
            Enemies,
            Review
        }
        MirrorTrial.Level.LevelManager manager;
        SerializedProperty levelId;
        SerializedProperty levelDisplayName;
        SerializedProperty playerSpawn;
        SerializedProperty playerPrefab;
        SerializedProperty geometryRoot;
        SerializedProperty gameplayRoot;
        SerializedProperty runtimeRoot;
        SerializedProperty autoCollectOnAwake;

        enum GeometryKind
        {
            Platform,
            Boundary,
            SolidBlock
        }

        GeometryKind geometryKind = GeometryKind.Platform;
        Vector2 geometrySize = new Vector2(4f, 1f);
        int geometryPlatformSpriteIndex = 1;
        GameObject enemyPrefab;
        bool enemySnapToGround = true;
        float enemySnapOffsetY = 1f;
        bool enemyPlacementActive;
        readonly BoxBoundsHandle enemyDetectionHandle = new BoxBoundsHandle();
        EditorTab currentTab;

        void OnEnable()
        {
            manager = target as MirrorTrial.Level.LevelManager;
            levelId = serializedObject.FindProperty("levelId");
            levelDisplayName = serializedObject.FindProperty("levelDisplayName");
            playerSpawn = serializedObject.FindProperty("playerSpawn");
            playerPrefab = serializedObject.FindProperty("playerPrefab");
            geometryRoot = serializedObject.FindProperty("geometryRoot");
            gameplayRoot = serializedObject.FindProperty("gameplayRoot");
            runtimeRoot = serializedObject.FindProperty("runtimeRoot");
            autoCollectOnAwake = serializedObject.FindProperty("AutoCollectOnAwake");
            SceneView.duringSceneGui += OnSceneGUI;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("关卡布置功能已集中到独立的关卡编辑器窗口。", MessageType.Info);
            if (GUILayout.Button("打开关卡编辑器", GUILayout.Height(32)))
                LevelEditorWindow.Open(manager);

            serializedObject.Update();
            DrawLevelSettings();
            serializedObject.ApplyModifiedProperties();
        }

        public void DrawTab(EditorTab tab)
        {
            if (!manager) return;
            currentTab = tab;
            serializedObject.Update();

            switch (tab)
            {
                case EditorTab.Level:
                    DrawLevelSettings();
                    break;
                case EditorTab.Geometry:
                    DrawGeometryTools();
                    EditorGUILayout.Space(12);
                    DrawPlatformArtTools();
                    break;
                case EditorTab.Gameplay:
                    DrawAddButtons();
                    EditorGUILayout.Space(12);
                    DrawCollections();
                    break;
                case EditorTab.Enemies:
                    DrawEnemyPlacementTools();
                    break;
                case EditorTab.Review:
                    DrawValidation();
                    EditorGUILayout.Space(12);
                    DrawExportTools();
                    EditorGUILayout.Space(12);
                    if (GUILayout.Button("全部收集", GUILayout.Height(28)))
                    {
                        Undo.RecordObject(manager, "全部收集");
                        manager.CollectAll();
                        EditorUtility.SetDirty(manager);
                    }
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawLevelSettings()
        {
            EditorGUILayout.LabelField("关卡信息", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelId, new GUIContent("关卡ID"));
            EditorGUILayout.PropertyField(levelDisplayName, new GUIContent("关卡显示名"));
            EditorGUILayout.PropertyField(playerSpawn, new GUIContent("玩家出生点"));
            EditorGUILayout.PropertyField(playerPrefab, new GUIContent("玩家预制体"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("场景根节点", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(geometryRoot, new GUIContent("地形根节点"));
            EditorGUILayout.PropertyField(gameplayRoot, new GUIContent("玩法根节点"));
            EditorGUILayout.PropertyField(runtimeRoot, new GUIContent("运行时根节点"));
            EditorGUILayout.PropertyField(autoCollectOnAwake, new GUIContent("启动时自动收集"));
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

        void DrawGeometryTools()
        {
            geometryKind = (GeometryKind)EditorGUILayout.Popup("地形类型", (int)geometryKind, new[] { "平台", "边界", "实体块" });
            geometrySize = EditorGUILayout.Vector2Field("默认尺寸", geometrySize);

            if (geometryKind == GeometryKind.Platform)
            {
                geometryPlatformSpriteIndex = EditorGUILayout.IntSlider("平台贴图", geometryPlatformSpriteIndex, 1, LevelPlatformVisualUtility.PlatformSpriteCount);
            }

            DrawGeometryPreview();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 添加地形", GUILayout.Height(28)))
                AddGeometryObject(geometryKind);
            if (GUILayout.Button("+ 添加平台", GUILayout.Height(28)))
                AddGeometryObject(GeometryKind.Platform);
            if (GUILayout.Button("+ 添加边界", GUILayout.Height(28)))
                AddGeometryObject(GeometryKind.Boundary);
            EditorGUILayout.EndHorizontal();
        }

        void AddGeometryObject(GeometryKind kind)
        {
            serializedObject.ApplyModifiedProperties();

            var root = EnsureGeometryRoot();
            var prefix = GetGeometryPrefix(kind);
            var go = new GameObject(manager.GetUniqueName(prefix + "01"));
            Undo.RegisterCreatedObjectUndo(go, "添加地形");
            go.transform.SetParent(root);
            go.transform.position = manager.transform.position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            ApplyGroundLayer(go);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = geometrySize;
            col.isTrigger = false;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = GetGeometryColor(kind);
            sr.sortingOrder = 0;

            if (kind == GeometryKind.Platform)
                LevelPlatformVisualUtility.ApplyPlatformVisual(go, geometryPlatformSpriteIndex);
            ApplyGroundLayerRecursive(go);

            Selection.activeObject = manager;
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        void DrawGeometryPreview()
        {
            var previewRect = GUILayoutUtility.GetRect(120f, 72f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.12f, 1f));

            var safeSize = new Vector2(Mathf.Max(0.01f, geometrySize.x), Mathf.Max(0.01f, geometrySize.y));
            var maxWidth = previewRect.width - 24f;
            var maxHeight = previewRect.height - 24f;
            var scale = Mathf.Min(maxWidth / safeSize.x, maxHeight / safeSize.y);
            var size = safeSize * scale;
            var shapeRect = new Rect(previewRect.center.x - size.x * 0.5f, previewRect.center.y - size.y * 0.5f, size.x, size.y);

            if (geometryKind == GeometryKind.Platform)
            {
                var sprite = LevelPlatformVisualUtility.GetPlatformSprite(geometryPlatformSpriteIndex);
                var texture = sprite ? AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite) : null;
                if (texture)
                    GUI.DrawTexture(shapeRect, texture, ScaleMode.ScaleToFit, true);
                else
                    EditorGUI.DrawRect(shapeRect, new Color(0.3f, 0.45f, 0.65f, 0.9f));
            }
            else
            {
                EditorGUI.DrawRect(shapeRect, GetGeometryColor(geometryKind));
            }

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.7f);
            Handles.DrawAAPolyLine(2f,
                new Vector3(shapeRect.xMin, shapeRect.yMin),
                new Vector3(shapeRect.xMax, shapeRect.yMin),
                new Vector3(shapeRect.xMax, shapeRect.yMax),
                new Vector3(shapeRect.xMin, shapeRect.yMax),
                new Vector3(shapeRect.xMin, shapeRect.yMin));
            Handles.EndGUI();
        }

        static void ApplyGroundLayer(GameObject go)
        {
            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                go.layer = groundLayer;
        }

        static void ApplyGroundLayerRecursive(GameObject go)
        {
            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0 || !go) return;

            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = groundLayer;
        }

        Transform EnsureGeometryRoot()
        {
            var root = geometryRoot.objectReferenceValue as Transform;
            if (root) return root;

            root = manager.transform.Find("地形");
            if (!root)
            {
                var go = new GameObject("地形");
                Undo.RegisterCreatedObjectUndo(go, "创建地形根节点");
                go.transform.SetParent(manager.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                root = go.transform;
            }

            geometryRoot.objectReferenceValue = root;
            serializedObject.ApplyModifiedProperties();
            return root;
        }

        static string GetGeometryPrefix(GeometryKind kind)
        {
            switch (kind)
            {
                case GeometryKind.Boundary: return "Boundary_";
                case GeometryKind.SolidBlock: return "Block_";
                default: return "Platform_";
            }
        }

        static Color GetGeometryColor(GeometryKind kind)
        {
            switch (kind)
            {
                case GeometryKind.Boundary: return new Color(1f, 0.25f, 0.25f, 0.35f);
                case GeometryKind.SolidBlock: return new Color(0.7f, 0.7f, 0.7f, 0.8f);
                default: return Color.white;
            }
        }


        void DrawEnemyPlacementTools()
        {
            DrawSelectedEnemyLevelSettings();
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("放置敌兵", EditorStyles.boldLabel);
            enemyPrefab = (GameObject)EditorGUILayout.ObjectField("敌兵预制体", enemyPrefab, typeof(GameObject), false);
            enemySnapToGround = EditorGUILayout.Toggle("自动贴地", enemySnapToGround);
            if (enemySnapToGround)
                enemySnapOffsetY = EditorGUILayout.FloatField("离地高度", enemySnapOffsetY);

            using (new EditorGUI.DisabledScope(enemyPrefab == null))
                enemyPlacementActive = GUILayout.Toggle(enemyPlacementActive, "在场景中放置敌兵（Ctrl+点击结束）", GUI.skin.button, GUILayout.Height(30));
        }

        void OnSceneGUI(SceneView sceneView)
        {
            var e = Event.current;
            if (currentTab == EditorTab.Enemies)
                DrawSelectedEnemyLevelHandles();

            if (!enemyPlacementActive) return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.type == EventType.MouseDown && e.button == 0 && e.control)
            {
                enemyPlacementActive = false;
                e.Use();
                Repaint();
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var worldPos = (Vector2)ray.origin;
            if (enemySnapToGround)
                worldPos = SnapEnemyToGround(worldPos);

            Handles.color = Color.red;
            Handles.DrawWireDisc(worldPos, Vector3.forward, 0.25f);
            SceneView.RepaintAll();

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceEnemy(worldPos);
                e.Use();
            }
        }

        static EnemyAI GetSelectedEnemy()
        {
            var selected = Selection.activeGameObject;
            return selected ? selected.GetComponentInParent<EnemyAI>() : null;
        }

        void DrawSelectedEnemyLevelSettings()
        {
            EditorGUILayout.LabelField("当前敌兵", EditorStyles.boldLabel);
            var enemy = GetSelectedEnemy();
            EditorGUILayout.ObjectField("敌兵", enemy, typeof(EnemyAI), true);
            if (!enemy)
            {
                EditorGUILayout.HelpBox("在左侧层级中选中敌兵后，可配置探测范围和左右巡逻边界。", MessageType.Info);
                return;
            }

            var enemyObject = new SerializedObject(enemy);
            enemyObject.Update();
            var detection = enemyObject.FindProperty("overrideDetectionSize");
            var left = enemyObject.FindProperty("patrolLeftOffset");
            var right = enemyObject.FindProperty("patrolRightOffset");

            EditorGUI.BeginChangeCheck();
            var currentDetection = enemy.DetectionSize;
            var nextDetection = ClampSize(EditorGUILayout.Vector2Field("探测矩形 宽 / 高", currentDetection));
            var leftDistance = Mathf.Max(0f, EditorGUILayout.FloatField("向左巡逻距离", Mathf.Abs(enemy.PatrolLeftOffset)));
            var rightDistance = Mathf.Max(0f, EditorGUILayout.FloatField("向右巡逻距离", Mathf.Abs(enemy.PatrolRightOffset)));

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(enemy, "修改敌兵关卡配置");
                detection.vector2Value = nextDetection;
                left.floatValue = -leftDistance;
                right.floatValue = rightDistance;
                enemyObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(enemy);
                EditorSceneManager.MarkSceneDirty(enemy.gameObject.scene);
                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox("黄色矩形是探测区域；绿色左右端点是巡逻边界。两者都可以直接在场景中拖动。", MessageType.Info);
        }

        void DrawSelectedEnemyLevelHandles()
        {
            var enemy = GetSelectedEnemy();
            if (!enemy) return;

            var origin = enemy.transform.position;
            DrawDetectionHandle(enemy, origin);
            DrawPatrolHandles(enemy, origin);
        }

        void DrawDetectionHandle(EnemyAI enemy, Vector3 origin)
        {
            var size = enemy.DetectionSize;
            Handles.color = new Color(1f, 0.9f, 0f, 1f);
            Handles.DrawWireCube(origin, size);

            enemyDetectionHandle.center = origin;
            enemyDetectionHandle.size = new Vector3(size.x, size.y, 0.01f);
            enemyDetectionHandle.handleColor = new Color(1f, 0.9f, 0f, 1f);
            enemyDetectionHandle.wireframeColor = new Color(1f, 0.9f, 0f, 0.9f);

            EditorGUI.BeginChangeCheck();
            enemyDetectionHandle.DrawHandle();
            if (!EditorGUI.EndChangeCheck()) return;

            var nextSize = ClampSize(new Vector2(enemyDetectionHandle.size.x, enemyDetectionHandle.size.y));
            Undo.RecordObject(enemy, "修改敌兵探测范围");
            var enemyObject = new SerializedObject(enemy);
            enemyObject.FindProperty("overrideDetectionSize").vector2Value = nextSize;
            enemyObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(enemy);
            EditorSceneManager.MarkSceneDirty(enemy.gameObject.scene);
            Repaint();
        }

        void DrawPatrolHandles(EnemyAI enemy, Vector3 origin)
        {
            var leftPoint = new Vector3(origin.x + enemy.PatrolLeftOffset, origin.y, origin.z);
            var rightPoint = new Vector3(origin.x + enemy.PatrolRightOffset, origin.y, origin.z);
            Handles.color = new Color(0.2f, 1f, 0.35f, 1f);
            Handles.DrawAAPolyLine(3f, leftPoint, rightPoint);
            Handles.DrawDottedLine(leftPoint + Vector3.down * 0.6f, leftPoint + Vector3.up * 0.6f, 4f);
            Handles.DrawDottedLine(rightPoint + Vector3.down * 0.6f, rightPoint + Vector3.up * 0.6f, 4f);

            var handleSize = HandleUtility.GetHandleSize(origin) * 0.08f;
            EditorGUI.BeginChangeCheck();
            var nextLeft = Handles.FreeMoveHandle(leftPoint, handleSize, Vector3.zero, Handles.RectangleHandleCap);
            var nextRight = Handles.FreeMoveHandle(rightPoint, handleSize, Vector3.zero, Handles.RectangleHandleCap);
            if (!EditorGUI.EndChangeCheck()) return;

            var leftOffset = Mathf.Min(-0.05f, nextLeft.x - origin.x);
            var rightOffset = Mathf.Max(0.05f, nextRight.x - origin.x);
            Undo.RecordObject(enemy, "修改敌兵巡逻边界");
            var enemyObject = new SerializedObject(enemy);
            enemyObject.FindProperty("patrolLeftOffset").floatValue = leftOffset;
            enemyObject.FindProperty("patrolRightOffset").floatValue = rightOffset;
            enemyObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(enemy);
            EditorSceneManager.MarkSceneDirty(enemy.gameObject.scene);
            Repaint();
        }

        static Vector2 ClampSize(Vector2 size)
        {
            return new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        }

        Vector2 SnapEnemyToGround(Vector2 pos)
        {
            var hit = Physics2D.Raycast(pos + Vector2.up * 10f, Vector2.down, 20f, LayerMask.GetMask("Ground"));
            if (hit.collider != null)
                return new Vector2(pos.x, hit.point.y + Mathf.Abs(enemySnapOffsetY));
            return pos;
        }

        void PlaceEnemy(Vector2 position)
        {
            if (!enemyPrefab) return;
            serializedObject.ApplyModifiedProperties();

            var parent = EnsureEnemyRoot();
            var created = PrefabUtility.InstantiatePrefab(enemyPrefab) as GameObject;
            if (!created)
                created = Instantiate(enemyPrefab);

            Undo.RegisterCreatedObjectUndo(created, "放置敌兵");
            created.transform.SetParent(parent);
            created.transform.position = position;
            created.name = $"{enemyPrefab.name}_{System.Guid.NewGuid().ToString("N").Substring(0, 4)}";

            EditorUtility.SetDirty(created);
            EditorSceneManager.MarkSceneDirty(created.scene);
            Selection.activeGameObject = created;
            manager.CollectAll();
        }

        Transform EnsureEnemyRoot()
        {
            serializedObject.ApplyModifiedProperties();
            var gameplay = manager.GameplayRoot;
            if (!gameplay) return manager.transform;
            var root = gameplay.Find("Enemies");
            if (root) return root;

            var go = new GameObject("Enemies");
            Undo.RegisterCreatedObjectUndo(go, "创建敌人根节点");
            go.transform.SetParent(gameplay);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        void DrawExportTools()
        {
            EditorGUILayout.HelpBox("把当前场景里的 LevelManager 内容导出/更新为 LevelConfig。", MessageType.Info);
            if (GUILayout.Button("导出当前关卡配置", GUILayout.Height(32)))
            {
                serializedObject.ApplyModifiedProperties();
                LevelConfigExporter.Export(manager);
            }
        }

        void DrawPlatformArtTools()
        {
            var selectedPlatforms = Selection.gameObjects
                .Where(LevelPlatformVisualUtility.LooksLikePlatform)
                .ToArray();

            EditorGUILayout.HelpBox("Select platform objects, then click 01-13 to apply a sliced platform sprite. Collision stays unchanged; only PlatformVisual is updated.", MessageType.Info);
            EditorGUILayout.LabelField($"Selected platforms: {selectedPlatforms.Length}");

            using (new EditorGUI.DisabledScope(selectedPlatforms.Length == 0))
            {
                for (var row = 0; row < 4; row++)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (var col = 0; col < 4; col++)
                    {
                        var index = row * 4 + col + 1;
                        if (index > LevelPlatformVisualUtility.PlatformSpriteCount) break;

                        if (GUILayout.Button(index.ToString("00"), GUILayout.Height(28)))
                        {
                            ApplyPlatformVisualToSelection(selectedPlatforms, index);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Remove Platform Visual", GUILayout.Height(24)))
                {
                    foreach (var platform in selectedPlatforms)
                    {
                        LevelPlatformVisualUtility.RemovePlatformVisual(platform);
                        EditorSceneManager.MarkSceneDirty(platform.scene);
                    }
                }
            }
        }

        void ApplyPlatformVisualToSelection(GameObject[] platforms, int spriteIndex)
        {
            foreach (var platform in platforms)
            {
                if (LevelPlatformVisualUtility.ApplyPlatformVisual(platform, spriteIndex))
                {
                    EditorSceneManager.MarkSceneDirty(platform.scene);
                }
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
