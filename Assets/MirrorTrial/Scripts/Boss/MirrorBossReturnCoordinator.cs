using MirrorTrial.Level;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Boss
{
    public sealed class MirrorBossReturnCoordinator : MonoBehaviour
    {
        MirrorBossActor observedBoss;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Equals("Level_Mirror_01", System.StringComparison.OrdinalIgnoreCase)) return;
            foreach (var listener in FindObjectsOfType<MirrorReturnOnClear>(true)) listener.enabled = false;
            var coordinator = new GameObject("MirrorBossReturnCoordinator");
            coordinator.AddComponent<MirrorBossReturnCoordinator>();
        }

        void Update()
        {
            if (observedBoss) return;
            observedBoss = FindObjectOfType<MirrorBossActor>();
            if (observedBoss) observedBoss.Defeated += OnBossDefeated;
        }

        void OnBossDefeated(MirrorBossActor boss)
        {
            boss.Defeated -= OnBossDefeated;
            enabled = false;
            var bridge = MirrorTransitionBridge.Instance;
            if (bridge) bridge.NotifyMirrorBossCleared();
            else Debug.LogWarning("[MirrorBossReturnCoordinator] 找不到 MirrorTransitionBridge，无法返回现实场景。", this);
        }

        void OnDestroy()
        {
            if (observedBoss) observedBoss.Defeated -= OnBossDefeated;
        }
    }
}
