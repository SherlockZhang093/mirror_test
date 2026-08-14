#if UNITY_EDITOR
using MirrorTrial.Boss;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    // Legacy migration only. MirrorBossInputIsolation requires the retired
    // MirrorBossActor and must not be added to the current V2 prefab.
    static class MirrorBossInputIsolationSetup
    {
        const string SessionKey = "MirrorTrial.MirrorBossInputIsolationSetup.v1";
        const string BossPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (!root) return;
            if (!root.GetComponent<MirrorBossInputIsolation>()) root.AddComponent<MirrorBossInputIsolation>();
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("镜像 Boss 已与玩家 WASD 输入隔离。");
        }
    }
}
#endif
