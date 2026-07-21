using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;
using MirrorTrial.Player;

namespace MirrorTrial.Level
{
    public sealed class CameraDirectorBootstrap : MonoBehaviour
    {
        static CameraDirectorBootstrap instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var go = new GameObject("CameraDirectorBootstrap");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CameraDirectorBootstrap>();
        }

        void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Start()
        {
            BindCurrentScene();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindCurrentScene();
        }

        void BindCurrentScene()
        {
            var main = Camera.main;
            if (!main) return;
            var brain = main.GetComponent<CinemachineBrain>();
            if (!brain) brain = main.gameObject.AddComponent<CinemachineBrain>();

            var player = FindObjectOfType<PlayerInputReader>();
            if (!player) return;

            var director = CameraDirector.Ensure();
            director.SetBrain(brain);
            director.SyncLens(main);
            director.SetPlayerTarget(player.transform);

            var level = FindObjectOfType<LevelManager>();
            if (level && level.TryGetSceneHorizontalBounds(out var minX, out var maxX))
                director.SetHorizontalBounds(minX, maxX);

        }
    }
}
