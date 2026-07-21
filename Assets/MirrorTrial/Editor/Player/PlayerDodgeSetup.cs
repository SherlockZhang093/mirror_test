using MirrorTrial.Player;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Player
{
    [InitializeOnLoad]
    public static class PlayerDodgeSetup
    {
        const string PlayerPrefabPath =
            "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";

        static PlayerDodgeSetup()
        {
            EditorApplication.delayCall += EnsurePlayerDodge;
        }

        [MenuItem("Tools/Mirror Trial/Player/Configure Dodge")]
        public static void EnsurePlayerDodge()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!prefab)
                return;

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (!root)
                return;

            try
            {
                if (!root.GetComponent<PlayerDodgeController>())
                    root.AddComponent<PlayerDodgeController>();

                ConfigureInput(root.GetComponent<PlayerInputReader>());
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureInput(PlayerInputReader input)
        {
            if (!input)
                return;

            var serializedInput = new SerializedObject(input);
            var bindings = serializedInput.FindProperty("bindings");
            var hasDodge = false;

            for (var i = 0; i < bindings.arraySize; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                var command = binding.FindPropertyRelative("command");
                var key = binding.FindPropertyRelative("key");

                if (command.intValue == (int)PlayerInputCommand.Dodge)
                    hasDodge = true;

                if (command.intValue == (int)PlayerInputCommand.MobilitySkill &&
                    key.intValue == (int)KeyCode.LeftShift)
                {
                    key.intValue = (int)KeyCode.Q;
                }
            }

            if (!hasDodge)
            {
                var index = bindings.arraySize;
                bindings.InsertArrayElementAtIndex(index);
                var binding = bindings.GetArrayElementAtIndex(index);
                binding.FindPropertyRelative("command").intValue =
                    (int)PlayerInputCommand.Dodge;
                binding.FindPropertyRelative("buttonName").stringValue =
                    string.Empty;
                binding.FindPropertyRelative("key").intValue =
                    (int)KeyCode.LeftShift;
            }

            serializedInput.FindProperty("bindingSchemaVersion").intValue = 3;
            serializedInput.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}