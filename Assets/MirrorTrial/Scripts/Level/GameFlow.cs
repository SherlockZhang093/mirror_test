using UnityEngine.SceneManagement;

namespace MirrorTrial.Level
{
    public static class GameFlow
    {
        public const string EntrySceneName = "SampleScene";
        public const string FirstRealitySceneName = "Level_Reality_01";

        static readonly string[] RealityScenes =
        {
            FirstRealitySceneName
        };

        public static void StartNewGame()
        {
            MirrorTransitionBridge.Ensure().ResetForNewGame();
            SceneManager.LoadScene(FirstRealitySceneName, LoadSceneMode.Single);
        }

        public static void ReturnToEntry()
        {
            var bridge = MirrorTransitionBridge.Instance;
            if (bridge != null)
                bridge.ClearAll();
            SceneManager.LoadScene(EntrySceneName, LoadSceneMode.Single);
        }

        public static bool TryLoadNextRealityScene(string currentSceneName)
        {
            for (var i = 0; i < RealityScenes.Length - 1; i++)
            {
                if (RealityScenes[i] != currentSceneName)
                    continue;

                SceneManager.LoadScene(RealityScenes[i + 1], LoadSceneMode.Single);
                return true;
            }

            return false;
        }
    }
}