#if UNITY_EDITOR
using MirrorTrial.Combat;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    [InitializeOnLoad]
    static class MirrorBossRuntimeFixCleanup
    {
        const string SessionKey = "MirrorTrial.MirrorBossRuntimeFixCleanup.v1";
        const string PlayerPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string BossPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";

        static MirrorBossRuntimeFixCleanup() => EditorApplication.delayCall += Apply;

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            RemoveRelays(PlayerPath);
            RemoveRelays(BossPath);
            AssetDatabase.SaveAssets();
            Debug.Log("MirrorBoss collision cleanup applied: removed duplicate relay components.");
        }

        static void RemoveRelays(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) return;
            foreach (var relay in root.GetComponentsInChildren<HitboxRelayV2>(true))
                Object.DestroyImmediate(relay);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
