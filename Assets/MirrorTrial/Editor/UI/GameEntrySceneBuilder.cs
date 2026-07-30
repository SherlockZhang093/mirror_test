using MirrorTrial.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MirrorTrial.Editor.UI
{
    public static class GameEntrySceneBuilder
    {
        const string ScenePath = "Assets/MirrorTrial/Scenes/GameEntry.unity";
        const string BackgroundPath = "Assets/MirrorTrial/Art/UI/GameEntry/GameEntryBackground.png";

        [MenuItem("Mirror Trial/Rebuild Game Entry Scene")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Stop Play Mode before rebuilding GameEntry.");
                return;
            }

            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForBuild = !scene.isLoaded;
            if (openedForBuild)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                Object.DestroyImmediate(roots[i]);

            var menuRoot = new GameObject("MainMenu");
            SceneManager.MoveGameObjectToScene(menuRoot, scene);
            var controller = menuRoot.AddComponent<MainMenuControllerComponent>();
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            if (!background)
                throw new MissingReferenceException("Could not load GameEntry background: " + BackgroundPath);
            controller.BuildSceneContent(background);

            var startButton = FindButton(menuRoot.transform, "StartButton");
            UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartWithTutorialPreference);

            EditorUtility.SetDirty(startButton);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForBuild)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("GameEntry scene rebuilt with serialized UI content.");
        }

        static Button FindButton(Transform root, string objectName)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                    return transforms[i].GetComponent<Button>();
            }

            throw new MissingReferenceException("Could not find button: " + objectName);
        }
    }
}
