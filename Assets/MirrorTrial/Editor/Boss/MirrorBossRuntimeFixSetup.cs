#if UNITY_EDITOR
using MirrorTrial.Combat;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    [InitializeOnLoad]
    static class MirrorBossRuntimeFixSetup
    {
        const string SessionKey = "MirrorTrial.MirrorBossRuntimeFixSetup.v1";
        const string PlayerPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string BossPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";

        static MirrorBossRuntimeFixSetup()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            FixPlayer();
            FixBoss();
            AssetDatabase.SaveAssets();
            Debug.Log("MirrorBoss runtime collision fix applied: gravity enabled and hierarchical damage relay installed.");
        }

        static void FixPlayer()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            if (!root) return;
            if (!root.GetComponent<Hurtbox>()) root.AddComponent<Hurtbox>();
            InstallRelay(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void FixBoss()
        {
            var root = PrefabUtility.LoadPrefabContents(BossPath);
            if (!root) return;
            var body = root.GetComponent<Rigidbody2D>();
            if (body)
            {
                body.gravityScale = 1f;
                body.bodyType = RigidbodyType2D.Dynamic;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.freezeRotation = true;
            }
            if (!root.GetComponent<Hurtbox>()) root.AddComponent<Hurtbox>();
            InstallRelay(root);
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void InstallRelay(GameObject root)
        {
            foreach (var hitbox in root.GetComponentsInChildren<Hitbox>(true))
            {
                if (!hitbox.GetComponent<HitboxRelayV2>())
                    hitbox.gameObject.AddComponent<HitboxRelayV2>();
            }
        }
    }
}
#endif
