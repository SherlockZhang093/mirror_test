using MirrorTrial.Player;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    [InitializeOnLoad]
    public static class PlayerWeaponSystemMigration
    {
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string MigrationKey = "MirrorTrial.PlayerWeaponSystemMigration.v4";

        static PlayerWeaponSystemMigration()
        {
            EditorApplication.delayCall += RunOnce;
        }

        [MenuItem("Tools/\u955c\u50cf\u8bd5\u70bc/\u6218\u6597/\u91cd\u65b0\u5e94\u7528\u6b66\u5668\u7cfb\u7edf\u5230\u73a9\u5bb6 Prefab")]
        public static void RunFromMenu()
        {
            ApplyMigration(true);
        }

        static void RunOnce()
        {
            if (!SessionState.GetBool(MigrationKey, false))
                ApplyMigration(false);
        }

        static void ApplyMigration(bool notify)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (!root)
                return;
            try
            {
                if (!root.GetComponent<PlayerWeaponController>())
                    root.AddComponent<PlayerWeaponController>();
                if (!root.GetComponent<PlayerBowCombat>())
                    root.AddComponent<PlayerBowCombat>();
                var input = root.GetComponent<PlayerInputReader>();
                if (input) EditorUtility.SetDirty(input);
                var combat = root.GetComponent<PlayerCombat>();
                if (combat)
                {
                    combat.EnsureComboData();
                    AssignDirectCombatClips(combat);
                    EditorUtility.SetDirty(combat);
                }
                AssignClip(root.GetComponent<PlayerAbilityLoadout>(), "mirrorBladeClip", "AirSlash");
                var bow = root.GetComponent<PlayerBowCombat>();
                AssignClip(bow, "drawClip", "BowDraw");
                AssignClip(bow, "fullDrawClip", "BowFull");
                AssignClip(bow, "fireClip", "BowFire");
                if (combat)
                {
                    AssignClip(combat, "swordGuardClip", "SwordGuard");
                    AssignClip(combat, "swordGuardImpactClip", "SwordGuardImpact");
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                SessionState.SetBool(MigrationKey, true);
                if (notify) Debug.Log("\u5df2\u5c06\u56db\u69fd\u6b66\u5668\u7cfb\u7edf\u5e94\u7528\u5230 Player_MirrorTrial Prefab\u3002");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void AssignDirectCombatClips(PlayerCombat combat)
        {
            var serialized = new SerializedObject(combat);
            serialized.Update();
            var sets = serialized.FindProperty("comboSets");
            if (sets != null)
                for (var i = 0; i < sets.arraySize; i++)
                    AssignStepListClips(sets.GetArrayElementAtIndex(i).FindPropertyRelative("steps"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignStepListClips(SerializedProperty steps)
        {
            if (steps == null) return;
            for (var i = 0; i < steps.arraySize; i++)
                AssignStepClip(steps.GetArrayElementAtIndex(i));
        }

        static void AssignStepClip(SerializedProperty step)
        {
            if (step == null) return;
            var clipProperty = step.FindPropertyRelative("animationClip");
            if (clipProperty == null || clipProperty.objectReferenceValue) return;
            var stateProperty = step.FindPropertyRelative("animationState");
            if (stateProperty == null) return;
            var state = (PlayerActionState)stateProperty.enumValueIndex;
            var clipName = state == PlayerActionState.Attack ? "SwordAttack" : state.ToString();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MirrorTrial/Animations/Player/Clips/" + clipName + ".anim");
            if (clip) clipProperty.objectReferenceValue = clip;
        }

        static void AssignClip(Object target, string propertyName, string clipName)
        {
            if (!target) return;
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue) return;
            property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MirrorTrial/Animations/Player/Clips/" + clipName + ".anim");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
