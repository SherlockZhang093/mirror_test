using System.Collections.Generic;
using MirrorTrial.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MirrorTrial.Editor.UI
{
    public static class GameEntrySceneBuilder
    {
        const string ScenePath = "Assets/MirrorTrial/Scenes/GameEntry.unity";
        const string PlayerPrefabPath =
            "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";

        // The menu uses the prefab's sprite frames without instantiating gameplay input or physics.
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

            var menuRoot = FindRoot(scene, "MainMenu");
            if (!menuRoot)
            {
                menuRoot = new GameObject("MainMenu");
                SceneManager.MoveGameObjectToScene(menuRoot, scene);
            }

            // Rebuild only the entry UI. Other GameEntry roots (including the authored
            // far-background GLB objects) belong to the scene and must be preserved.
            var oldCanvas = menuRoot.transform.Find("MainMenuCanvas");
            if (oldCanvas)
                Object.DestroyImmediate(oldCanvas.gameObject);

            var controller = menuRoot.GetComponent<MainMenuControllerComponent>();
            if (!controller)
                controller = menuRoot.AddComponent<MainMenuControllerComponent>();

            RemoveDuplicateEventSystems(scene);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!playerPrefab)
                throw new MissingReferenceException("Could not load player prefab: " + PlayerPrefabPath);
            controller.ConfigureProtagonistAnimation(
                ExtractPlayerFrames(playerPrefab, "Idle"),
                ExtractPlayerFrames(playerPrefab, "Run"));
            controller.BuildSceneContent();

            var startButton = FindButton(menuRoot.transform, "StartButton");
            UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartWithTutorialPreference);

            EditorUtility.SetDirty(startButton);
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForBuild)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("GameEntry scene rebuilt with serialized UI content.");
        }

        static GameObject FindRoot(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == objectName)
                    return roots[i];
            }

            return null;
        }

        static void RemoveDuplicateEventSystems(Scene scene)
        {
            var eventSystems = Object.FindObjectsOfType<EventSystem>(true);
            EventSystem keeper = null;
            for (var i = 0; i < eventSystems.Length; i++)
            {
                var eventSystem = eventSystems[i];
                if (eventSystem.gameObject.scene != scene)
                    continue;
                if (!keeper)
                {
                    keeper = eventSystem;
                    continue;
                }

                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        static Sprite[] ExtractPlayerFrames(GameObject playerPrefab, string clipName)
        {
            var animator = playerPrefab.GetComponentInChildren<Animator>(true);
            if (!animator || !animator.runtimeAnimatorController)
                throw new MissingReferenceException(
                    "Player_MirrorTrial prefab is missing its Visual Animator controller.");

            AnimationClip targetClip = null;
            var clips = animator.runtimeAnimatorController.animationClips;
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] && clips[i].name == clipName)
                {
                    targetClip = clips[i];
                    break;
                }
            }

            if (!targetClip)
                throw new MissingReferenceException(
                    "Player_MirrorTrial Animator does not contain clip: " + clipName);

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(targetClip);
            for (var i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].propertyName != "m_Sprite")
                    continue;

                var keys = AnimationUtility.GetObjectReferenceCurve(targetClip, bindings[i]);
                var frames = new List<Sprite>(keys.Length);
                for (var j = 0; j < keys.Length; j++)
                {
                    if (keys[j].value is Sprite sprite)
                        frames.Add(sprite);
                }

                if (frames.Count > 0)
                    return frames.ToArray();
            }

            throw new MissingReferenceException(
                "Player animation clip does not contain sprite frames: " + clipName);
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
