using MirrorTrial.Enemies;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

namespace MirrorTrial.Editor.Enemies
{
    public class EnemyWaveEditor : EditorWindow
    {
        const string Title = "敌人编辑器";
        const string MenuPath = "Tools/镜像试炼/关卡/敌人编辑器";
        const string EnemyLibraryPath = "Assets/MirrorTrial/Enemies";
        const string WhiteSquareGuid = "878d9104245e32d49b7351c8b8387bd6";

        bool showExistingEnemy = true;
        bool showPrefabCreator = true;
        GameObject existingEnemyPrefab;
        UnityEditor.Editor existingEnemyEditor;
        string newEnemyId = "E001";
        string newEnemyName = "NewEnemy";
        Color newEnemyColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        Vector2 newEnemySize = new Vector2(0.7f, 1.2f);
        int newEnemyLayer = 7;
        bool newEnemyAutoSelect = true;
        Vector2 scrollPos;

        enum EnemyTemplate { Custom, Melee, Ranged, Elite }
        EnemyTemplate selectedTemplate = EnemyTemplate.Custom;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            GetWindow<EnemyWaveEditor>(Title);
        }

        void OnDisable()
        {
            if (existingEnemyEditor)
                DestroyImmediate(existingEnemyEditor);
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawExistingEnemyPanel();
            EditorGUILayout.Space(10);
            DrawPrefabCreatorPanel();
            EditorGUILayout.EndScrollView();
        }

