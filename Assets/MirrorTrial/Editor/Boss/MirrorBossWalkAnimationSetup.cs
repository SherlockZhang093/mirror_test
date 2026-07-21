#if UNITY_EDITOR
using MirrorTrial.Boss;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    [InitializeOnLoad]
    static class MirrorBossWalkAnimationSetup
    {
        const string SessionKey = "MirrorTrial.MirrorBossWalkAnimationSetup.v1";
        const string BossPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";

        static MirrorBossWalkAnimationSetup() => EditorApplication.delayCall += Apply;

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            var root = PrefabUtility.LoadPrefabContents(BossPath);
            if (!root) return;
            if (!root.GetComponent<MirrorBossWalkAnimationOverride>())
                root.AddComponent<MirrorBossWalkAnimationOverride>();
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("MirrorBoss walk animation override applied: SwordWalk is driven directly during approach.");
        }
    }
}
#endif
