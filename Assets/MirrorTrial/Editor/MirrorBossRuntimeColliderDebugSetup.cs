#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Boss.Editor
{
    [InitializeOnLoad]
    public static class MirrorBossRuntimeColliderDebugSetup
    {
        const string PrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";
        const string SessionKey = "MirrorTrial.MirrorBossRuntimeColliderDebugSetup.v1";

        static MirrorBossRuntimeColliderDebugSetup() => EditorApplication.delayCall += Install;

        static void Install()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (!root) return;
            try
            {
                if (!root.GetComponent<MirrorBossRuntimeColliderDebug>())
                    root.AddComponent<MirrorBossRuntimeColliderDebug>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[MirrorTrial] 已为 Boss 安装运行时攻击框/受击框显示按钮（F8）。");
        }
    }
}
#endif
