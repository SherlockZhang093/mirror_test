using UnityEngine;

namespace MirrorTrial.Level
{
    public sealed class CameraShotTarget : MonoBehaviour
    {
        [SerializeField] string targetId = "EnemyIntro";
        [SerializeField] int priority;
        [SerializeField] Transform focusPoint;

        public string TargetId => targetId;
        public int Priority => priority;
        public Transform FocusPoint => focusPoint ? focusPoint : transform;
    }
}
