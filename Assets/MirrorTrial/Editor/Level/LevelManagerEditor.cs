using System.Collections.Generic;
using System.Linq;
using MirrorTrial.Enemies;
using MirrorTrial.HealthResources;
using MirrorTrial.Editor.HealthResources;
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
        SerializedProperty levelConfig;
        SerializedProperty levelId;
        SerializedProperty levelDisplayName;
        SerializedProperty playerSpawn;
        SerializedProperty playerPrefab;
        SerializedProperty geometryRoot;
        SerializedProperty gameplayRoot;
        SerializedProperty runtimeRoot;
        SerializedProperty autoCollectOnAwake;
        SerializedProperty limitCameraToVisibleArea;
        SerializedProperty cameraVisibleArea;
        SerializedProperty waterSurface;

        enum GeometryKind
        {
            Platform,
            Boundary,
            SolidBlock
        }

        GeometryKind geometryKind = GeometryKind.Platform;
        Vector2 geometrySize = new Vector2(4f, 1f);
        bool showAllPlatforms;
        bool geometryPlacementActive;
        enum GameplayPlacementKind
        {
            Segment,
            Trigger,
            Encounter,
            MirrorGate,
            Gate
        }
        GameplayPlacementKind gameplayPlacementKind;
        bool gameplayPlacementActive;
        GameObject mirrorGatePrefab;
        GameObject enemyPrefab;
        bool enemyPlacementActive;
        GameObject healthResourcePrefab;
        HealthResourceCatalog healthResourceCatalog;
        int selectedHealthResourceIndex = -1;
        bool healthResourcePlacementActive;
        readonly BoxBoundsHandle enemyDetectionHandle = new BoxBoundsHandle();
        readonly BoxBoundsHandle cameraVisibleAreaHandle = new BoxBoundsHandle();
        bool cameraAreaEditing;
        EditorTab currentTab;

        void OnEnable()
        {
            manager = target as MirrorTrial.Level.LevelManager;
            levelConfig = serializedObject.FindProperty("levelConfig");
            levelId = serializedObject.FindProperty("levelId");
            levelDisplayName = serializedObject.FindProperty("levelDisplayName");
            playerSpawn = serializedObject.FindProperty("playerSpawn");
            playerPrefab = serializedObject.FindProperty("playerPrefab");
            geometryRoot = serializedObject.FindProperty("geometryRoot");
            gameplayRoot = serializedObject.FindProperty("gameplayRoot");
            runtimeRoot = serializedObject.FindProperty("runtimeRoot");
            autoCollectOnAwake = serializedObject.FindProperty("AutoCollectOnAwake");
            limitCameraToVisibleArea = serializedObject.FindProperty("limitCameraToVisibleArea");
            cameraVisibleArea = serializedObject.FindProperty("cameraVisibleArea");
            waterSurface = serializedObject.FindProperty("waterSurface");
            healthResourceCatalog = HealthResourceCatalogEditorUtility.GetOrCreate();
            if (healthResourceCatalog.Prefabs.Count == 0)
                HealthResourceCatalogEditorUtility.Rebuild();
            SelectFirstAvailableHealthResource();
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
            if (currentTab != tab)
                StopPlacementModes();
            currentTab = tab;
            serializedObject.Update();

            switch (tab)
            {
                case EditorTab.Level:
                    DrawLevelSettings();
                    break;
                case EditorTab.Geometry:
                    DrawGeometryTools();
                    break;
                case EditorTab.Gameplay:
                    DrawAddButtons();
                    EditorGUILayout.Space(12);
                    DrawHealthResourcePlacementTools();
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
            EditorGUILayout.PropertyField(levelConfig, new GUIContent("关卡配置"));
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

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("相机视野范围", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这里填写画面允许看到的完整区域，不是相机中心范围。运行时会自动扣除半个屏幕尺寸。", MessageType.Info);
            EditorGUILayout.PropertyField(limitCameraToVisibleArea, new GUIContent("限制相机视野"));
            if (limitCameraToVisibleArea.boolValue)
            {
                EditorGUILayout.HelpBox("可见区域请在 Scene 视图中直接拖动蓝色矩形边框设置。", MessageType.Info);
                var editLabel = cameraAreaEditing ? "停止编辑可见区域" : "在 Scene 中框选可见区域";
                if (GUILayout.Button(editLabel, GUILayout.Height(28)))
                {
                    cameraAreaEditing = !cameraAreaEditing;
                    if (cameraAreaEditing)
                        FocusCameraVisibleArea();
                    SceneView.RepaintAll();
                }
            }
            else
            {
                cameraAreaEditing = false;
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("动态水域", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(waterSurface, new GUIContent("水域对象"));
            var water = waterSurface.objectReferenceValue as LevelWaterSurface;
            if (water)
            {
                var waterObject = new SerializedObject(water);
                waterObject.Update();
                EditorGUILayout.PropertyField(waterObject.FindProperty("waterArea"), new GUIContent("水域范围"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("deathDepth"), new GUIContent("落水死亡深度"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("shallowColor"), new GUIContent("浅水颜色"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("deepColor"), new GUIContent("深水颜色"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("highlightColor"), new GUIContent("高光颜色"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("flowSpeed"), new GUIContent("流动速度"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("waveScale"), new GUIContent("波纹尺寸"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("pixelDensity"), new GUIContent("像素密度"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("backgroundBlendHeight"), new GUIContent("远景融合高度"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("backgroundBlendStrength"), new GUIContent("远景融合强度"));
                EditorGUILayout.PropertyField(waterObject.FindProperty("backgroundBlendColor"), new GUIContent("远景融合颜色"));
                if (waterObject.ApplyModifiedProperties())
                {
                    water.Refresh();
                    EditorUtility.SetDirty(water);
                    EditorSceneManager.MarkSceneDirty(water.gameObject.scene);
                }
                if (GUILayout.Button("在场景中选中水域")) Selection.activeObject = water.gameObject;
            }
            else if (GUILayout.Button("创建动态水域", GUILayout.Height(28)))
            {
                CreateDynamicWater();
                serializedObject.Update();
            }
        }

        void CreateDynamicWater()
        {
            const string materialPath = "Assets/MirrorTrial/Materials/Level02_PixelWater.mat";
            var oldWater = GameObject.Find("底部水层_运行时可见");
            if (oldWater) Undo.DestroyObjectImmediate(oldWater);

            var shader = Shader.Find("MirrorTrial/Pixel Water");
            if (!shader)
            {
                Debug.LogError("找不到动态水 Shader：MirrorTrial/Pixel Water");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(shader) { name = "Level02_PixelWater" };
                AssetDatabase.CreateAsset(material, materialPath);
                AssetDatabase.SaveAssets();
            }

            var go = new GameObject("动态像素水域", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D), typeof(LevelWaterSurface));
            Undo.RegisterCreatedObjectUndo(go, "创建动态像素水域");
            go.transform.SetParent(manager.GeometryRoot ? manager.GeometryRoot : manager.transform, false);
            var water = go.GetComponent<LevelWaterSurface>();
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            var waterObject = new SerializedObject(water);
            waterObject.FindProperty("waterArea").rectValue = new Rect(-6f, -8.8f, 23f, 2.7f);
            waterObject.ApplyModifiedPropertiesWithoutUndo();
            water.Refresh();

            waterSurface.objectReferenceValue = water;
            limitCameraToVisibleArea.boolValue = true;
            cameraVisibleArea.rectValue = new Rect(-6f, -6.8f, 23f, 10f);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(water);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Selection.activeObject = water.gameObject;
        }
        void DrawAddButtons()
        {
            EditorGUILayout.LabelField("场景放置", EditorStyles.boldLabel);
            mirrorGatePrefab = (GameObject)EditorGUILayout.ObjectField("镜子门 Prefab", mirrorGatePrefab, typeof(GameObject), false);
            if (!mirrorGatePrefab && gameplayPlacementKind == GameplayPlacementKind.MirrorGate)
                gameplayPlacementActive = false;

            EditorGUILayout.BeginHorizontal();
            DrawGameplayPlacementButton("段落", GameplayPlacementKind.Segment);
            DrawGameplayPlacementButton("触发器", GameplayPlacementKind.Trigger);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawGameplayPlacementButton("战斗区", GameplayPlacementKind.Encounter);
            using (new EditorGUI.DisabledScope(!mirrorGatePrefab))
                DrawGameplayPlacementButton("镜子门", GameplayPlacementKind.MirrorGate);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawGameplayPlacementButton("门", GameplayPlacementKind.Gate);
            EditorGUILayout.EndHorizontal();

            if (!mirrorGatePrefab)
                EditorGUILayout.HelpBox("放置镜子门前，请先拖入镜子门外观 Prefab。不会自动使用通用 Prefab。", MessageType.Info);
            if (gameplayPlacementActive)
                EditorGUILayout.HelpBox("在场景中点击一次完成放置。", MessageType.Info);
        }

        void DrawGameplayPlacementButton(string label, GameplayPlacementKind kind)
        {
            var selected = gameplayPlacementActive && gameplayPlacementKind == kind;
            var next = GUILayout.Toggle(selected, selected ? $"正在放置{label}" : $"放置{label}", GUI.skin.button, GUILayout.Height(28));
            if (next == selected) return;

            gameplayPlacementActive = next;
            gameplayPlacementKind = kind;
            if (next)
            {
                geometryPlacementActive = false;
                enemyPlacementActive = false;
                healthResourcePlacementActive = false;
            }
            SceneView.RepaintAll();
        }

        void StopPlacementModes()
        {
            geometryPlacementActive = false;
            gameplayPlacementActive = false;
            enemyPlacementActive = false;
            healthResourcePlacementActive = false;
            SceneView.RepaintAll();
        }

        void DrawHealthResourcePlacementTools()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("生命资源仓库", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新索引", GUILayout.Width(72f)))
            {
                HealthResourceCatalogEditorUtility.Rebuild();
                healthResourceCatalog = HealthResourceCatalogEditorUtility.GetOrCreate();
                SelectFirstAvailableHealthResource();
            }
            EditorGUILayout.EndHorizontal();

            if (!healthResourceCatalog || healthResourceCatalog.Prefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("资源目录中还没有生命资源。请先在生命资源编辑器中制作并登记 Prefab。", MessageType.Warning);
            }
            else
            {
                DrawHealthResourceWarehouse();
                EditorGUILayout.HelpBox("点击仓库中的资源卡片进行选择，再开启放置；在 Scene 视图左键点击一次完成布置。", MessageType.Info);
            }

            var valid = healthResourcePrefab && healthResourcePrefab.GetComponent<HealthResourceNode>();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("打开生命资源编辑器", GUILayout.Height(28)))
                EditorApplication.ExecuteMenuItem("Tools/Mirror Trial/关卡/生命资源编辑器");

            using (new EditorGUI.DisabledScope(!valid))
            {
                var next = GUILayout.Toggle(healthResourcePlacementActive,
                    healthResourcePlacementActive ? "请在场景中点击放置" : "在场景中放置",
                    GUI.skin.button, GUILayout.Height(28));
                if (next != healthResourcePlacementActive)
                {
                    healthResourcePlacementActive = next;
                    if (next)
                    {
                        geometryPlacementActive = false;
                        gameplayPlacementActive = false;
                        enemyPlacementActive = false;
                    }
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawHealthResourceWarehouse()
        {
            const int columns = 3;
            var entries = healthResourceCatalog.Prefabs;
            for (var row = 0; row * columns < entries.Count; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (var column = 0; column < columns; column++)
                {
                    var index = row * columns + column;
                    if (index >= entries.Count)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    var prefab = entries[index];
                    if (!prefab) continue;
                    var preview = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
                    var selected = index == selectedHealthResourceIndex;
                    var previous = GUI.backgroundColor;
                    if (selected) GUI.backgroundColor = new Color(0.35f, 0.9f, 0.6f, 1f);
                    var content = new GUIContent(prefab.name, preview, "选择 " + prefab.name);
                    if (GUILayout.Button(content, GUILayout.Height(64f), GUILayout.MinWidth(100f)))
                    {
                        selectedHealthResourceIndex = index;
                        healthResourcePrefab = prefab;
                        healthResourcePlacementActive = false;
                        EditorGUIUtility.PingObject(prefab);
                    }
                    GUI.backgroundColor = previous;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (healthResourcePrefab)
                EditorGUILayout.LabelField("当前选择", healthResourcePrefab.name);
        }

        void SelectFirstAvailableHealthResource()
        {
            healthResourcePrefab = null;
            selectedHealthResourceIndex = -1;
            if (!healthResourceCatalog) return;
            for (var i = 0; i < healthResourceCatalog.Prefabs.Count; i++)
            {
                var prefab = healthResourceCatalog.Prefabs[i];
                if (!prefab || !prefab.GetComponent<HealthResourceNode>()) continue;
                selectedHealthResourceIndex = i;
                healthResourcePrefab = prefab;
                return;
            }
        }

        void PlaceHealthResource(Vector2 position)
        {
            if (!healthResourcePrefab || !healthResourcePrefab.GetComponent<HealthResourceNode>()) return;
            serializedObject.ApplyModifiedProperties();

            var parent = EnsureHealthResourceRoot();
            var created = PrefabUtility.InstantiatePrefab(healthResourcePrefab) as GameObject;
            if (!created) created = Instantiate(healthResourcePrefab);
            Undo.RegisterCreatedObjectUndo(created, "放置生命资源");
            created.transform.SetParent(parent);
            created.transform.position = position;
            created.name = manager.GetUniqueName(healthResourcePrefab.name + "_01");
            EditorUtility.SetDirty(created);
            EditorSceneManager.MarkSceneDirty(created.scene);
            Selection.activeGameObject = created;
            manager.CollectAll();
        }

        Transform EnsureHealthResourceRoot()
        {
            serializedObject.ApplyModifiedProperties();
            var gameplay = manager.GameplayRoot;
            if (!gameplay) return manager.transform;
            var root = gameplay.Find("生命资源");
            if (root) return root;

            var go = new GameObject("生命资源");
            Undo.RegisterCreatedObjectUndo(go, "创建生命资源根节点");
            go.transform.SetParent(gameplay);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        void DrawGeometryTools()
        {
            geometryKind = (GeometryKind)EditorGUILayout.Popup("地形类型", (int)geometryKind, new[] { "平台", "边界", "实体块" });
            geometrySize = EditorGUILayout.Vector2Field("默认尺寸", geometrySize);
            showAllPlatforms = EditorGUILayout.Toggle(
                new GUIContent("显示当前所有平台", "在 Scene 视图中显示平台碰撞范围和位置"),
                showAllPlatforms);

            EditorGUILayout.HelpBox("这里只创建不可见的 BoxCollider2D。平台美术请在独立美术层中摆放。", MessageType.Info);

            DrawGeometryPreview();

            var next = GUILayout.Toggle(geometryPlacementActive,
                geometryPlacementActive ? "请在场景中点击放置" : "在场景中放置",
                GUI.skin.button, GUILayout.Height(30));
            if (next != geometryPlacementActive)
            {
                geometryPlacementActive = next;
                if (next)
                {
                    gameplayPlacementActive = false;
                    enemyPlacementActive = false;
                    healthResourcePlacementActive = false;
                }
                SceneView.RepaintAll();
            }
        }

        void AddGeometryObject(GeometryKind kind, Vector2 position)
        {
            serializedObject.ApplyModifiedProperties();

            var root = EnsureGeometryRoot();
            var prefix = GetGeometryPrefix(kind);
            var go = new GameObject(manager.GetUniqueName(prefix + "01"));
            Undo.RegisterCreatedObjectUndo(go, "添加地形");
            go.transform.SetParent(root);
            go.transform.position = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            ApplyGroundLayer(go);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = geometrySize;
            col.isTrigger = false;

            ApplyGroundLayerRecursive(go);

            Selection.activeGameObject = go;
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

            EditorGUI.DrawRect(shapeRect, new Color(0.32f, 0.24f, 0.52f, 0.22f));

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

            using (new EditorGUI.DisabledScope(enemyPrefab == null))
            {
                var next = GUILayout.Toggle(enemyPlacementActive, "在场景中放置敌兵", GUI.skin.button, GUILayout.Height(30));
                if (next != enemyPlacementActive)
                {
                    enemyPlacementActive = next;
                    if (next)
                    {
                        geometryPlacementActive = false;
                        gameplayPlacementActive = false;
                        healthResourcePlacementActive = false;
                    }
                }
            }
        }

        void OnSceneGUI(SceneView sceneView)
        {
            var e = Event.current;
            if (currentTab == EditorTab.Geometry && showAllPlatforms)
                DrawGroundCollidersInScene();

            if (currentTab == EditorTab.Level && cameraAreaEditing && limitCameraToVisibleArea.boolValue)
                DrawCameraVisibleAreaHandle();

            if (currentTab == EditorTab.Enemies)
                DrawSelectedEnemyLevelHandles();

            if (currentTab == EditorTab.Geometry && geometryPlacementActive)
            {
                var position = GetSceneMousePosition(e);
                DrawPlacementPreview(position, new Color(0.65f, 0.45f, 1f, 1f));
                if (HandlePlacementClick(e, () => AddGeometryObject(geometryKind, position), () => geometryPlacementActive = false))
                    return;
            }

            if (currentTab == EditorTab.Gameplay && gameplayPlacementActive)
            {
                var position = GetSceneMousePosition(e);
                DrawPlacementPreview(position, new Color(0.2f, 0.85f, 1f, 1f));
                if (HandlePlacementClick(e, () => PlaceGameplayObject(position), () => gameplayPlacementActive = false))
                    return;
            }

            if (currentTab == EditorTab.Gameplay && healthResourcePlacementActive)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                var resourceRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                var resourcePosition = (Vector2)resourceRay.origin;
                Handles.color = new Color(0.2f, 1f, 0.55f, 1f);
                Handles.DrawWireDisc(resourcePosition, Vector3.forward, 0.3f);
                SceneView.RepaintAll();

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    PlaceHealthResource(resourcePosition);
                    healthResourcePlacementActive = false;
                    e.Use();
                    Repaint();
                }
                return;
            }

            if (!enemyPlacementActive) return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var worldPos = GetSceneMousePosition(e);

            Handles.color = Color.red;
            Handles.DrawWireDisc(worldPos, Vector3.forward, 0.25f);
            SceneView.RepaintAll();

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceEnemy(worldPos);
                enemyPlacementActive = false;
                e.Use();
                Repaint();
            }
        }

        void FocusCameraVisibleArea()
        {
            var rect = cameraVisibleArea.rectValue;
            var center = new Vector3(rect.center.x, rect.center.y, manager.transform.position.z);
            var size = new Vector3(Mathf.Max(0.01f, rect.width), Mathf.Max(0.01f, rect.height), 0.01f);
            if (SceneView.lastActiveSceneView)
            {
                SceneView.lastActiveSceneView.Frame(new Bounds(center, size), false);
                SceneView.lastActiveSceneView.Focus();
            }
        }

        void DrawCameraVisibleAreaHandle()
        {
            serializedObject.Update();
            var rect = cameraVisibleArea.rectValue;
            cameraVisibleAreaHandle.center = new Vector3(rect.center.x, rect.center.y, manager.transform.position.z);
            cameraVisibleAreaHandle.size = new Vector3(
                Mathf.Max(0.01f, rect.width),
                Mathf.Max(0.01f, rect.height),
                0.01f);
            cameraVisibleAreaHandle.handleColor = new Color(0.25f, 0.85f, 1f, 1f);
            cameraVisibleAreaHandle.wireframeColor = new Color(0.25f, 0.85f, 1f, 0.9f);

            EditorGUI.BeginChangeCheck();
            cameraVisibleAreaHandle.DrawHandle();
            if (!EditorGUI.EndChangeCheck()) return;

            var center = cameraVisibleAreaHandle.center;
            var size = cameraVisibleAreaHandle.size;
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);

            Undo.RecordObject(manager, "修改相机可见区域");
            cameraVisibleArea.rectValue = new Rect(
                center.x - size.x * 0.5f,
                center.y - size.y * 0.5f,
                size.x,
                size.y);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Repaint();
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

        static Vector2 GetSceneMousePosition(Event e)
        {
            return (Vector2)HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        }

        void DrawPlacementPreview(Vector2 position, Color color)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Handles.color = color;
            Handles.DrawWireDisc(position, Vector3.forward, 0.25f);
            SceneView.RepaintAll();
        }

        bool HandlePlacementClick(Event e, System.Action place, System.Action cancel)
        {
            if (e.type != EventType.MouseDown || e.button != 0) return false;
            place();
            cancel();
            Repaint();
            e.Use();
            return true;
        }

        void PlaceGameplayObject(Vector2 position)
        {
            switch (gameplayPlacementKind)
            {
                case GameplayPlacementKind.Segment:
                    CreateGameplayObject<LevelSegment>("段落", "SEG_", position);
                    break;
                case GameplayPlacementKind.Trigger:
                    CreateGameplayObject<LevelTrigger>("触发器", "TR_", position);
                    break;
                case GameplayPlacementKind.Encounter:
                    CreateGameplayObject<CombatEncounter>("战斗区", "Combat_", position);
                    break;
                case GameplayPlacementKind.MirrorGate:
                    CreateMirrorGate(position);
                    break;
                case GameplayPlacementKind.Gate:
                    CreateGameplayObject<AreaGate>("门", "Gate_", position);
                    break;
            }
        }

        void CreateGameplayObject<T>(string category, string prefix, Vector2 position) where T : Component
        {
            var created = manager.CreateGameplayObject<T>(category, manager.GetUniqueName(prefix + "01"), position);
            FinalizeGameplayPlacement(created);
        }

        void CreateMirrorGate(Vector2 position)
        {
            if (!mirrorGatePrefab) return;
            var gate = manager.CreateGameplayObject<MirrorGate>("镜子门", manager.GetUniqueName("MirrorGate_01"), position);
            if (!gate) return;

            var gateObject = new SerializedObject(gate);
            gateObject.FindProperty("mirrorPrefab").objectReferenceValue = mirrorGatePrefab;
            gateObject.ApplyModifiedPropertiesWithoutUndo();

            var visual = PrefabUtility.InstantiatePrefab(mirrorGatePrefab, gate.transform) as GameObject;
            if (!visual)
            {
                visual = Instantiate(mirrorGatePrefab, gate.transform);
                visual.name = mirrorGatePrefab.name;
            }
            if (visual) Undo.RegisterCreatedObjectUndo(visual, "创建镜子门外观");
            FinalizeGameplayPlacement(gate);
        }

        void FinalizeGameplayPlacement(Component created)
        {
            if (!created) return;
            Selection.activeGameObject = created.gameObject;
            EditorUtility.SetDirty(created);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(created.gameObject.scene);
            manager.CollectAll();
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

        void DrawGroundCollidersInScene()
        {
            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) return;

            var colliders = Resources.FindObjectsOfTypeAll<Collider2D>()
                .Where(collider => collider
                    && collider.gameObject.scene == manager.gameObject.scene
                    && collider.gameObject.layer == groundLayer)
                .ToArray();

            var oldColor = Handles.color;
            Handles.color = new Color(0.2f, 1f, 0.35f, 0.9f);
            foreach (var collider in colliders)
            {
                var bounds = collider.bounds;
                Handles.DrawWireCube(bounds.center, bounds.size);
                Handles.Label(bounds.center,
                    $"{collider.name}  ({bounds.center.x:0.##}, {bounds.center.y:0.##})",
                    EditorStyles.whiteMiniLabel);
            }
            Handles.color = oldColor;
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
