using System.Collections;
using MirrorTrial.Enemies;
using MirrorTrial.Boss;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Level
{
    public sealed class MirrorSceneCameraIntro : MonoBehaviour
    {
        static MirrorSceneCameraIntro instance;
        MirrorTrial.Player.PlayerInputReader transitionInput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var go = new GameObject("MirrorSceneCameraIntro");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MirrorSceneCameraIntro>();
        }

        void Awake() => SceneManager.sceneLoaded += OnSceneLoaded;

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.ToLowerInvariant().Contains("mirror")) return;
            // Boss battle areas own their intro camera and activation callback.
            // Starting a second global shot would cancel that callback and leave the fight locked.
            if (FindObjectOfType<MirrorBossBattleAreaV3>()) return;
            transitionInput = FindObjectOfType<MirrorTrial.Player.PlayerInputReader>();
            if (transitionInput) transitionInput.InputEnabled = false;
            var encounter = FindObjectOfType<CombatEncounter>();
            if (encounter) encounter.enabled = false;
            StartCoroutine(PlayAfterSceneSetup(encounter));
        }

        IEnumerator PlayAfterSceneSetup(CombatEncounter encounter)
        {
            yield return null;
            var shotTarget = FindBestTarget();
            if (!shotTarget)
            {
                if (encounter)
                {
                    encounter.enabled = true;
                    if (encounter.StartOnPlayerEnter) encounter.StartEncounter();
                }
                if (transitionInput) transitionInput.InputEnabled = true;
                transitionInput = null;
                yield break;
            }

            var preset = ScriptableObject.CreateInstance<CameraShotPreset>();
            preset.blendIn = 0.5f;
            preset.holdDuration = 1.2f;
            preset.blendOut = 0.4f;
            preset.orthographicSize = 4f;
            preset.lockPlayerInput = true;
            preset.useUnscaledTime = true;

            CameraDirector.Ensure().PlayShot(shotTarget, preset, () =>
            {
                if (encounter)
                {
                    encounter.enabled = true;
                    if (encounter.StartOnPlayerEnter) encounter.StartEncounter();
                }
                Destroy(preset);
                transitionInput = null;
            });
        }

        Transform FindBestTarget()
        {
            CameraShotTarget best = null;
            foreach (var candidate in FindObjectsOfType<CameraShotTarget>())
                if (!best || candidate.Priority > best.Priority) best = candidate;

            if (best) return best.FocusPoint;
            var enemy = FindObjectOfType<EnemyAI>();
            return enemy ? enemy.transform : null;
        }
    }
}
