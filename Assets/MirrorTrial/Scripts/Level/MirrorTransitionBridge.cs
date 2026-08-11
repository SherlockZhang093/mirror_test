using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Player;
using MirrorTrial.Growth;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Level
{
    public class MirrorTransitionBridge : MonoBehaviour
    {
        public static MirrorTransitionBridge Instance { get; private set; }

        [Header("Runtime State")]
        [SerializeField] string pendingMirrorGateId;
        [SerializeField] string pendingRealityScene;
        [SerializeField] GameObject persistentPlayer;

        readonly HashSet<string> completedGates = new HashSet<string>();
        bool swordUnlocked;
        bool bowUnlocked;
        PlayerWeaponType pendingUnlockPresentation = PlayerWeaponType.Unarmed;

        public GameObject PersistentPlayer => persistentPlayer;
        public bool IsInMirror => !string.IsNullOrEmpty(pendingMirrorGateId);

        public static event System.Action OnEnterMirrorScene;
        public static event System.Action OnReturnToRealityScene;

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static MirrorTransitionBridge Ensure()
        {
            if (Instance) return Instance;

            var go = new GameObject("MirrorTransitionBridge");
            go.AddComponent<MirrorTransitionBridge>();
            return Instance;
        }

        public void EnterMirror(string gateId, string mirrorSceneName, GameObject player)
        {
            pendingMirrorGateId = gateId;
            pendingRealityScene = SceneManager.GetActiveScene().name;

            if (player)
            {
                if (persistentPlayer && persistentPlayer != player)
                    Destroy(persistentPlayer);

                persistentPlayer = player;
                DontDestroyOnLoad(player);
            }

            OnEnterMirrorScene?.Invoke();
            // Never carry a hit-stop freeze across a scene transition.
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            SceneManager.LoadScene(mirrorSceneName, LoadSceneMode.Single);
        }

        public void NotifyMirrorBossCleared()
        {
            if (string.IsNullOrEmpty(pendingMirrorGateId) || string.IsNullOrEmpty(pendingRealityScene))
            {
                Debug.LogWarning("[MirrorTransitionBridge] Cannot return to reality scene because mirror state is missing.", this);
                return;
            }

            completedGates.Add(pendingMirrorGateId);
            var realityScene = pendingRealityScene;
            UnlockRewardFor(realityScene);
            pendingMirrorGateId = null;
            pendingRealityScene = null;

            LevelEndGrowthController.TryShow(persistentPlayer, () => CompleteMirrorBossClear(realityScene));
        }

        void CompleteMirrorBossClear(string realityScene)
        {
            OnReturnToRealityScene?.Invoke();
            if (!GameFlow.TryLoadNextRealityScene(realityScene))
                SceneManager.LoadScene(realityScene, LoadSceneMode.Single);
        }

        public bool ReturnToRealityAfterPlayerDeath()
        {
            if (string.IsNullOrEmpty(pendingRealityScene))
            {
                Debug.LogWarning("[MirrorTransitionBridge] Cannot respawn in reality because the source scene is missing.", this);
                return false;
            }

            var realityScene = pendingRealityScene;
            pendingMirrorGateId = null;
            pendingRealityScene = null;

            OnReturnToRealityScene?.Invoke();
            SceneManager.LoadScene(realityScene, LoadSceneMode.Single);
            return true;
        }
        public bool IsGateCompleted(string gateId)
        {
            return !string.IsNullOrEmpty(gateId) && completedGates.Contains(gateId);
        }

        public void RegisterPersistentPlayer(GameObject player)
        {
            if (!player) return;

            if (persistentPlayer && persistentPlayer != player)
            {
                Destroy(player);
                return;
            }

            persistentPlayer = player;
            DontDestroyOnLoad(player);
            ApplyWeaponProgression(player);
        }

        public GameObject GetOrCreatePersistentPlayer(GameObject scenePlayer)
        {
            if (persistentPlayer && scenePlayer && persistentPlayer != scenePlayer)
            {
                Destroy(scenePlayer);
                return persistentPlayer;
            }

            if (persistentPlayer) return persistentPlayer;

            if (scenePlayer)
            {
                persistentPlayer = scenePlayer;
                DontDestroyOnLoad(scenePlayer);
                return persistentPlayer;
            }

            return null;
        }

        public void PlacePlayerAt(Vector3 position)
        {
            if (persistentPlayer)
                persistentPlayer.transform.position = position;
        }

        public void ResetForNewGame()
        {
            ClearAll();

            var session = GameSessionProgress.Instance;
            if (session != null)
                session.ResetSession();

            swordUnlocked = false;
            bowUnlocked = false;
            pendingUnlockPresentation = PlayerWeaponType.Unarmed;

            if (persistentPlayer)
                Destroy(persistentPlayer);

            persistentPlayer = null;
        }

        public void ClearAll()
        {
            pendingMirrorGateId = null;
            pendingRealityScene = null;
            completedGates.Clear();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
                Instance = null;
        }

        void UnlockRewardFor(string completedRealityScene)
        {
            if (completedRealityScene.Equals(GameFlow.FirstRealitySceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                swordUnlocked = true;
                pendingUnlockPresentation = PlayerWeaponType.Sword;
            }
            else if (completedRealityScene.Equals(GameFlow.SecondRealitySceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                bowUnlocked = true;
                pendingUnlockPresentation = PlayerWeaponType.Bow;
            }

            ApplyWeaponProgression(persistentPlayer);
            var weapons = persistentPlayer ? persistentPlayer.GetComponent<PlayerWeaponController>() : null;
            if (weapons && pendingUnlockPresentation != PlayerWeaponType.Unarmed)
                weapons.ForceEquipWeapon(pendingUnlockPresentation);
        }

        void ApplyWeaponProgression(GameObject player)
        {
            if (!player) return;
            var weapons = player.GetComponent<PlayerWeaponController>();
            if (weapons) weapons.ApplyProgression(swordUnlocked, bowUnlocked);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyWeaponProgression(persistentPlayer);
            if (pendingUnlockPresentation == PlayerWeaponType.Unarmed || !persistentPlayer)
                return;

            var weapon = pendingUnlockPresentation;
            pendingUnlockPresentation = PlayerWeaponType.Unarmed;
            StartCoroutine(PlayUnlockWhenPresentationIsClear(weapon));
        }

        IEnumerator PlayUnlockWhenPresentationIsClear(PlayerWeaponType weapon)
        {
            while (SceneTransitionController.IsTransitioning || IsStorySequencePlaying())
                yield return null;

            if (persistentPlayer)
                PlayerWeaponUnlockSequence.Play(persistentPlayer, weapon);
        }

        static bool IsStorySequencePlaying()
        {
            var sequences = FindObjectsOfType<StoryTutorialSequence>(true);
            for (var i = 0; i < sequences.Length; i++)
            {
                if (sequences[i].IsPlaying)
                    return true;
            }

            return false;
        }
    }
}
