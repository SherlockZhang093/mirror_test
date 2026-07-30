#if UNITY_EDITOR
using MirrorTrial.Boss;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    // Legacy one-shot migration. The BehaviourTree setup removes this component.
    static class MirrorBossPhysicsAnimationSetup
    {
        const string SessionKey = "MirrorTrial.MirrorBossPhysicsAnimationSetup.v1";
        const string BossPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";

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
