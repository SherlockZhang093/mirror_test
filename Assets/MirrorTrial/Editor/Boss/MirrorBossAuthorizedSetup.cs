#if UNITY_EDITOR
using MirrorTrial.Boss;
using Platformer.Mechanics;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    [InitializeOnLoad]
    static class MirrorBossAuthorizedSetup
    {
        const string SessionKey = "MirrorTrial.MirrorBossAuthorizedSetup.v1";
        const string PlayerPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string ProfilePath = "Assets/MirrorTrial/Boss/MirrorBossProfile.asset";

        static MirrorBossAuthorizedSetup()
        {
            EditorApplication.delayCall += ApplyOnce;
        }

        static void ApplyOnce()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            SetPlayerHealthToFive();
            SetBossDamageToOne();
            // Disabled: rebuilding the prefab changes component file IDs and breaks scene references.
            // MirrorBossBuilder.BuildBossPrefab();
        }

        static void SetPlayerHealthToFive()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            if (!root) return;
            var health = root.GetComponent<Health>();
            if (health) health.maxHP = 5;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void SetBossDamageToOne()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MirrorBossProfile>(ProfilePath);
            if (!profile) return;
            var data = new SerializedObject(profile);
            SetDamage(data, "doubleFirst");
            SetDamage(data, "doubleSecond");
            SetDamage(data, "tripleFirst");
            SetDamage(data, "tripleSecond");
            SetDamage(data, "tripleFinisher");
            SetDamage(data, "heavySlash");
            SetDamage(data, "pursuitSlash");
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        static void SetDamage(SerializedObject data, string attackName)
        {
            var attack = data.FindProperty(attackName);
            var damage = attack?.FindPropertyRelative("damage");
            if (damage != null) damage.intValue = 1;
        }
    }
}
#endif
