using System.Collections;
using MirrorTrial.Level;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Boss
{
    public sealed class MirrorBossReturnCoordinatorV2 : MonoBehaviour
    {
        MirrorBossActorV2 boss;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!SceneManager.GetActiveScene().name.Equals("Level_Mirror_01", System.StringComparison.OrdinalIgnoreCase)) return;
            foreach (var listener in FindObjectsOfType<MirrorReturnOnClear>(true)) listener.enabled = false;
            new GameObject("MirrorBossReturnCoordinatorV2").AddComponent<MirrorBossReturnCoordinatorV2>();
        }

        void Update()
        {
            if (boss) return;
            boss = FindObjectOfType<MirrorBossActorV2>();
            if (boss) boss.Defeated += OnDefeated;
        }

        void OnDefeated(MirrorBossActorV2 defeated)
        {
            defeated.Defeated -= OnDefeated;
            StartCoroutine(ReturnAfterDelay());
        }

        IEnumerator ReturnAfterDelay()
        {
            yield return new WaitForSeconds(1.5f);
            var bridge = MirrorTransitionBridge.Instance;
            if (bridge) bridge.NotifyMirrorBossCleared();
        }

        void OnDestroy() { if (boss) boss.Defeated -= OnDefeated; }
    }
}
