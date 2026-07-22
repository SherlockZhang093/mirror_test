#if UNITY_EDITOR
using System.IO;
using MirrorTrial.Boss;
using MirrorTrial.Combat;
using MirrorTrial.Level;
using MirrorTrial.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.EditorTools
{
    public static class MirrorBossBuilder
    {
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string ProfilePath = "Assets/MirrorTrial/Boss/MirrorBossProfile.asset";
        const string BossPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";
        public static void BuildBossPrefab()
        {
            EnsureFolder("Assets/MirrorTrial/Boss");
            EnsureFolder("Assets/MirrorTrial/Prefabs/Boss");
            var profile = AssetDatabase.LoadAssetAtPath<MirrorBossProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<MirrorBossProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!source)
            {
                Debug.LogError("找不到主角预制体：" + PlayerPrefabPath);
                return;
            }

            var root = PrefabUtility.InstantiatePrefab(source) as GameObject;
            root.name = "MirrorBoss";
            root.tag = "Untagged";
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour) continue;
                var ns = behaviour.GetType().Namespace;
                if (ns == "MirrorTrial.Player") Object.DestroyImmediate(behaviour);
            }
            if (!root.GetComponent<Hurtbox>()) root.AddComponent<Hurtbox>();
            var actor = root.GetComponent<MirrorBossActor>() ?? root.AddComponent<MirrorBossActor>();
            if (!root.GetComponent<MirrorBossHud>()) root.AddComponent<MirrorBossHud>();
            var actorData = new SerializedObject(actor);
            actorData.FindProperty("profile").objectReferenceValue = profile;
            actorData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            Debug.Log("镜像 Boss 预制体已生成：" + BossPrefabPath);
        }
        public static void CreateBattleArea()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (!prefab)
            {
                BuildBossPrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
                if (!prefab) return;
            }

            var root = new GameObject("镜像Boss战斗区域");
            Undo.RegisterCreatedObjectUndo(root, "创建镜像 Boss 战斗区域");
            var view = SceneView.lastActiveSceneView;
            root.transform.position = view ? view.pivot : Vector3.zero;
            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(14f, 6f);
            var encounter = root.AddComponent<CombatEncounter>();
            var battleArea = root.AddComponent<MirrorBossBattleArea>();

            var spawn = new GameObject("Boss出生点");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.localPosition = new Vector3(3f, 0f, 0f);
            var spawnPoint = spawn.AddComponent<MirrorBossSpawnPoint>();

            var data = new SerializedObject(battleArea);
            data.FindProperty("bossPrefab").objectReferenceValue = prefab.GetComponent<MirrorBossActor>();
            data.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
            data.FindProperty("encounter").objectReferenceValue = encounter;
            data.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("已创建 Boss 战斗区域。请移动‘Boss出生点’并缩放区域上的 BoxCollider2D，然后保存场景。");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
