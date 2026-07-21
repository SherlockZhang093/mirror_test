using UnityEngine;

namespace MirrorTrial.Enemies
{
    [RequireComponent(typeof(EnemyAI))]
    [RequireComponent(typeof(Animator))]
    public class EnemyAIAnimator : MonoBehaviour
    {
        static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int WalkHash = Animator.StringToHash("Walk");
        static readonly int AttackStateHash = Animator.StringToHash("Attack");
        static readonly int AttackedStateHash = Animator.StringToHash("Attacked");

        EnemyAI enemyAI;
        Animator animator;
        EnemyAI.State previousState;

        void Awake()
        {
            enemyAI = GetComponent<EnemyAI>();
            animator = GetComponent<Animator>();
            previousState = enemyAI.CurrentState;
        }

        void Update()
        {
            var state = enemyAI.CurrentState;
            bool shouldWalk = state == EnemyAI.State.Chase || state == EnemyAI.State.Idle;
            animator.SetBool(IsMovingHash, shouldWalk);

            if (state == EnemyAI.State.Attack && previousState != EnemyAI.State.Attack)
            {
                animator.SetTrigger(AttackHash);
                animator.CrossFade(AttackStateHash, 0.04f);
            }
            else if (state == EnemyAI.State.Hurt && previousState != EnemyAI.State.Hurt)
            {
                animator.CrossFade(AttackedStateHash, 0.04f);
            }
            else if (shouldWalk && previousState != state)
            {
                animator.CrossFade(WalkHash, 0.04f);
            }

            previousState = state;
        }
    }
}
