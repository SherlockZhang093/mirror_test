#if UNITY_EDITOR
using System.Linq;
using MirrorTrial.Boss;
using MirrorTrial.Level;
using MirrorTrial.Player;
using Platformer.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.EditorTools
{
    // Legacy one-shot migration: disabled to avoid rebuilding the Boss on every domain reload.
    static class MirrorBossFinalSetup
    {
        const string SessionKey = "MirrorTrial.MirrorBossFinalSetup.v3";
        const string PlayerPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string BossPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";
        const string ProfilePath = "Assets/MirrorTrial/Boss/MirrorBossSimpleProfile.asset";
        const string ScenePath = "Assets/MirrorTrial/Scenes/Level_Mirror_01.unity";

        static MirrorBossFinalSetup()
        {
            EditorApplication.delayCall += QueueAfterLegacySetups;
        }

        static void QueueAfterLegacySetups()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            SetPlayerInvincibility();
            var bossPrefab = BuildCleanBossPrefab();
            if (bossPrefab) UpgradeMirrorScene(bossPrefab);
            Debug.Log("镜像 Boss 最终简单逻辑已配置：独立目标、动画帧判定、300HP、三阶段固定连招。");
        }

        static void SetPlayerInvincibility()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            if (!root) return;
            var tuning = root.GetComponent<PlayerTuning>();
            if (tuning)
            {
                var data = new SerializedObject(tuning);
                var hurt = data.FindProperty("hurt");
                var invincible = hurt?.FindPropertyRelative("invincibleTime");
                if (invincible != null) invincible.floatValue = 0.35f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var health = root.GetComponent<Health>();
            if (health) health.maxHP = 5;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static GameObject BuildCleanBossPrefab()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MirrorBossSimpleProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<MirrorBossSimpleProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (!playerPrefab) return null;
            var root = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "MirrorBoss";
            root.tag = "Untagged";

            var playerBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(component => component && component.GetType().Namespace == "MirrorTrial.Player")
                .Reverse().ToArray();
            foreach (var component in playerBehaviours) Object.DestroyImmediate(component);
            var legacyBossBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(component => component && component.GetType().Namespace == "MirrorTrial.Boss")
                .Reverse().ToArray();
            foreach (var component in legacyBossBehaviours) Object.DestroyImmediate(component);
            var health = root.GetComponent<Health>();
            if (health) Object.DestroyImmediate(health);

            if (!root.GetComponent<MirrorTrial.Combat.Hurtbox>()) root.AddComponent<MirrorTrial.Combat.Hurtbox>();
            var actor = root.AddComponent<MirrorBossActorV2>();
            root.AddComponent<MirrorBossHudV3>();
            var actorData = new SerializedObject(actor);
            actorData.FindProperty("profile").objectReferenceValue = profile;
            actorData.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, BossPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return saved;
        }

        static void UpgradeMirrorScene(GameObject bossPrefab)
        {
            var current = SceneManager.GetActiveScene();
            var scene = current.path == ScenePath ? current : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var encounter in root.GetComponentsInChildren<CombatEncounter>(true))
                {
                    encounter.enabled = false;
                    var data = new SerializedObject(encounter);
                    var start = data.FindProperty("startOnPlayerEnter");
                    if (start != null) start.boolValue = false;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }

                var oldAreas = root.GetComponentsInChildren<MirrorBossBattleArea>(true);
                foreach (var oldArea in oldAreas) ReplaceArea(oldArea.gameObject, new SerializedObject(oldArea), bossPrefab);
                var v2Areas = root.GetComponentsInChildren<MirrorBossBattleAreaV2>(true);
                foreach (var oldArea in v2Areas) ReplaceArea(oldArea.gameObject, new SerializedObject(oldArea), bossPrefab);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (scene != current) EditorSceneManager.CloseScene(scene, true);
        }

        static void ReplaceArea(GameObject gameObject, SerializedObject oldData, GameObject bossPrefab)
        {
            var area = gameObject.GetComponent<MirrorBossBattleAreaV3>() ?? gameObject.AddComponent<MirrorBossBattleAreaV3>();
            var data = new SerializedObject(area);
            data.FindProperty("bossPrefab").objectReferenceValue = bossPrefab.GetComponent<MirrorBossActorV2>();
            var oldSpawn = oldData.FindProperty("spawnPoint");
            if (oldSpawn != null) data.FindProperty("spawnPoint").objectReferenceValue = oldSpawn.objectReferenceValue;
            var oldEncounter = oldData.FindProperty("encounter");
            if (oldEncounter != null) data.FindProperty("encounter").objectReferenceValue = oldEncounter.objectReferenceValue;
            data.ApplyModifiedPropertiesWithoutUndo();
            var oldV1 = gameObject.GetComponent<MirrorBossBattleArea>();
            if (oldV1) Object.DestroyImmediate(oldV1);
            var oldV2 = gameObject.GetComponent<MirrorBossBattleAreaV2>();
            if (oldV2) Object.DestroyImmediate(oldV2);
        }
    }
}
#endif
