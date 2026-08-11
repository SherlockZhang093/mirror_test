using MirrorTrial.Combat;
using MirrorTrial.HealthResources;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.HealthResources
{
    public sealed class HealthResourceEditorWindow : EditorWindow
    {
        GameObject selectedPrefab;
        int durability = 30;
        int reward = 25;
        string hitTrigger = "Hit";
        Color flashColor = Color.white;
        float flashDuration = 0.08f;
        AudioClip hitSound;
        float hitVolume = 1f;

        [MenuItem("Tools/Mirror Trial/关卡/生命资源编辑器")]
        static void Open() => GetWindow<HealthResourceEditorWindow>("生命资源编辑器");

        void OnSelectionChange()
        {
            var candidate = Selection.activeGameObject;
            if (candidate && PrefabUtility.IsPartOfPrefabAsset(candidate))
            {
                selectedPrefab = candidate.transform.root.gameObject;
                ReadCurrentValues();
                Repaint();
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("单 Prefab 生命资源配置", EditorStyles.boldLabel);
            selectedPrefab = (GameObject)EditorGUILayout.ObjectField("当前 Prefab", selectedPrefab, typeof(GameObject), false);
            EditorGUILayout.Space();
            durability = EditorGUILayout.IntField("最大耐久", Mathf.Max(1, durability));
            reward = EditorGUILayout.IntField("生命精华产量", Mathf.Max(0, reward));
            hitTrigger = EditorGUILayout.TextField("受击 Trigger", hitTrigger);
            flashColor = EditorGUILayout.ColorField("闪烁颜色", flashColor);
            flashDuration = EditorGUILayout.FloatField("闪烁时间", Mathf.Max(0f, flashDuration));
            hitSound = (AudioClip)EditorGUILayout.ObjectField("受击音效", hitSound, typeof(AudioClip), false);
            hitVolume = EditorGUILayout.Slider("音量", hitVolume, 0f, 1f);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!selectedPrefab || !PrefabUtility.IsPartOfPrefabAsset(selectedPrefab)))
            {
                if (GUILayout.Button("安装 / 更新并保存", GUILayout.Height(32f)))
                    ConfigurePrefab(selectedPrefab, durability, reward, hitTrigger, flashColor, flashDuration, hitSound, hitVolume);
                if (GUILayout.Button("验证配置")) ValidatePrefab(selectedPrefab, true);
            }
            EditorGUILayout.HelpBox("一次处理一个 Project 中的 Prefab。必需组件会自动安装，现有美术层级不会被改动。resourceNodeId 需要在场景实例上单独配置，Prefab 本体保持为空。", MessageType.Info);
        }

        void ReadCurrentValues()
        {
            if (!selectedPrefab) return;
            var node = selectedPrefab.GetComponent<HealthResourceNode>();
            if (!node) return;
            var data = new SerializedObject(node);
            durability = data.FindProperty("maxDurability").intValue;
            reward = data.FindProperty("lifeEssenceReward").intValue;
            hitTrigger = data.FindProperty("hitTrigger").stringValue;
            flashColor = data.FindProperty("flashColor").colorValue;
            flashDuration = data.FindProperty("flashDuration").floatValue;
            hitSound = data.FindProperty("hitSound").objectReferenceValue as AudioClip;
            hitVolume = data.FindProperty("hitVolume").floatValue;
        }

        public static void ConfigurePrefab(GameObject prefab, int durability, int reward, string trigger,
            Color color, float duration, AudioClip sound, float volume)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path)) throw new System.ArgumentException("A Prefab asset must be selected.");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var collider = root.GetComponent<Collider2D>();
                if (!collider) collider = root.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                if (!root.GetComponent<Hurtbox>()) root.AddComponent<Hurtbox>();
                var node = root.GetComponent<HealthResourceNode>();
                if (!node) node = root.AddComponent<HealthResourceNode>();
                var data = new SerializedObject(node);
                data.FindProperty("maxDurability").intValue = Mathf.Max(1, durability);
                data.FindProperty("lifeEssenceReward").intValue = Mathf.Max(0, reward);
                data.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>(true);
                data.FindProperty("hitTrigger").stringValue = trigger;
                data.FindProperty("flashColor").colorValue = color;
                data.FindProperty("flashDuration").floatValue = Mathf.Max(0f, duration);
                data.FindProperty("hitSound").objectReferenceValue = sound;
                data.FindProperty("hitVolume").floatValue = Mathf.Clamp01(volume);
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                var renderersProperty = data.FindProperty("flashRenderers");
                renderersProperty.arraySize = renderers.Length;
                for (var i = 0; i < renderers.Length; i++)
                    renderersProperty.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            HealthResourceCatalogEditorUtility.Register(
                AssetDatabase.LoadAssetAtPath<GameObject>(path));
            ValidatePrefab(prefab, true);
        }

        static bool ValidatePrefab(GameObject prefab, bool log)
        {
            var valid = prefab && prefab.GetComponent<Collider2D>() && prefab.GetComponent<Hurtbox>() &&
                prefab.GetComponent<HealthResourceNode>();
            if (log)
            {
                if (valid) Debug.Log("[HealthResourceEditor] Prefab 配置验证通过。", prefab);
                else Debug.LogError("[HealthResourceEditor] Prefab 缺少 Collider2D、Hurtbox 或 HealthResourceNode。", prefab);
            }
            return valid;
        }
    }
}
