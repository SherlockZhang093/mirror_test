using MirrorTrial.Level;
using MirrorTrial.Enemies;
using MirrorTrial.Combat;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

namespace MirrorTrial.Editor.Enemies
{
    public class EnemyWaveEditor : EditorWindow
    {
        const string Title = "敌人波次编辑器";
        const string MenuPath = "Tools/镜像试炼/关卡/敌人波次编辑器";

        // === 波次放置面板 ===
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] EnemyAIProfile aiProfile;
        [SerializeField] CombatEncounter targetEncounter;
        [SerializeField] int waveIndex;
        [SerializeField] SpawnPointRole role = SpawnPointRole.Melee;
        [SerializeField] float facingDegrees = 180f;
        [SerializeField] int overrideHitPoints = -1;
        [SerializeField] float overrideMoveSpeed = -1f;

        bool isPlacing;
        int pendingCount = 1;
        float pendingDelay;
        float pendingInterval;
        bool autoGroupIntoWave = true;
        bool snapToGround = true;
        float snapOffsetY = -1f;

        // === 预制体生成面板 ===
        bool showPrefabCreator = true;
        string newEnemyName = "NewEnemy";
        Color newEnemyColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        Vector2 newEnemySize = new Vector2(0.7f, 1.2f);
        int newEnemyLayer = 7;
        EnemyAIProfile newEnemyProfile;
        bool newEnemyAutoSelect = true;

        // 预设模板
        enum EnemyTemplate { 自定义, 近战兵, 远程兵, 精英兵 }
        EnemyTemplate selectedTemplate = EnemyTemplate.自定义;

        Vector2 scrollPos;

        // 已知 GUID 常量
        const string GUID_HURTBOX = "ed3a54687cf32534a8742db499d5a8dc";
        const string GUID_ENEMY_AI = "047623e3bf0e4708a501c691457132d1";
        const string GUID_AI_PROFILE = "64bb7804a99142779f6df45df7de92de";
        const string GUID_WHITE_SQUARE = "878d9104245e32d49b7351c8b8387bd6";

        const string PREFAB_OUTPUT_PATH = "Assets/MirrorTrial/Prefabs/Enemies/Characters";
        const string PROFILE_OUTPUT_PATH = "Assets/MirrorTrial/Prefabs/Enemies/AIProfiles";

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            GetWindow<EnemyWaveEditor>(Title);
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawPrefabCreatorPanel();
            EditorGUILayout.Space(10);
            DrawWavePlacementPanel();

