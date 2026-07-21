using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(MirrorBossActorV2), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class MirrorBossPhysicsAnimationFix : MonoBehaviour
    {
        MirrorBossActorV2 actor;
        Rigidbody2D body;
        Collider2D bossCollider;
        Animator animator;
        Collider2D ignoredPlayerCollider;
        bool collisionIgnored;
        static readonly int SwordRun = Animator.StringToHash("SwordRun");

        void Awake()
        {
            actor = GetComponent<MirrorBossActorV2>();
            body = GetComponent<Rigidbody2D>();
            bossCollider = GetComponent<Collider2D>();
            animator = GetComponentInChildren<Animator>(true);
        }

        void Update()
        {
            if (!collisionIgnored)
            {
                var player = FindObjectOfType<PlayerInputReader>();
                ignoredPlayerCollider = player ? player.GetComponent<Collider2D>() : null;
                if (bossCollider && ignoredPlayerCollider)
                {
                    Physics2D.IgnoreCollision(bossCollider, ignoredPlayerCollider, true);
                    collisionIgnored = true;
                }
            }
        }

        void LateUpdate()
        {
            if (!actor || !body || !animator || actor.CurrentState != MirrorBossActorV2.State.Approach) return;
            if (Mathf.Abs(body.velocity.x) < 0.05f) return;
            animator.Play(SwordRun, 0, Mathf.Repeat(Time.time * 1.35f, 1f));
        }

        void OnDestroy()
        {
            if (bossCollider && ignoredPlayerCollider)
                Physics2D.IgnoreCollision(bossCollider, ignoredPlayerCollider, false);
        }
    }
}
