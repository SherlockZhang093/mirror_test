using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(MirrorBossActorV2))]
    public sealed class MirrorBossWalkAnimationOverride : MonoBehaviour
    {
        MirrorBossActorV2 actor;
        Animator animator;
        static readonly int SwordWalk = Animator.StringToHash("SwordWalk");

        void Awake()
        {
            actor = GetComponent<MirrorBossActorV2>();
            animator = GetComponentInChildren<Animator>(true);
        }

        void LateUpdate()
        {
            if (!actor || !animator || actor.CurrentState != MirrorBossActorV2.State.Approach) return;
            animator.speed = 1f;
            animator.Play(SwordWalk, 0, Mathf.Repeat(Time.time * 1.2f, 1f));
        }
    }
}
