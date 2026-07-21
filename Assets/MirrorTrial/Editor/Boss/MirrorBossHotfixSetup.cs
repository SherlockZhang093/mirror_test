#if UNITY_EDITOR
using MirrorTrial.Boss;
using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.EditorTools
{
    [InitializeOnLoad]
    static class MirrorBossHotfixSetup
    {
        const string SessionKey = "MirrorTrial.MirrorBossHotfixSetup.v2";
        const string BossPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";
        const string MirrorScenePath = "Assets/MirrorTrial/Scenes/Level_Mirror_01.unity";

        static MirrorBossHotfixSetup() => EditorApplication.delayCall += Apply;

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            FixBossPrefab();
            FixMirrorScene();
        }

        static void FixBossPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (!root) return;
            var oldHud = root.GetComponent<MirrorBossHud>();
            if (oldHud) Object.DestroyImmediate(oldHud);
            if (!root.GetComponent<MirrorBossHudV2>()) root.AddComponent<MirrorBossHudV2>();
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void FixMirrorScene()
        {
            var current = SceneManager.GetActiveScene();
            var scene = current.path == MirrorScenePath ? current : EditorSceneManager.OpenScene(MirrorScenePath, OpenSceneMode.Additive);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var encounter in root.GetComponentsInChildren<CombatEncounter>(true))
                {
                    var data = new SerializedObject(encounter);
                    data.FindProperty("startOnPlayerEnter").boolValue = false;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var oldArea in root.GetComponentsInChildren<MirrorBossBattleArea>(true))
                {
                    var oldData = new SerializedObject(oldArea);
                    var replacement = oldArea.GetComponent<MirrorBossBattleAreaV2>() ?? oldArea.gameObject.AddComponent<MirrorBossBattleAreaV2>();
                    var newData = new SerializedObject(replacement);
                    Copy(oldData, newData, "bossPrefab");
                    Copy(oldData, newData, "spawnPoint");
                    Copy(oldData, newData, "encounter");
                    Copy(oldData, newData, "clearDelay");
                    newData.ApplyModifiedPropertiesWithoutUndo();
                    Object.DestroyImmediate(oldArea);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (scene != current) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("镜中 Boss 秒退与血条字体问题已自动修复。");
        }

        static void Copy(SerializedObject from, SerializedObject to, string name)
        {
            var source = from.FindProperty(name);
            var target = to.FindProperty(name);
            if (source == null || target == null) return;
            if (source.propertyType == SerializedPropertyType.ObjectReference) target.objectReferenceValue = source.objectReferenceValue;
            else if (source.propertyType == SerializedPropertyType.Float) target.floatValue = source.floatValue;
        }
    }
}
#endif
