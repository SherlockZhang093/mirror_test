#if UNITY_EDITOR
using MirrorTrial.Boss;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    [InitializeOnLoad]
    static class MirrorBossPhysicsAnimationSetup
    {
        const string SessionKey = "MirrorTrial.MirrorBossPhysicsAnimationSetup.v1";
        const string BossPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";

        static MirrorBossPhysicsAnimationSetup() => EditorApplication.delayCall += Apply;

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            var root = PrefabUtility.LoadPrefabContents(BossPath);
            if (!root) return;
            if (!root.GetComponent<MirrorBossPhysicsAnimationFix>())
                root.AddComponent<MirrorBossPhysicsAnimationFix>();
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("MirrorBoss physics and movement animation fix applied.");
        }
    }
}
#endif
