using System.IO;
using MirrorTrial.Player;
using Platformer.Mechanics;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Player
{
    public static class PlayerStatsSetup
    {
        public const string ProfilePath = "Assets/MirrorTrial/Resources/Player/PlayerInitialStats.asset";
        public const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";

        [InitializeOnLoadMethod]
        static void ScheduleSetup()
        {
            EditorApplication.delayCall += EnsureSetup;
        }

        [MenuItem("Tools/镜像试炼/玩家/创建或修复玩家属性系统")]
        public static void EnsureSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!prefab)
            {
                Debug.LogError("[PlayerStatsSetup] 找不到玩家 Prefab：" + PlayerPrefabPath);
                return;
            }

            var profile = AssetDatabase.LoadAssetAtPath<PlayerInitialStats>(ProfilePath);
            if (!profile)
            {
                EnsureFolder("Assets/MirrorTrial/Resources", "Player");
                profile = ScriptableObject.CreateInstance<PlayerInitialStats>();
                var health = prefab.GetComponent<Health>();
                var reserve = prefab.GetComponent<PlayerHealthReserve>();
                var tuning = prefab.GetComponent<PlayerTuning>();
                var energyGain = prefab.GetComponent<LifeEnergyGainProcessor>();
                profile.ConfigureDefaults(
                    health ? health.maxHP : 5,
                    reserve ? reserve.Current : 0,
                    reserve ? reserve.BaseCapacity : 5,
                    energyGain ? energyGain.BaseDamageConversionRate : 0.2f,
                    energyGain ? energyGain.PlayerConversionMultiplier : 1f,
                    tuning ? tuning.combat.attackDamage : 8,
                    tuning ? tuning.movement.moveSpeed : 5f);
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var initializer = root.GetComponent<PlayerStatsInitializer>();
                if (!initializer) initializer = root.AddComponent<PlayerStatsInitializer>();
                if (!root.GetComponent<PlayerSessionGrowth>()) root.AddComponent<PlayerSessionGrowth>();
                if (!root.GetComponent<PlayerRuntimeStats>()) root.AddComponent<PlayerRuntimeStats>();
                if (!root.GetComponent<LifeEnergyGainProcessor>()) root.AddComponent<LifeEnergyGainProcessor>();

                var serialized = new SerializedObject(initializer);
                serialized.FindProperty("profile").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerStatsSetup] 玩家属性系统已配置完成。", profile);
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
