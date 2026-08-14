#if UNITY_EDITOR
using System.Linq;
using MirrorTrial.Boss;
using MirrorTrial.Combat;
using MirrorTrial.Player;
using Platformer.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.EditorTools
{
    // Legacy one-shot migration: disabled to avoid overwriting the NodeCanvas Boss prefab.
    static class MirrorBossFinalSetupV4
    {
        const string SessionKey = "MirrorTrial.MirrorBossFinalSetup.v4";
        const string PlayerPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string BossPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";
        const string ProfilePath = "Assets/MirrorTrial/Boss/MirrorBossSimpleProfile.asset";
        const string ScenePath = "Assets/MirrorTrial/Scenes/Level_Mirror_01.unity";

        static MirrorBossFinalSetupV4()
        {
            EditorApplication.delayCall += QueueSecondPass;
        }

        static void QueueSecondPass()
        {
            EditorApplication.delayCall += QueueFinalPass;
        }

        static void QueueFinalPass()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            var playerRoot = PrefabUtility.LoadPrefabContents(PlayerPath);
            if (playerRoot)
            {
                var tuning = playerRoot.GetComponent<PlayerTuning>();
                if (tuning)
                {
                    var tuningData = new SerializedObject(tuning);
                    var invincible = tuningData.FindProperty("hurt")?.FindPropertyRelative("invincibleTime");
                    if (invincible != null) invincible.floatValue = 0.35f;
                    tuningData.ApplyModifiedPropertiesWithoutUndo();
                }
                var playerHealth = playerRoot.GetComponent<Health>();
                if (playerHealth) playerHealth.maxHP = 5;
                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPath);
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }

            var bossPrefab = RebuildIndependentBoss();
            if (bossPrefab) UpdateSceneReference(bossPrefab);
            AssetDatabase.SaveAssets();
        }

        static GameObject RebuildIndependentBoss()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MirrorBossSimpleProfile>(ProfilePath);
            if (!profile) return null;
            profile.maxHitPoints = 300;
            EditorUtility.SetDirty(profile);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (!playerPrefab) return null;
            var root = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            if (!root) return null;
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "MirrorBoss";
            root.tag = "Untagged";

            // Remove dependants before their required player components.
            for (var pass = 0; pass < 3; pass++)
            {
                var components = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(IsPlayerComponent)
                    .OrderBy(RemovalPriority)
                    .ToArray();
                if (components.Length == 0) break;
                foreach (var component in components)
                    if (component) Object.DestroyImmediate(component);
            }

            foreach (var legacy in root.GetComponentsInChildren<MonoBehaviour>(true)
                         .Where(c => c && c.GetType().Namespace == "MirrorTrial.Boss").ToArray())
                Object.DestroyImmediate(legacy);

            var health = root.GetComponent<Health>();
            if (health) Object.DestroyImmediate(health);

            if (!root.GetComponent<Hurtbox>()) root.AddComponent<Hurtbox>();
            var actor = root.AddComponent<MirrorBossActorV2>();
            root.AddComponent<MirrorBossHudV3>();
            var actorData = new SerializedObject(actor);
            actorData.FindProperty("profile").objectReferenceValue = profile;
            actorData.ApplyModifiedPropertiesWithoutUndo();

            var swordHitbox = root.GetComponentInChildren<Hitbox>(true);
            if (swordHitbox && !swordHitbox.GetComponent<MirrorBossHitboxAnimationGate>())
                swordHitbox.gameObject.AddComponent<MirrorBossHitboxAnimationGate>();

            var remaining = root.GetComponentsInChildren<MonoBehaviour>(true).Count(IsPlayerComponent);
            if (remaining != 0)
            {
                Debug.LogError($"MirrorBoss rebuild aborted: {remaining} player components remain.", root);
                Object.DestroyImmediate(root);
                return null;
            }

            var saved = PrefabUtility.SaveAsPrefabAsset(root, BossPath);
            Object.DestroyImmediate(root);
            Debug.Log("MirrorBoss rebuilt as an input-independent actor. HP=300, player invincibility=0.35s.");
            return saved;
        }

        static bool IsPlayerComponent(MonoBehaviour component)
        {
            return component && component.GetType().Namespace == "MirrorTrial.Player";
        }

        static int RemovalPriority(MonoBehaviour component)
        {
            switch (component.GetType().Name)
            {
                case "PlayerDamageReceiver": return 10;
                case "PlayerWeaponController": return 20;
                case "PlayerAbilityLoadout": return 30;
                case "PlayerBowCombat": return 40;
                case "PlayerCombat": return 50;
                case "PlayerAnimationDriver": return 60;
                case "PlayerStateMachine": return 70;
                case "PlayerMotor": return 80;
                case "PlayerTuning": return 90;
                case "PlayerInputReader": return 100;
                default: return 0;
            }
        }

        static void UpdateSceneReference(GameObject bossPrefab)
        {
            var current = SceneManager.GetActiveScene();
            var scene = current.path == ScenePath
                ? current
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var area in sceneRoot.GetComponentsInChildren<MirrorBossBattleAreaV3>(true))
            {
                var data = new SerializedObject(area);
                data.FindProperty("bossPrefab").objectReferenceValue = bossPrefab.GetComponent<MirrorBossActorV2>();
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (scene != current) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
