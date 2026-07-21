using UnityEngine;

namespace MirrorTrial.Boss
{
    // Safety gate for the current player attack animations. Even if another script
    // accidentally re-enables the hitbox after the active frames, the collider
    // stays closed until the next attack animation reaches its own active window.
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MirrorBossHitboxAnimationGate : MonoBehaviour
    {
        Collider2D hitCollider;
        Animator animator;

        static readonly int AttackA = Animator.StringToHash("ComboAttackA");
        static readonly int AttackB = Animator.StringToHash("ComboAttackB");
        static readonly int AttackC = Animator.StringToHash("ComboAttackC");
        static readonly int AttackD = Animator.StringToHash("ComboAttackD");

        void Awake()
        {
            hitCollider = GetComponent<Collider2D>();
            animator = GetComponentInParent<Animator>();
        }

        void OnEnable() => Refresh();
        void Update() => Refresh();

        void Refresh()
        {
            if (!hitCollider) hitCollider = GetComponent<Collider2D>();
            if (!animator) animator = GetComponentInParent<Animator>();
            hitCollider.enabled = IsInsideActiveWindow();
        }

        bool IsInsideActiveWindow()
        {
            if (!animator) return false;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            var progress = info.normalizedTime;
            if (info.shortNameHash == AttackA) return progress >= 0.30f && progress < 0.52f;
            if (info.shortNameHash == AttackB) return progress >= 0.31f && progress < 0.54f;
            if (info.shortNameHash == AttackC) return progress >= 0.33f && progress < 0.56f;
            if (info.shortNameHash == AttackD) return progress >= 0.36f && progress < 0.60f;
            return false;
        }
    }
}
