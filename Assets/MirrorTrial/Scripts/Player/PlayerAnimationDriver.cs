using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerStateMachine))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Animator animator;

        [Header("Blend")]
        [SerializeField] float fadeDuration = 0.08f;

        [Header("Animation States")]
        [SerializeField] string idleState = "Idle";
        [SerializeField] string runState = "Run";
        [SerializeField] string jumpRiseState = "JumpRise";
        [SerializeField] string jumpFallState = "JumpFall";
        [SerializeField] string landState = "Land";
        [SerializeField] string attackState = "SwordAttack";
        [SerializeField] string castState = "AirSlash";
        [SerializeField] string dashState = "Dash";
        [SerializeField] string hurtState = "HitDamage";
        [SerializeField] string deadState = "Die";

        PlayerStateMachine stateMachine;
        PlayerActionState playingState = PlayerActionState.None;

        void Awake()
        {
            if (!animator)
                animator = GetComponentInChildren<Animator>(true);

            if (animator)
                animator.applyRootMotion = false;

            stateMachine = GetComponent<PlayerStateMachine>();
        }

        void Update()
        {
            if (!animator || stateMachine == null)
                return;

            Play(stateMachine.CurrentState);
        }

        public void ForceState(PlayerActionState state)
        {
            if (stateMachine != null)
                stateMachine.RequestAction(state);
        }

        public void ClearForcedState(PlayerActionState state)
        {
            if (stateMachine != null)
                stateMachine.ReleaseAction(state);
        }

        void Play(PlayerActionState state)
        {
            if (state == playingState)
                return;

            var stateName = GetAnimationStateName(state);
            if (string.IsNullOrEmpty(stateName))
            {
                if (state == PlayerActionState.Land)
                    stateName = idleState;
                if (string.IsNullOrEmpty(stateName))
                    return;
            }

            animator.CrossFade(stateName, fadeDuration);
            playingState = state;
        }

        string GetAnimationStateName(PlayerActionState state)
        {
            switch (state)
            {
                case PlayerActionState.Idle: return idleState;
                case PlayerActionState.Run: return runState;
                case PlayerActionState.JumpRise: return jumpRiseState;
                case PlayerActionState.JumpFall: return jumpFallState;
                case PlayerActionState.Land: return landState;
                case PlayerActionState.Attack: return attackState;
                case PlayerActionState.Cast: return castState;
                case PlayerActionState.Dash: return dashState;
                case PlayerActionState.Hurt: return hurtState;
                case PlayerActionState.Dead: return deadState;
                default: return string.Empty;
            }
        }
    }
}