            EditorGUILayout.EndScrollView();
            Repaint();
        }

        // ============================================================
        //  预制体生成器面板
        // ============================================================
        void DrawPrefabCreatorPanel()
        {
            showPrefabCreator = EditorGUILayout.BeginFoldoutHeaderGroup(showPrefabCreator, "一键生成敌人预制体");
            if (!showPrefabCreator)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.HelpBox(
                "配置参数后点击生成，将自动创建包含全套组件的预制体：\n" +
                "SpriteRenderer + BoxCollider2D + Hurtbox + Rigidbody2D + EnemyAI",
                MessageType.Info);

            EditorGUILayout.Space(4);

            // 模板快捷选择
            var prevTemplate = selectedTemplate;
            selectedTemplate = (EnemyTemplate)EditorGUILayout.EnumPopup("快捷模板", selectedTemplate);
            if (selectedTemplate != prevTemplate)
                ApplyTemplate(selectedTemplate);

            EditorGUILayout.Space(4);
            GUILayout.Label("基础配置", EditorStyles.boldLabel);

            newEnemyName = EditorGUILayout.TextField("名称", newEnemyName);
            newEnemyColor = EditorGUILayout.ColorField("颜色", newEnemyColor);
            newEnemySize = EditorGUILayout.Vector2Field("大小 (宽x高)", newEnemySize);
            newEnemyLayer = EditorGUILayout.LayerField("Layer", newEnemyLayer);

            EditorGUILayout.Space(4);
            GUILayout.Label("AI 配置", EditorStyles.boldLabel);

            newEnemyProfile = (EnemyAIProfile)EditorGUILayout.ObjectField(
                "AI 行为配置",
                newEnemyProfile,
                typeof(EnemyAIProfile), false);

            EditorGUILayout.HelpBox(
                "可留空 — 预制体生成后可在 Inspector 中再指定。\n" +
                "也可点击下方按钮同时生成一个新的 AI Profile。",
                MessageType.None);

            if (GUILayout.Button("同时新建 AI Profile"))
            {
                CreateNewAIProfile();
            }

            EditorGUILayout.Space(4);
            newEnemyAutoSelect = EditorGUILayout.Toggle("生成后自动选中并填入波次面板", newEnemyAutoSelect);

            EditorGUILayout.Space(8);

            // 预览颜色方块
            var previewRect = GUILayoutUtility.GetRect(60, 40, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(previewRect, newEnemyColor);
            EditorGUI.LabelField(previewRect, $"{newEnemySize.x}x{newEnemySize.y}", new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            });

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newEnemyName)))
            {
                var btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 32
                };
                if (GUILayout.Button("生成预制体", btnStyle))
                {
                    GenerateEnemyPrefab();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void ApplyTemplate(EnemyTemplate template)
        {
            switch (template)
            {
                case EnemyTemplate.近战兵:
                    newEnemyName = "Enemy_Melee";
                    newEnemyColor = new Color(0.9f, 0.25f, 0.3f, 1f);
                    newEnemySize = new Vector2(0.7f, 1.2f);
                    break;
                case EnemyTemplate.远程兵:
                    newEnemyName = "Enemy_Ranged";
                    newEnemyColor = new Color(0.2f, 0.5f, 0.9f, 1f);
                    newEnemySize = new Vector2(0.7f, 1.2f);
                    break;
                case EnemyTemplate.精英兵:
                    newEnemyName = "Enemy_Elite";
                    newEnemyColor = new Color(0.7f, 0.2f, 0.9f, 1f);
                    newEnemySize = new Vector2(0.9f, 1.5f);
                    break;
            }
        }

        void CreateNewAIProfile()
        {
            EnsureDirectoryExists(PROFILE_OUTPUT_PATH);

            string profileName = newEnemyName.Replace("Enemy_", "") + "AI";
            string assetPath = $"{PROFILE_OUTPUT_PATH}/{profileName}.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            var profile = ScriptableObject.CreateInstance<EnemyAIProfile>();
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();

            newEnemyProfile = profile;
            EditorGUIUtility.PingObject(profile);
            Debug.Log($"[敌人编辑器] 已创建 AI Profile: {assetPath}");
        }

        void GenerateEnemyPrefab()
        {
            EnsureDirectoryExists(PREFAB_OUTPUT_PATH);

            string prefabName = newEnemyName;
            if (!prefabName.StartsWith("Enemy_"))
                prefabName = "Enemy_" + prefabName;

            string prefabPath = $"{PREFAB_OUTPUT_PATH}/{prefabName}.prefab";
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

            // 创建临时 GameObject
            var go = new GameObject(prefabName);
            go.layer = newEnemyLayer;

            // SpriteRenderer
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                AssetDatabase.GUIDToAssetPath(GUID_WHITE_SQUARE));
            sr.color = newEnemyColor;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = newEnemySize;

            // BoxCollider2D
            var col = go.AddComponent<BoxCollider2D>();
            col.size = newEnemySize;
            col.isTrigger = false;

            // Hurtbox（必须在 EnemyAI 之前添加，因为 RequireComponent）
            go.AddComponent<MirrorTrial.Combat.Hurtbox>();

            // Rigidbody2D
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = 1f;

            // EnemyAI
            var ai = go.AddComponent<EnemyAI>();
            if (newEnemyProfile != null)
            {
                // 通过 SerializedObject 设置 private profile 字段
                var so = new SerializedObject(ai);
                var profileProp = so.FindProperty("profile");
                if (profileProp != null)
                {
                    profileProp.objectReferenceValue = newEnemyProfile;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 保存为预制体
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            DestroyImmediate(go);

            AssetDatabase.Refresh();

            // 自动选中并填入波次面板
            if (newEnemyAutoSelect && prefab != null)
            {
                enemyPrefab = prefab;
                aiProfile = newEnemyProfile;
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log($"[敌人编辑器] 已生成预制体: {prefabPath}\n" +
                      $"  组件: SpriteRenderer, BoxCollider2D, Hurtbox, Rigidbody2D, EnemyAI\n" +
                      $"  颜色: {newEnemyColor}, 大小: {newEnemySize}\n" +
                      $"  AI Profile: {(newEnemyProfile ? newEnemyProfile.name : "未设置")}");
        }

        static void EnsureDirectoryExists(string assetPath)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }

        // ============================================================
        //  波次放置面板
        // ============================================================
        void DrawWavePlacementPanel()
        {
            GUILayout.Label("波次放置", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.HelpBox(
                "1. 选择敌人预制体和 AI 配置\n" +
                "2. 在 Scene 视图点击地面放置刷怪点\n" +
                "3. 自动生成新波次或追加到现有波次",
                MessageType.Info);

            enemyPrefab = (GameObject)EditorGUILayout.ObjectField("敌人预制体", enemyPrefab, typeof(GameObject), false);
            aiProfile = (EnemyAIProfile)EditorGUILayout.ObjectField("AI 配置", aiProfile, typeof(EnemyAIProfile), false);
            targetEncounter = (CombatEncounter)EditorGUILayout.ObjectField("目标战斗区", targetEncounter, typeof(CombatEncounter), true);
            role = (SpawnPointRole)EditorGUILayout.EnumPopup("角色类型", role);
            facingDegrees = EditorGUILayout.FloatField("出生朝向（度）", facingDegrees);
            overrideHitPoints = EditorGUILayout.IntField("覆盖血量（-1=不覆盖）", overrideHitPoints);
            overrideMoveSpeed = EditorGUILayout.FloatField("覆盖移动速度（-1=不覆盖）", overrideMoveSpeed);

            EditorGUILayout.Space();
            GUILayout.Label("波次设置", EditorStyles.boldLabel);
            autoGroupIntoWave = EditorGUILayout.Toggle("自动归入波次", autoGroupIntoWave);
            if (autoGroupIntoWave)
            {
                waveIndex = EditorGUILayout.IntField("波次索引（超出范围则新建）", waveIndex);
                pendingCount = EditorGUILayout.IntField("本波数量", pendingCount);
                pendingDelay = EditorGUILayout.FloatField("首只延迟", pendingDelay);
                pendingInterval = EditorGUILayout.FloatField("生成间隔", pendingInterval);
            }

            EditorGUILayout.Space();
            snapToGround = EditorGUILayout.Toggle("吸附地面", snapToGround);
            if (snapToGround)
                snapOffsetY = EditorGUILayout.FloatField("地面 Y 偏移", snapOffsetY);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(targetEncounter == null))
            {
                if (GUILayout.Button("整理波次（移除空条目）"))
                    CleanupEmptyWaves();
            }

            using (new EditorGUI.DisabledScope(targetEncounter == null || targetEncounter.WaveCount == 0))
            {
                if (GUILayout.Button("打印当前波次结构"))
                    PrintWaves();
            }

            EditorGUILayout.Space();
            isPlacing = GUILayout.Toggle(isPlacing, "开启 Scene 视图放置（Ctrl+点击取消）", GUI.skin.button, GUILayout.Height(30));
        }

        // ============================================================
        //  Scene 视图交互
        // ============================================================
        void OnSceneGUI(SceneView sceneView)
        {
            if (!isPlacing)
                return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && !e.control)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector2 worldPos = ray.origin;

                if (snapToGround)
                    worldPos = SnapToGround(worldPos);

                PlaceEnemy(worldPos);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && e.control)
            {
                isPlacing = false;
                e.Use();
            }

            if (enemyPrefab != null)
            {
                Ray hoverRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector2 hoverPos = hoverRay.origin;
                if (snapToGround)
                    hoverPos = SnapToGround(hoverPos);

                Handles.color = role == SpawnPointRole.Ranged ? Color.cyan : (role == SpawnPointRole.EliteOrBoss ? Color.magenta : Color.red);
                Handles.DrawWireDisc(hoverPos, Vector3.forward, 0.25f);
                var hoverWorld = (Vector3)hoverPos;
                Handles.DrawLine(hoverWorld, hoverWorld + Vector3.right * 0.5f);
                SceneView.RepaintAll();
            }
        }

        Vector2 SnapToGround(Vector2 pos)
        {
            var hit = Physics2D.Raycast(pos + Vector2.up * 10f, Vector2.down, 20f, LayerMask.GetMask("Ground"));
            if (hit.collider != null)
                return new Vector2(pos.x, hit.point.y + Mathf.Abs(snapOffsetY));
            return pos;
        }

        // ============================================================
        //  放置逻辑
        // ============================================================
        void PlaceEnemy(Vector2 position)
        {
            if (enemyPrefab == null)
            {
                EditorUtility.DisplayDialog("缺少预制体", "请先选择一个敌人预制体，或使用上方面板一键生成。", "确定");
                return;
            }

            if (targetEncounter == null)
            {
                EditorUtility.DisplayDialog("缺少目标战斗区", "请先选择一个 CombatEncounter。", "确定");
                return;
            }

            Undo.RecordObject(targetEncounter, "放置敌人");

            var spawnRoot = FindOrCreateSpawnRoot();
            var spawnGo = new GameObject($"SP_{enemyPrefab.name}_{System.Guid.NewGuid().ToString("N").Substring(0, 4)}");
            Undo.RegisterCreatedObjectUndo(spawnGo, "创建刷怪点");
            spawnGo.transform.SetParent(spawnRoot);
            spawnGo.transform.position = position;

            var spawnPoint = spawnGo.AddComponent<SpawnPoint>();
            SetPrivate(spawnPoint, "spawnId", spawnGo.name);
            SetPrivate(spawnPoint, "role", role);
            SetPrivate(spawnPoint, "defaultEnemyPrefab", enemyPrefab);
            SetPrivate(spawnPoint, "facingDegrees", facingDegrees);
            SetPrivate(spawnPoint, "aiProfile", aiProfile);
            SetPrivate(spawnPoint, "overrideHitPoints", overrideHitPoints);
            SetPrivate(spawnPoint, "overrideMoveSpeed", overrideMoveSpeed);

            var waves = new System.Collections.Generic.List<WaveDefinition>(targetEncounter.Waves);
            int targetWave = autoGroupIntoWave ? waveIndex : -1;
            if (targetWave < 0 || targetWave >= waves.Count)
            {
                waves.Add(new WaveDefinition
                {
                    waveId = $"W{waves.Count + 1:00}_{enemyPrefab.name}",
                    clearCondition = CombatClearCondition.AllEnemiesDefeated,
                    nextWaveDelay = 0.8f,
                    spawnEntries = new WaveSpawnEntry[0]
                });
                targetWave = waves.Count - 1;
                waveIndex = targetWave;
            }

            var entry = new WaveSpawnEntry
            {
                enemyPrefab = enemyPrefab,
                spawnPoint = spawnPoint,
                count = pendingCount,
                delay = pendingDelay,
                interval = pendingInterval
            };

            var entries = new System.Collections.Generic.List<WaveSpawnEntry>(waves[targetWave].spawnEntries) { entry };
            waves[targetWave].spawnEntries = entries.ToArray();

            SetPrivate(targetEncounter, "waves", waves.ToArray());

            EditorUtility.SetDirty(targetEncounter);
            EditorUtility.SetDirty(spawnGo);
            Selection.activeGameObject = spawnGo;
            Debug.Log($"[敌人波次编辑器] 已放置 {spawnGo.name} 到 {targetEncounter.EncounterId} 的第 {targetWave} 波，位置 {position}");
        }

        Transform FindOrCreateSpawnRoot()
        {
            var manager = Object.FindObjectOfType<LevelManager>();
            Transform gameplay = manager ? manager.GameplayRoot : null;
            if (!gameplay)
            {
                var root = GameObject.Find("Level_Reality_01");
                if (root)
                    gameplay = root.transform.Find("Gameplay");
            }

            Transform spawnRoot = gameplay ? gameplay.Find("SpawnPoints") : null;
            if (!spawnRoot)
            {
                var go = new GameObject("SpawnPoints");
                if (gameplay)
                    go.transform.SetParent(gameplay);
                spawnRoot = go.transform;
            }
            return spawnRoot;
        }

        void CleanupEmptyWaves()
        {
            if (targetEncounter == null)
                return;

            Undo.RecordObject(targetEncounter, "整理空波次");
            var waves = new System.Collections.Generic.List<WaveDefinition>(targetEncounter.Waves);
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                if (waves[i].spawnEntries == null || waves[i].spawnEntries.Length == 0)
                    waves.RemoveAt(i);
            }
            SetPrivate(targetEncounter, "waves", waves.ToArray());
            EditorUtility.SetDirty(targetEncounter);
            Debug.Log($"[敌人波次编辑器] 已整理波次，剩余 {waves.Count} 波。");
        }

        void PrintWaves()
        {
            if (targetEncounter == null)
                return;

            var sb = new StringBuilder();
            sb.AppendLine($"战斗区：{targetEncounter.EncounterId}");
            int i = 0;
            foreach (var wave in targetEncounter.Waves)
            {
                sb.AppendLine($"  第 {i++} 波：{wave.waveId}");
                if (wave.spawnEntries == null)
                    continue;
                foreach (var entry in wave.spawnEntries)
                {
                    if (entry == null)
                        continue;
                    string pointName = entry.spawnPoint ? entry.spawnPoint.name : "（未设置）";
                    string enemyName = entry.enemyPrefab ? entry.enemyPrefab.name : "（未设置）";
                    sb.AppendLine($"    - {entry.count}x {enemyName} at {pointName} delay={entry.delay} interval={entry.interval}");
                }
            }
            Debug.Log(sb.ToString());
        }

        static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(target, value);
        }
    }
}
