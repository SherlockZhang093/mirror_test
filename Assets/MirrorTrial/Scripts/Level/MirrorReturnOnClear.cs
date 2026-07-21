using UnityEngine;

namespace MirrorTrial.Level
{
    public class MirrorReturnOnClear : MonoBehaviour
    {
        [ChineseLabel("监听的战斗")] [Tooltip("清场后触发返回现实的战斗")] [SerializeField] CombatEncounter encounter;

        public CombatEncounter Encounter => encounter;

        void Awake()
        {
            EnsureEncounter();
        }

        void OnEnable()
        {
            EnsureEncounter();
            if (encounter) encounter.OnCleared += OnCleared;
        }

        void OnDisable()
        {
            if (encounter) encounter.OnCleared -= OnCleared;
        }

        void OnCleared(CombatEncounter e)
        {
            var bridge = MirrorTransitionBridge.Instance;
            if (bridge != null)
                bridge.NotifyMirrorBossCleared();
            else
                Debug.LogWarning("[MirrorReturnOnClear] MirrorTransitionBridge 不存在，无法返回现实场景。", this);
        }

        void EnsureEncounter()
        {
            if (encounter) return;

            encounter = GetComponent<CombatEncounter>();
            if (encounter) return;

            encounter = GetComponentInParent<CombatEncounter>();
            if (encounter) return;

            var encounters = Object.FindObjectsOfType<CombatEncounter>();
            if (encounters.Length == 1)
            {
                encounter = encounters[0];
                return;
            }

            if (encounters.Length > 1)
                Debug.LogWarning("[MirrorReturnOnClear] 场景中存在多个 CombatEncounter，请在检查器中指定镜中清场监听目标。", this);
            else
                Debug.LogWarning("[MirrorReturnOnClear] 场景中没有找到 CombatEncounter，镜中清场后不会自动返回。", this);
        }
    }
}

