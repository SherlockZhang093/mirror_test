using UnityEngine;

namespace MirrorTrial.Boss
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MirrorBossActor))]
    public sealed class MirrorBossInputIsolation : MonoBehaviour
    {
        void Awake()
        {
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour || behaviour == this) continue;
                if (behaviour.GetType().Namespace == "MirrorTrial.Player") behaviour.enabled = false;
            }
        }
    }
}