        void DrawExistingEnemyPanel()
        {
            showExistingEnemy = EditorGUILayout.BeginFoldoutHeaderGroup(showExistingEnemy, "编辑已有敌兵");
            if (showExistingEnemy)
            {
                existingEnemyPrefab = (GameObject)EditorGUILayout.ObjectField("敌兵预制体", existingEnemyPrefab, typeof(GameObject), false);
                if (existingEnemyPrefab)
                {
                    var ai = existingEnemyPrefab.GetComponent<EnemyAI>();
                    if (!ai)
                    {
                        EditorGUILayout.HelpBox("选择的预制体没有 EnemyAI 组件。", MessageType.Warning);
                    }
                    else
                    {
                        UnityEditor.Editor.CreateCachedEditor(ai, typeof(EnemyAIEditor), ref existingEnemyEditor);
                        existingEnemyEditor.OnInspectorGUI();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("选择一个敌兵预制体后，可在这里修改 AI、攻击、碰撞框和特效。", MessageType.Info);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawPrefabCreatorPanel()
        {
            showPrefabCreator = EditorGUILayout.BeginFoldoutHeaderGroup(showPrefabCreator, "敌人资源生成");
            if (!showPrefabCreator)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.HelpBox("这里负责创建敌人资源。敌人在关卡里的放置，请到 LevelManager 的关卡编辑器里完成。", MessageType.Info);

            var prevTemplate = selectedTemplate;
            selectedTemplate = (EnemyTemplate)EditorGUILayout.EnumPopup("快捷模板", selectedTemplate);
            if (selectedTemplate != prevTemplate)
                ApplyTemplate(selectedTemplate);

            EditorGUILayout.Space(4);
            GUILayout.Label("基础配置", EditorStyles.boldLabel);
            newEnemyId = EditorGUILayout.TextField("敌兵编号", newEnemyId);
            newEnemyName = EditorGUILayout.TextField("名称", newEnemyName);
            newEnemyColor = EditorGUILayout.ColorField("颜色", newEnemyColor);
            newEnemySize = EditorGUILayout.Vector2Field("大小", newEnemySize);
            newEnemyLayer = EditorGUILayout.LayerField("Layer", newEnemyLayer);

            EditorGUILayout.HelpBox("生成预制体时会自动创建并绑定该敌兵专属的 AI 配置。", MessageType.Info);

            newEnemyAutoSelect = EditorGUILayout.Toggle("生成后自动选中", newEnemyAutoSelect);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newEnemyId) || string.IsNullOrWhiteSpace(newEnemyName)))
            {
                if (GUILayout.Button("生成预制体", GUILayout.Height(32)))
                    GenerateEnemyPrefab();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void ApplyTemplate(EnemyTemplate template)
        {
            switch (template)
            {
                case EnemyTemplate.Melee:
                    newEnemyName = "Melee";
                    newEnemyColor = new Color(0.9f, 0.25f, 0.3f, 1f);
                    newEnemySize = new Vector2(0.7f, 1.2f);
                    break;
                case EnemyTemplate.Ranged:
                    newEnemyName = "Ranged";
                    newEnemyColor = new Color(0.2f, 0.5f, 0.9f, 1f);
                    newEnemySize = new Vector2(0.7f, 1.2f);
                    break;
                case EnemyTemplate.Elite:
                    newEnemyName = "Elite";
                    newEnemyColor = new Color(0.7f, 0.2f, 0.9f, 1f);
                    newEnemySize = new Vector2(0.9f, 1.5f);
                    break;
            }
        }

        void GenerateEnemyPrefab()
        {
            if (!ValidateEnemyIdentity()) return;

            var enemyFolder = GetEnemyFolderPath();
            var prefabFolder = $"{enemyFolder}/Prefabs";
            EnsureEnemyContentFolders(enemyFolder);

            var prefabName = "Enemy_" + GetEnemyAssetPrefix();
            var prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{prefabFolder}/{prefabName}.prefab");
            var profileFolder = $"{enemyFolder}/AIProfiles";
            var profilePath = AssetDatabase.GenerateUniqueAssetPath($"{profileFolder}/{GetEnemyAssetPrefix()}AI.asset");
            var profile = ScriptableObject.CreateInstance<EnemyAIProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            var go = new GameObject(prefabName);
            go.layer = newEnemyLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(WhiteSquareGuid));
            sr.color = newEnemyColor;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = newEnemySize;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = newEnemySize;
            col.isTrigger = false;

            go.AddComponent<MirrorTrial.Combat.Hurtbox>();

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = 1f;

            var ai = go.AddComponent<EnemyAI>();
            var effectPointObject = new GameObject("AttackEffectPoint");
            effectPointObject.transform.SetParent(go.transform);
            effectPointObject.transform.localPosition = new Vector3(newEnemySize.x * 0.5f, newEnemySize.y * 0.15f, 0f);
            var aiObject = new SerializedObject(ai);
            aiObject.FindProperty("attackEffectPoint").objectReferenceValue = effectPointObject.transform;
            aiObject.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(ai);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            DestroyImmediate(go);
            AssetDatabase.Refresh();

            if (newEnemyAutoSelect && prefab)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log($"[Enemy Editor] Created prefab: {prefabPath}");
        }

        bool ValidateEnemyIdentity()
        {
            if (string.IsNullOrWhiteSpace(newEnemyId))
            {
                EditorUtility.DisplayDialog("缺少敌兵编号", "请先输入敌兵编号，例如 E001。", "确定");
                return false;
            }

            if (string.IsNullOrWhiteSpace(newEnemyName))
            {
                EditorUtility.DisplayDialog("缺少敌兵名称", "请先输入敌兵名称，例如 MeleeGrunt。", "确定");
                return false;
            }

            return true;
        }

        string GetEnemyFolderPath()
        {
            return $"{EnemyLibraryPath}/{GetEnemyFolderName()}";
        }

        string GetEnemyFolderName()
        {
            return $"{SanitizeAssetName(newEnemyId)}_{SanitizeAssetName(newEnemyName.Replace("Enemy_", ""))}";
        }

        string GetEnemyAssetPrefix()
        {
            return $"{SanitizeAssetName(newEnemyId)}_{SanitizeAssetName(newEnemyName.Replace("Enemy_", ""))}";
        }

        static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed";
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Trim().Length);
            foreach (var c in value.Trim())
                builder.Append(System.Array.IndexOf(invalid, c) >= 0 || char.IsWhiteSpace(c) ? '_' : c);
            return builder.ToString();
        }

        static void EnsureEnemyContentFolders(string enemyFolder)
        {
            EnsureDirectoryExists(enemyFolder);
            EnsureDirectoryExists($"{enemyFolder}/Sprites");
            EnsureDirectoryExists($"{enemyFolder}/Animations");
            EnsureDirectoryExists($"{enemyFolder}/AIProfiles");
            EnsureDirectoryExists($"{enemyFolder}/Prefabs");
        }

        static void EnsureDirectoryExists(string assetPath)
        {
            var fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }
    }
}
