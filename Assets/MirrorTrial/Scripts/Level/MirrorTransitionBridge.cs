using System.Collections.Generic;
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
            pendingMirrorGateId = null;
            pendingRealityScene = null;

            OnReturnToRealityScene?.Invoke();
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
            if (Instance == this)
                Instance = null;
        }
    }
}