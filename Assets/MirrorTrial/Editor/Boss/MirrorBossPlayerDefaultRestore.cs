#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    [InitializeOnLoad]
    static class MirrorBossPlayerDefaultRestore
    {
        const string SessionKey = "MirrorTrial.MirrorBossPlayerDefaultRestore.v1";
        const string PlayerPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";

        static MirrorBossPlayerDefaultRestore()
        {
            EditorApplication.delayCall += Queue;
        }

        static void Queue()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            if (!root) return;
            var weaponController = root.GetComponent("PlayerWeaponController") as MonoBehaviour;
            if (weaponController)
            {
                var data = new SerializedObject(weaponController);
                var activeSlot = data.FindProperty("activeSlotIndex");
                if (activeSlot != null) activeSlot.intValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("Player default weapon restored to the original Unarmed slot; mirror runtime equips Sword only.");
        }
    }
}
#endif
